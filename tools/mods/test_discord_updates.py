import copy
import io
import json
import os
import tempfile
import unittest
import urllib.error
from pathlib import Path
from unittest.mock import patch

import discord_updates as discord


def entry(version='1.0.0'):
    return {'uid': '12345678-1234-1234-1234-123456789abc', 'id': 'test', 'name': 'Test Package',
            'version': version, 'manifest': {'authors': [{'name': 'Potato'}], 'type': 'font',
                                            'summary': 'Texto em português'},
            'files': ['icon.png', 'description.md'],
            'download': {'url': f'https://github.com/example/releases/download/v{version}/test.bl',
                         'size': 1048576, 'sha256': 'a' * 64}}


class DiscordTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        for attribute, value in [('CONFIG', self.root / 'webhook.txt'), ('STATE', self.root / 'state.json')]:
            replacement = patch.object(discord, attribute, value)
            replacement.start()
            self.addCleanup(replacement.stop)
        environment = patch.dict(os.environ, {'BL_DISCORD_WEBHOOK_URL': ''})
        environment.start()
        self.addCleanup(environment.stop)
        self.url = 'https://discord.com/api/webhooks/123/private-test-token'
        discord.CONFIG.write_text(self.url, encoding='utf-8')
        network = patch.object(discord.urllib.request, 'urlopen')
        self.network = network.start()
        self.addCleanup(network.stop)
        self.network.return_value.__enter__.return_value.read.return_value = b'{"id":"12345"}'
        self.output = io.StringIO()
        output = patch('sys.stdout', self.output)
        output.start()
        self.addCleanup(output.stop)

    def events(self, previous=None, current=None, paths=None, commit='new'):
        return discord.catalog_events({'mods': previous or []}, {'mods': current or []},
                                      'old', commit, paths or [])

    def test_new_update_listing_and_removal(self):
        original, updated = entry(), entry('1.1.0')
        for old, new, title in [([], [original], 'New package published'),
                                ([original], [updated], 'Package updated'),
                                ([original], [], 'Package removed')]:
            with self.subTest(title=title):
                body = self.events(old, new)[0]['payload']
                self.assertTrue(body['embeds'][0]['title'].startswith(title))
                self.assertEqual(body['allowed_mentions'], {'parse': []})
                self.assertNotIn('Texto em', json.dumps(body))
        changed = copy.deepcopy(original)
        changed['manifest']['authors'] = ['New author']
        body = self.events([original], [changed])[0]['payload']
        self.assertTrue(body['embeds'][0]['title'].startswith('Catalog listing updated'))
        body = self.events([original], [original], [f"mods/{original['uid']}/description.md"])[0]['payload']
        self.assertTrue(body['embeds'][0]['title'].startswith('Catalog listing updated'))

    def test_unchanged_catalog_and_unrelated_files_do_not_announce(self):
        self.assertEqual(self.events([entry()], [entry()], ['README.md', 'ports/font.bl']), [])

    def test_removed_icon_uses_previous_commit(self):
        embed = self.events([entry()], [])[0]['payload']['embeds'][0]
        self.assertIn('/old/mods/', embed['thumbnail']['url'])
        self.assertIn('Previous release', embed['fields'][-1]['value'])
        self.assertNotIn('Download package', embed['fields'][-1]['value'])

    def test_embed_limits_and_emoji_free_text(self):
        data = entry()
        data['name'] = '\U0001f525 <a:party:123> 1\ufe0f\u20e3 ' + 'Name_' * 2000
        data['manifest']['authors'] = '\U0001f954' + 'Author*' * 2000
        body = self.events([], [data])[0]['payload']
        embed = body['embeds'][0]
        self.assertLessEqual(len(embed['title']), 256)
        total = len(embed['title']) + len(embed['description']) + len(embed['footer']['text'])
        for field in embed['fields']:
            self.assertLessEqual(len(field['name']), 256)
            self.assertLessEqual(len(field['value']), 1024)
            total += len(field['name']) + len(field['value'])
        self.assertLessEqual(total, 6000)
        serialized = json.dumps(body, ensure_ascii=False)
        for emoji in ['\U0001f525', '\U0001f954', '<a:party:123>', '\u20e3']:
            self.assertNotIn(emoji, serialized)

    def test_confirmed_delivery_is_persisted_and_deduplicated(self):
        events = self.events([], [entry()])
        discord.notify(events)
        discord.notify(events)
        self.network.assert_called_once()
        self.assertEqual(discord.status()['pending'], 0)
        self.assertEqual(discord.load_state()['delivered'], [events[0]['id']])
        request = self.network.call_args.args[0]
        self.assertIn('wait=true', request.full_url)
        self.assertNotIn('private-test-token', json.dumps(discord.status()))

    def test_offline_notification_survives_reload_and_retry(self):
        self.network.side_effect = urllib.error.URLError(self.url)
        discord.notify(self.events([], [entry()]))
        self.assertEqual(discord.status()['pending'], 1)
        self.assertNotIn('private-test-token', self.output.getvalue())
        self.assertNotIn('private-test-token', discord.STATE.read_text(encoding='utf-8'))
        self.network.side_effect = None
        discord.flush()
        self.assertEqual(discord.status()['pending'], 0)

    def test_rate_limit_preserves_queue_and_honors_retry_after(self):
        error = urllib.error.HTTPError(self.url, 429, 'Too many requests', {}, io.BytesIO(b'{"retry_after":30}'))
        self.network.side_effect = error
        with patch.object(discord.time, 'time', return_value=100):
            discord.notify(self.events([], [entry()]))
            self.assertEqual(discord.status()['retry_at'], 130)
            discord.flush()
        self.assertEqual(self.network.call_count, 1)
        self.assertEqual(discord.status()['pending'], 1)
        self.network.side_effect = None
        with patch.object(discord.time, 'time', return_value=131):
            discord.flush()
        self.assertEqual(discord.status()['pending'], 0)

    def test_http_failure_and_missing_confirmation_are_not_marked_delivered(self):
        for failure in ['http', 'empty']:
            with self.subTest(failure=failure):
                if discord.STATE.exists():
                    discord.STATE.unlink()
                self.network.side_effect = urllib.error.HTTPError(self.url, 404, self.url, {}, None) if failure == 'http' else None
                self.network.return_value.__enter__.return_value.read.return_value = b'{}'
                discord.notify(self.events([], [entry()]))
                self.assertEqual(discord.status()['pending'], 1)
                self.assertEqual(discord.load_state()['delivered'], [])
                self.assertNotIn('private-test-token', self.output.getvalue())

    def test_invalid_or_missing_configuration_never_posts(self):
        for value in ['', 'http://discord.com/api/webhooks/123/token',
                      'https://other.invalid/api/webhooks/123/token', 'https://discord.com@other.invalid/path',
                      'https://[invalid']:
            with self.subTest(value=value):
                discord.CONFIG.write_text(value, encoding='utf-8')
                discord.notify(self.events([], [entry()]))
        self.network.assert_not_called()
        self.assertFalse(discord.STATE.exists())

    def test_corrupt_queue_is_preserved(self):
        for raw in ['{broken', '{"pending":[null],"delivered":[]}',
                    '{"pending":[],"delivered":[],"retry_at":"later"}']:
            with self.subTest(raw=raw):
                discord.STATE.write_text(raw, encoding='utf-8')
                discord.notify(self.events([], [entry()]))
                self.assertEqual(discord.STATE.read_text(encoding='utf-8'), raw)
        self.network.assert_not_called()

    def test_connection_test_has_no_catalog_side_effect(self):
        self.assertEqual(discord.send_test(), '12345')
        self.assertFalse(discord.STATE.exists())
        body = json.loads(self.network.call_args.args[0].data)
        self.assertEqual(body['embeds'][0]['title'], 'Catalog notifications connected')


if __name__ == '__main__':
    unittest.main()

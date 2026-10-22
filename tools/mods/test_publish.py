import json
import os
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import publish
import discord_updates


class PackageTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.path = Path(temporary.name) / 'package.bl'

    def package(self, files, kind=None):
        manifest = {'uid': '12345678-1234-1234-1234-123456789abc', 'id': 'test',
                    'version': '1.0.0', 'name': 'Test', 'blVersion': 2}
        if kind is not None:
            manifest['type'] = kind
        with zipfile.ZipFile(self.path, 'w') as archive:
            archive.writestr('manifest.json', json.dumps(manifest))
            archive.writestr('icon.png', b'icon')
            for name in files:
                archive.writestr(name, b'content')
        return publish.Package(self.path)

    def test_texture_aliases_accept_images_without_script(self):
        for kind in ['texture', 'textures', 'textura', 'texturas', 'resourcepack', 'resource-pack', ' Texture ']:
            with self.subTest(kind=kind):
                package = self.package(['content/Images/UI/Inventory.PNG'], kind)
                self.addCleanup(package.zip.close)
                self.assertEqual(package.manifest['type'], kind)

    def test_texture_requires_image_in_supported_directory(self):
        for files in [[], ['content/main.js'], ['content/Elsewhere/test.png'], ['content/Images/test.txt']]:
            with self.subTest(files=files), self.assertRaisesRegex(publish.Fail, 'sem PNGs'):
                self.package(files, 'texture')

    def test_mod_and_unknown_type_keep_script_requirement(self):
        for kind in [None, 'mod', 'unknown']:
            with self.subTest(kind=kind), self.assertRaisesRegex(publish.Fail, 'sem content/main.js'):
                self.package(['content/Images/test.png'], kind)

    def test_native_content_directory_is_accepted(self):
        package = self.package(['Content/Images/test.png'], 'texture')
        self.addCleanup(package.zip.close)
        self.assertIn('Content/Images/test.png', package.names)

    def test_mod_with_script_is_accepted(self):
        package = self.package(['content/main.js'])
        self.addCleanup(package.zip.close)
        self.assertEqual(package.id, 'test')

    def test_alternative_texture_filename_is_accepted(self):
        package = self.package(['content/Images/Item_1225 (Alternative).png'], 'texture')
        self.addCleanup(package.zip.close)
        self.assertIn('content/Images/Item_1225 (Alternative).png', package.names)

    def test_unsafe_package_paths_are_rejected(self):
        for name in ['../outside.png', 'content/../outside.png', '/absolute.png', 'content/image.png\n']:
            with self.subTest(name=name), self.assertRaisesRegex(publish.Fail, 'caminho'):
                self.package([name], 'texture')

    def broken(self, data):
        self.path.write_bytes(data)
        with self.assertRaises(publish.Fail) as caught:
            publish.Package(self.path)
        return str(caught.exception)

    def valid_bytes(self):
        package = self.package(['content/main.js'])
        package.zip.close()
        # Texto com \n em entrada sem compressão: o caso que a conversão estraga.
        with zipfile.ZipFile(self.path, 'a', zipfile.ZIP_STORED) as archive:
            archive.writestr('description.md', '# Mod\n\nlinha 1\nlinha 2\n' * 20)
        return self.path.read_bytes()

    def test_not_zip_explains_text_conversion(self):
        data = self.valid_bytes()
        self.assertNotIn(b'\r\n', data)
        self.assertIn('tratado como texto', self.broken(data.replace(b'\n', b'\r\n')))

    def test_not_zip_explains_cut_file(self):
        data = self.valid_bytes()
        self.assertIn('cortado', self.broken(data[:len(data) // 2]))

    def test_not_zip_explains_web_page_empty_and_other_files(self):
        self.assertIn('página da web', self.broken(b'<!DOCTYPE html><html><body>Discord</body></html>'))
        self.assertIn('vazio', self.broken(b''))
        self.assertIn('ef bb bf 7b', self.broken(b'\xef\xbb\xbf{"uid": "x"}'))

    def test_store_takes_animated_banner_and_theme_background(self):
        package = self.package(['content/main.js', 'banner.gif', 'docs/bg.gif'])
        self.addCleanup(package.zip.close)
        package.manifest['theme'] = {'background': 'docs/bg.gif'}
        files = package.store_files()
        self.assertIn('banner.gif', files)
        self.assertIn('docs/bg.gif', files)
        package.manifest['theme'] = {'background': '#203040'}
        self.assertNotIn('#203040', package.store_files())

    def test_store_takes_localized_markdown_assets(self):
        package = self.package([
            'content/main.js', 'description_zh-Hans.md', 'changelog_zh-Hans.md',
        ])
        self.addCleanup(package.zip.close)
        files = package.store_files()
        self.assertEqual(
            ['changelog_zh-Hans.md', 'description_zh-Hans.md'],
            [f for f in files if f.startswith(('description_', 'changelog_'))],
        )

    def test_store_ignores_localized_description_lookalikes(self):
        package = self.package([
            'content/main.js', 'description_en_US.md', 'description_en.md.bak',
            'description_en-US.MD', 'description_english.md', 'description_zh-Hans.txt',
            'changelog_en_US.md', 'changelog_en.md.bak', 'changelog_en-US.MD',
            'changelog_english.md', 'changelog_zh-Hans.txt', 'docs/changelog_en-US.md',
            'docs/description_en-US.md',
        ])
        self.addCleanup(package.zip.close)
        files = package.store_files()
        self.assertFalse(any(f.startswith(('description_', 'changelog_')) for f in files))
        self.assertNotIn('docs/description_en-US.md', files)
        self.assertNotIn('docs/changelog_en-US.md', files)

    def test_store_paths_keep_launcher_restrictions(self):
        package = self.package(['content/main.js', 'page (1).md'])
        self.addCleanup(package.zip.close)
        package.manifest['pages'] = [{'file': 'page (1).md'}]
        with self.assertRaisesRegex(publish.Fail, 'vitrine'):
            package.store_files()


class CatalogGitTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        for attribute, value in [('CONFIG', self.root / 'webhook.txt'), ('STATE', self.root / 'discord.json')]:
            replacement = patch.object(discord_updates, attribute, value)
            replacement.start()
            self.addCleanup(replacement.stop)
        environment = patch.dict(os.environ, {'BL_DISCORD_WEBHOOK_URL': ''})
        environment.start()
        self.addCleanup(environment.stop)
        sender = patch.object(discord_updates, 'send', return_value={'id': '12345'})
        self.sender = sender.start()
        self.addCleanup(sender.stop)
        self.remote = self.root / 'remote.git'
        self.other = self.root / 'other'
        self.local = self.root / 'catalog'
        publish.git('init', '--bare', '--initial-branch=main', str(self.remote), cwd=self.root)
        self.clone(self.other)
        (self.other / 'index.json').write_text('{"format": 1, "mods": []}\n', encoding='utf-8')
        self.record(self.other, 'Initial catalog')
        publish.git('push', '-u', 'origin', 'main', cwd=self.other)
        self.clone(self.local)
        self.worktree_patch = patch.object(publish, 'WORKTREE', self.local)
        self.worktree_patch.start()
        self.addCleanup(self.worktree_patch.stop)

    def clone(self, path):
        publish.git('clone', str(self.remote), str(path), cwd=self.root)
        publish.git('config', 'user.name', 'Catalog test', cwd=path)
        publish.git('config', 'user.email', 'catalog-test@example.invalid', cwd=path)

    def record(self, path, message):
        publish.git('add', '-A', cwd=path)
        publish.git('commit', '-m', message, cwd=path)
        return self.head(path)

    def head(self, path):
        return publish.git('rev-parse', 'HEAD', cwd=path).stdout.strip()

    def advance_remote(self):
        (self.other / 'remote.txt').write_text('Other publisher\n', encoding='utf-8')
        head = self.record(self.other, 'Other publication')
        publish.git('push', 'origin', 'main', cwd=self.other)
        return head

    def test_worktree_fast_forwards_before_loading_catalog(self):
        head = self.advance_remote()
        self.assertEqual(publish.worktree(), self.local)
        self.assertEqual(self.head(self.local), head)
        self.assertEqual((self.local / 'remote.txt').read_text(encoding='utf-8'), 'Other publisher\n')

    def test_divergence_stops_publication_before_release(self):
        (self.local / 'local.txt').write_text('Pending publication\n', encoding='utf-8')
        local_head = self.record(self.local, 'Pending publication')
        self.advance_remote()
        with patch.object(publish, 'Package') as package, patch.object(publish, 'release_for') as release:
            package.return_value.uid = '12345678-1234-1234-1234-123456789abc'
            with self.assertRaisesRegex(publish.Fail, 'sincronizar o catálogo'):
                publish.publish([self.root / 'package.bl'], dry=False)
            release.assert_not_called()
        self.assertEqual(self.head(self.local), local_head)
        self.assertTrue((self.local / 'local.txt').is_file())
        self.assertFalse((self.local / '.git' / 'MERGE_HEAD').exists())

    def test_fetch_failure_stops_sync(self):
        publish.git('remote', 'set-url', 'origin', str(self.root / 'missing.git'), cwd=self.local)
        with self.assertRaisesRegex(publish.Fail, 'sincronizar o catálogo'):
            publish.worktree()

    def test_retry_pushes_pending_commit_without_new_changes(self):
        (self.local / 'local.txt').write_text('Pending publication\n', encoding='utf-8')
        local_head = self.record(self.local, 'Pending publication')
        self.assertNotEqual(self.head(self.remote), local_head)
        publish.worktree()
        publish.commit(self.local, 'Retry publication', dry=False)
        self.assertEqual(self.head(self.remote), local_head)
        self.assertEqual(self.head(self.local), local_head)

    def test_dry_run_does_not_push_pending_commit(self):
        remote_head = self.head(self.remote)
        (self.local / 'local.txt').write_text('Pending publication\n', encoding='utf-8')
        local_head = self.record(self.local, 'Pending publication')
        publish.commit(self.local, 'Retry publication', dry=True)
        self.assertEqual(self.head(self.remote), remote_head)
        self.assertEqual(self.head(self.local), local_head)

    def test_new_publication_still_commits_and_pushes(self):
        (self.local / 'local.txt').write_text('New publication\n', encoding='utf-8')
        publish.commit(self.local, 'New publication', dry=False)
        self.assertEqual(self.head(self.remote), self.head(self.local))
        self.assertEqual(publish.git('log', '-1', '--format=%s', cwd=self.local).stdout.strip(),
                         'New publication')

    def enable_discord(self):
        discord_updates.CONFIG.write_text('https://discord.com/api/webhooks/123/test-token', encoding='utf-8')

    def catalog_package(self, version='1.0.0'):
        path = self.root / f'package-{version}.bl'
        manifest = {'uid': '12345678-1234-1234-1234-123456789abc', 'id': 'test',
                    'version': version, 'name': 'Test', 'blVersion': 2,
                    'authors': [{'name': 'Author'}]}
        with zipfile.ZipFile(path, 'w') as archive:
            archive.writestr('manifest.json', json.dumps(manifest))
            archive.writestr('icon.png', b'icon')
            archive.writestr('content/main.js', b'// mod')
        return path

    def test_publish_update_noop_and_remove_notify_confirmed_catalog(self):
        self.enable_discord()
        def confirmed(url, body):
            index = json.loads(publish.git('show', 'main:index.json', cwd=self.remote).stdout)
            title = body['embeds'][0]['title']
            if title.startswith('Package removed'):
                self.assertEqual(index['mods'], [])
            else:
                version = next(f['value'] for f in body['embeds'][0]['fields'] if f['name'] == 'Version')
                self.assertEqual(index['mods'][0]['version'], version)
            return {'id': '12345'}
        self.sender.side_effect = confirmed
        with patch.object(publish, 'release_for'):
            path = self.catalog_package()
            publish.publish([path], dry=False)
            self.assertTrue(self.sender.call_args.args[1]['embeds'][0]['title'].startswith('New package published'))
            publish.publish([path], dry=False)
            self.assertEqual(self.sender.call_count, 1)
            publish.publish([self.catalog_package('1.1.0')], dry=False)
            embed = self.sender.call_args.args[1]['embeds'][0]
            self.assertTrue(embed['title'].startswith('Package updated'))
            self.assertIn({'name': 'Previous version', 'value': '1.0.0', 'inline': True}, embed['fields'])
        publish.remove('12345678-1234-1234-1234-123456789abc', dry=False)
        self.assertTrue(self.sender.call_args.args[1]['embeds'][0]['title'].startswith('Package removed'))
        self.assertEqual(self.sender.call_count, 3)

    def test_published_at_is_set_once_and_kept_on_update(self):
        def catalog_entry():
            return json.loads((self.local / 'index.json').read_text(encoding='utf-8'))['mods'][0]
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package()], dry=False)
            first = catalog_entry()['publishedAt']
            self.assertRegex(first, r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$')
            publish.publish([self.catalog_package('1.1.0')], dry=False)
            self.assertEqual(catalog_entry()['publishedAt'], first)

    def test_feature_takes_uid_or_id_and_empty_clears(self):
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package()], dry=False)
        def featured():
            return json.loads((self.local / 'index.json').read_text(encoding='utf-8')).get('featured')
        uid = '12345678-1234-1234-1234-123456789abc'
        publish.feature(['test'], dry=False)
        self.assertEqual(featured(), [uid])
        self.assertTrue((self.local / '.github' / 'workflows' / 'download-stats.yml').is_file())
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package('1.1.0')], dry=False)
        self.assertEqual(featured(), [uid])
        with self.assertRaisesRegex(publish.Fail, 'não está no catálogo'):
            publish.feature(['missing'], dry=False)
        publish.feature([], dry=False)
        self.assertIsNone(featured())

    def test_push_rejected_by_a_stats_commit_rebases_and_goes_through(self):
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package()], dry=False)
        publish.git('pull', 'origin', 'main', cwd=self.other)
        (self.other / 'stats.json').write_text('{"format": 1, "downloads": {}}\n', encoding='utf-8')
        self.record(self.other, 'Downloads')
        publish.git('push', 'origin', 'main', cwd=self.other)
        real_worktree = publish.worktree
        with patch.object(publish, 'worktree', side_effect=lambda: self.local):
            real_worktree()
            publish.git('pull', 'origin', 'main', cwd=self.other)
            (self.other / 'stats.json').write_text('{"format": 1, "downloads": {"a": 1}}\n', encoding='utf-8')
            self.record(self.other, 'Downloads again')
            publish.git('push', 'origin', 'main', cwd=self.other)
            publish.feature(['test'], dry=False)
        publish.git('pull', 'origin', 'main', cwd=self.other)
        self.assertIn('featured', json.loads((self.other / 'index.json').read_text(encoding='utf-8')))
        self.assertIn('"a": 1', (self.other / 'stats.json').read_text(encoding='utf-8'))

    def test_mod_published_before_the_field_stays_without_date(self):
        self.assertIsNone(publish.first_published({'uid': 'old'}))

    def test_failed_push_sends_nothing_and_retry_announces_once(self):
        self.enable_discord()
        real_git = publish.git
        def reject_push(*args, **kwargs):
            if args[0] == 'push':
                return subprocess.CompletedProcess(args, 1, '', 'non-fast-forward')
            return real_git(*args, **kwargs)
        path = self.catalog_package()
        with patch.object(publish, 'release_for'), patch.object(publish, 'git', side_effect=reject_push):
            with self.assertRaisesRegex(publish.Fail, 'push do main falhou'):
                publish.publish([path], dry=False)
        self.sender.assert_not_called()
        with patch.object(publish, 'release_for'):
            publish.publish([path], dry=False)
            publish.publish([path], dry=False)
        self.sender.assert_called_once()
        self.assertEqual(discord_updates.status()['pending'], 0)

    def test_dry_publication_never_sends_or_queues_discord(self):
        self.enable_discord()
        remote_head = self.head(self.remote)
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package()], dry=True)
        self.sender.assert_not_called()
        self.assertFalse(discord_updates.STATE.exists())
        self.assertEqual(self.head(self.remote), remote_head)

    def test_discord_failure_preserves_success_and_can_be_retried(self):
        self.enable_discord()
        self.sender.side_effect = OSError('offline')
        with patch.object(publish, 'release_for'):
            publish.publish([self.catalog_package()], dry=False)
        self.assertEqual(self.head(self.local), self.head(self.remote))
        self.assertEqual(discord_updates.status()['pending'], 1)
        self.sender.side_effect = None
        discord_updates.flush()
        self.assertEqual(discord_updates.status()['pending'], 0)


if __name__ == '__main__':
    unittest.main()

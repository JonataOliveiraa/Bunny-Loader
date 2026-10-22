import json
import tempfile
import unittest
from pathlib import Path

import download_stats

BASE = 'https://github.com/JonataOliveiraa/Bunny-Loader-Mods/releases/download/'


def mod(uid, mod_id, tag):
    return {'uid': uid, 'id': mod_id, 'download': {'url': f'{BASE}{tag}/{mod_id}.bl'}}


def release(tag, *counts, name='x.bl'):
    return {'tag_name': tag, 'assets': [{'name': name, 'download_count': c} for c in counts]}


class CountTests(unittest.TestCase):
    def test_tag_id_keeps_dashes_in_the_id(self):
        self.assertEqual('gravity-dont-flip-screen', download_stats.tag_id('mod-gravity-dont-flip-screen-v1.0.0'))
        self.assertEqual('qualityofbunny', download_stats.tag_id('mod-qualityofbunny-v67'))
        self.assertIsNone(download_stats.tag_id('v0.1.0-dev.20261006'))

    def test_every_version_of_a_mod_adds_up_and_only_bl_counts(self):
        index = {'mods': [mod('a', 'consolaria', 'mod-consolaria-v1.0.1'), mod('b', 'chestpeek', 'mod-chestpeek-v0.1.0')]}
        releases = [
            release('mod-consolaria-v1.0.1', 14),
            release('mod-consolaria-v1.0.0', 23),
            release('mod-consolaria-v1.0.0', 99, name='source.zip'),
            release('mod-other-v1.0.0', 50),
            release('v0.1.0-dev.20261006', 55, name='bunny-loader.apk'),
        ]
        self.assertEqual({'a': 37, 'b': 0}, download_stats.count(index, releases))

    def test_an_id_change_still_counts_the_old_releases_through_the_url(self):
        index = {'mods': [mod('a', 'moddifficulty', 'mod-moddifficulty-v1.0.0') | {'id': 'legendary'}]}
        self.assertEqual({'a': 2}, download_stats.count(index, [release('mod-moddifficulty-v1.0.0', 2)]))


class WriteTests(unittest.TestCase):
    def test_same_numbers_do_not_rewrite_the_file(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self.assertTrue(download_stats.write(root, {'a': 1}))
            first = (root / 'stats.json').read_text(encoding='utf-8')
            self.assertFalse(download_stats.write(root, {'a': 1}))
            self.assertEqual(first, (root / 'stats.json').read_text(encoding='utf-8'))
            self.assertTrue(download_stats.write(root, {'a': 2}))
            self.assertEqual({'a': 2}, json.loads((root / 'stats.json').read_text(encoding='utf-8'))['downloads'])


if __name__ == '__main__':
    unittest.main()

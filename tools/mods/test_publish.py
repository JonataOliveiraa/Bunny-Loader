import json
import tempfile
import unittest
import zipfile
from pathlib import Path

import publish


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


if __name__ == '__main__':
    unittest.main()

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

    def test_store_takes_animated_banner_and_theme_background(self):
        package = self.package(['content/main.js', 'banner.gif', 'docs/bg.gif'])
        self.addCleanup(package.zip.close)
        package.manifest['theme'] = {'background': 'docs/bg.gif'}
        files = package.store_files()
        self.assertIn('banner.gif', files)
        self.assertIn('docs/bg.gif', files)
        package.manifest['theme'] = {'background': '#203040'}
        self.assertNotIn('#203040', package.store_files())

    def test_store_paths_keep_launcher_restrictions(self):
        package = self.package(['content/main.js', 'page (1).md'])
        self.addCleanup(package.zip.close)
        package.manifest['pages'] = [{'file': 'page (1).md'}]
        with self.assertRaisesRegex(publish.Fail, 'vitrine'):
            package.store_files()


if __name__ == '__main__':
    unittest.main()

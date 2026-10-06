"""Valida os formatos e as falhas do downloader sem Steam/rede."""
import hashlib
import struct
import tempfile
import unittest
import zlib
from pathlib import Path
from unittest.mock import patch

import tmod


def string(value):
    data = value.encode("utf-8")
    size = len(data)
    prefix = bytearray()
    while size >= 128:
        prefix.append((size & 127) | 128)
        size >>= 7
    prefix.append(size)
    return bytes(prefix) + data


def package(entries, *, name="Example", old=False):
    body = string(name) + string("1.0") + struct.pack("<i", len(entries))
    payload = b""
    for path, data in entries:
        body += string(path) + struct.pack("<i", len(data))
        if old:
            body += data
        else:
            compressor = zlib.compressobj(wbits=-15)
            compressed = compressor.compress(data) + compressor.flush()
            body += struct.pack("<i", len(compressed))
            payload += compressed
    body += payload
    if old:
        compressor = zlib.compressobj(wbits=-15)
        body = compressor.compress(body) + compressor.flush()
    return (b"TMOD" + string("0.10.1" if old else "2026.1.0") +
            hashlib.sha1(body).digest() + bytes(256) + struct.pack("<i", len(body)) + body)


class DownloadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def write(self, data):
        path = self.root / "Teste.tmod"
        path.write_bytes(data)
        return path

    def test_link_and_numeric_id(self):
        self.assertEqual(tmod.workshop_id("https://steamcommunity.com/sharedfiles/filedetails/?id=3810452158&searchtext=x"), "3810452158")
        self.assertEqual(tmod.workshop_id(" 03810452158 "), "3810452158")

    def test_rejects_bad_links(self):
        for value in ["0", "-1", "abc", "18446744073709551616", "https://evil.com/?id=5",
                      "https://steamcommunity.com@evil.com/sharedfiles/filedetails/?id=5",
                      "https://steamcommunity.com/sharedfiles/filedetails/?id=5&id=6"]:
            with self.subTest(value=value), self.assertRaises(tmod.ToolError):
                tmod.workshop_id(value)

    def test_modern_and_legacy_tables(self):
        for old in [False, True]:
            with self.subTest(old=old):
                name, files = tmod.validate_tmod(self.write(package([
                    ("Content/á.txt", "olá".encode()), ("Textures/Icon.rawimg", b"image")], old=old)))
                self.assertEqual(name, "Example")
                self.assertEqual(files, [("Content/á.txt", 4), ("Textures/Icon.png", None)])

    def test_rejects_corruption_and_truncation(self):
        data = package([("test.txt", b"hello")])
        for damaged in [b"nope", data[:-1], data[:-1] + bytes([data[-1] ^ 1])]:
            with self.subTest(data=damaged[:8]), self.assertRaises(tmod.ToolError):
                tmod.validate_tmod(self.write(damaged))

    def test_rejects_unsafe_members_before_unpacking(self):
        for member in ["../outside", "/absolute", "C:/outside", "x:stream", "folder/../../x",
                       "folder\\..\\x", "folder/NUL.txt", "file. "]:
            with self.subTest(member=member), self.assertRaises(tmod.ToolError):
                tmod.validate_tmod(self.write(package([(member, b"x")])))

    def test_rejects_unsafe_mod_name(self):
        for name in ["../Example", "C:/Example", "CON"]:
            with self.subTest(name=name), self.assertRaises(tmod.ToolError):
                tmod.validate_tmod(self.write(package([("test", b"x")], name=name)))

    def test_rejects_windows_collisions(self):
        for entries in [[("Icon.rawimg", b"x"), ("Icon.png", b"y")],
                        [("Hello.txt", b"x"), ("hello.txt", b"y")]]:
            with self.subTest(entries=entries), self.assertRaises(tmod.ToolError):
                tmod.validate_tmod(self.write(package(entries)))

    def test_does_not_accept_steam_exit_zero_without_confirmation(self):
        with patch.object(tmod, "run", return_value="ERROR! Download item 3810452158 failed"), self.assertRaises(tmod.ToolError):
            tmod.fetch_mod("3810452158", self.root / "steamcmd.exe", self.root, self.root / "log", 20)

    def test_retries_only_successful_steam_bootstrap(self):
        folder = self.root / "workshop/steamapps/workshop/content/1281930/3810452158"
        folder.mkdir(parents=True)
        log = self.root / "steamcmd.log"
        log.write_text("Update complete, launching...")
        bootstrap = tmod.ProcessError(7, log.read_text(), log)
        success = f'Success. Downloaded item 3810452158 to "{folder}"'
        with patch.object(tmod, "run", side_effect=[bootstrap, success]) as runner:
            self.assertEqual(tmod.fetch_mod("3810452158", self.root / "steamcmd.exe", self.root, log, 20), folder)
            self.assertEqual(runner.call_count, 2)
        self.assertTrue((self.root / "steamcmd-bootstrap.log").is_file())

    def test_does_not_retry_other_steam_errors(self):
        error = tmod.ProcessError(7, "ERROR: Cannot connect", self.root / "log")
        with patch.object(tmod, "run", side_effect=error) as runner, self.assertRaises(tmod.ProcessError):
            tmod.fetch_mod("3810452158", self.root / "steamcmd.exe", self.root, self.root / "log", 20)
        self.assertEqual(runner.call_count, 1)

    def test_accepts_only_expected_workshop_folder(self):
        folder = self.root / "workshop/steamapps/workshop/content/1281930/3810452158"
        folder.mkdir(parents=True)
        with patch.object(tmod, "run", return_value=f'Success. Downloaded item 3810452158 to "{folder}"'):
            self.assertEqual(tmod.fetch_mod("3810452158", self.root / "steamcmd.exe", self.root, self.root / "log", 20), folder)
        with patch.object(tmod, "run", return_value=f'Success. Downloaded item 3810452158 to "{self.root}"'), self.assertRaises(tmod.ToolError):
            tmod.fetch_mod("3810452158", self.root / "steamcmd.exe", self.root, self.root / "log", 20)

    def test_extraction_failure_preserves_previous_outputs(self):
        source = self.write(package([("test.txt", b"hello")]))
        previous = self.root / "previous"
        previous.mkdir()
        (previous / "keep.txt").write_text("keep")
        destination = self.root / "new"
        with patch.object(tmod, "run", side_effect=tmod.ToolError("falha")), self.assertRaises(tmod.ToolError):
            tmod.unpack(source, destination, self.root / "unpacker.exe", self.root / "log", 20)
        self.assertFalse(destination.exists())
        self.assertEqual((previous / "keep.txt").read_text(), "keep")

    def test_extraction_checks_every_file_and_preserves_argv(self):
        source = self.write(package([("Content/test.txt", b"hello")]))
        def fake_unpack(command, log, timeout):
            target = Path(command[2]) / "Example/Content/test.txt"
            target.parent.mkdir(parents=True)
            target.write_bytes(b"hello")
            self.assertEqual(command[1], str(source))
            return "ok"
        destination = self.root / "output with spaces"
        with patch.object(tmod, "run", side_effect=fake_unpack):
            tmod.unpack(source, destination, self.root / "unpacker.exe", self.root / "log", 20)
        self.assertEqual((destination / "Example/Content/test.txt").read_bytes(), b"hello")

    def test_does_not_publish_incomplete_extraction(self):
        source = self.write(package([("test.txt", b"hello")]))
        destination = self.root / "incomplete"
        with patch.object(tmod, "run", return_value="ok"), self.assertRaises(tmod.ToolError):
            tmod.unpack(source, destination, self.root / "unpacker.exe", self.root / "log", 20)
        self.assertFalse(destination.exists())

    def steam_item(self, files):
        library = self.root / "Steam"
        folder = library / "steamapps/workshop/content/105600/2760972619"
        for name, data in files.items():
            (folder / name).parent.mkdir(parents=True, exist_ok=True)
            (folder / name).write_bytes(data)
        return library, folder

    def test_finds_terraria_item_in_any_library(self):
        library, folder = self.steam_item({"pack.json": b"{}", "Content/Images/Item_1.png": b"png"})
        empty = self.root / "Other"
        self.assertEqual(tmod.find_steam_item("105600", "2760972619", [empty, library]), folder)
        self.assertIsNone(tmod.find_steam_item("105600", "123", [empty, library]))

    def test_copies_terraria_item(self):
        library, _ = self.steam_item({"pack.json": b"{}", "Content/Images/Item_1.png": b"png"})
        result = self.root / "result"
        with patch.object(tmod, "steam_libraries", return_value=[library]):
            tmod.copy_terraria_item("2760972619", "Frosted Forest", result)
        self.assertEqual((result / "extraido/2760972619/Content/Images/Item_1.png").read_bytes(), b"png")

    def test_terraria_item_not_downloaded_says_how(self):
        with patch.object(tmod, "steam_libraries", return_value=[self.root / "Steam"]), \
                self.assertRaisesRegex(tmod.ToolError, "Inscrever-se"):
            tmod.copy_terraria_item("2760972619", "Frosted Forest", self.root / "result")

    def test_main_routes_terraria_items_without_steamcmd_download(self):
        library, _ = self.steam_item({"pack.json": b"{}"})
        out = self.root / "out"
        with patch.object(tmod, "item_details", return_value=("105600", "Frosted Forest")), \
                patch.object(tmod, "steam_libraries", return_value=[library]), \
                patch.object(tmod, "ensure_unpacker", return_value=self.root / "u.exe"), \
                patch.object(tmod, "ensure_steamcmd", return_value=self.root / "s.exe"), \
                patch.object(tmod, "fetch_mod") as fetch:
            code = tmod.main(["2760972619", "--out", str(out), "--cache", str(self.root / "cache")])
        self.assertEqual(code, 0)
        fetch.assert_not_called()
        self.assertEqual(len(list(out.glob("2760972619/*/extraido/2760972619/pack.json"))), 1)

    def test_main_rejects_items_of_other_games(self):
        with patch.object(tmod, "item_details", return_value=("4000", "A Garry's Mod addon")), \
                patch.object(tmod, "ensure_unpacker", return_value=self.root / "u.exe"), \
                patch.object(tmod, "ensure_steamcmd", return_value=self.root / "s.exe"), \
                patch.object(tmod, "fetch_mod") as fetch:
            code = tmod.main(["123", "--out", str(self.root / "out"), "--cache", str(self.root / "cache")])
        self.assertEqual(code, 1)
        fetch.assert_not_called()


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
import zipfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import csharp_build

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = csharp_build.RUNTIME
STORE = Path(r"C:\Scripts\Bunny Loader Mods C#") / "store"
RELEASES = "https://github.com/JonataOliveiraa/Bunny-Loader-CSharp-Mods/releases/download/"
CROSS_ID = "microsoft.netcore.app.runtime.aot.win-x64.cross.android-arm64"
CROSS_SHA256 = {
    "10.0.14": "5e7e62b6aebbb66e4fc4d86a847ac30f72b013ea9a1d313d29a92217b1c941dc",
}


class AotError(Exception):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def runtime_from_apk(apk: Path) -> Path:
    target = ROOT / "build" / "aot" / "runtime"
    shutil.rmtree(target, ignore_errors=True)
    target.mkdir(parents=True)
    with zipfile.ZipFile(apk) as z:
        for name in z.namelist():
            if name.startswith("assets/dotnet/") and name.count("/") == 2 and                     name.endswith((".dll", "references.txt", "aot-key.txt")):
                (target / name.rsplit("/", 1)[1]).write_bytes(z.read(name))
    if not (target / "aot-key.txt").is_file():
        raise AotError(f"{apk.name} has no assets/dotnet/aot-key.txt: it predates AOT images")
    return target


def read_key() -> str:
    path = RUNTIME / "aot-key.txt"
    if not path.is_file():
        raise AotError(f"{path} is missing: build the app once (gradle :app:syncDotnetAssets)")
    key = path.read_text().strip()
    api = sha256((RUNTIME / "BunnyLoader.dll").read_bytes())[:16]
    if not key.startswith(api + "-"):
        raise AotError(f"{path} does not match BunnyLoader.dll ({api}): build the app again")
    return key


def cross_compiler(version: str) -> Path:
    expected = CROSS_SHA256.get(version)
    if expected is None:
        raise AotError(f"no pinned AOT compiler for Mono {version}: add its SHA-256 to CROSS_SHA256")
    folder = ROOT / "build" / "mono" / version
    pack = folder / "aot-cross.nupkg"
    tool = folder / "aot-cross" / "tools" / "mono-aot-cross.exe"
    if tool.is_file():
        return tool
    if not pack.is_file() or sha256(pack.read_bytes()) != expected:
        folder.mkdir(parents=True, exist_ok=True)
        url = f"https://api.nuget.org/v3-flatcontainer/{CROSS_ID}/{version}/{CROSS_ID}.{version}.nupkg"
        print(f"downloading the AOT compiler {version}")
        with urllib.request.urlopen(url) as r:
            data = r.read()
        if sha256(data) != expected:
            raise AotError(f"AOT compiler {version}: SHA-256 differs from the pinned one")
        pack.write_bytes(data)
    with zipfile.ZipFile(pack) as z:
        for name in z.namelist():
            if name.startswith("tools/"):
                z.extract(name, folder / "aot-cross")
    return tool


def ndk_bin() -> Path:
    roots = [os.environ.get("ANDROID_NDK_HOME"), os.environ.get("ANDROID_NDK_ROOT")]
    sdk = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT") or \
        str(Path(os.environ.get("LOCALAPPDATA", "")) / "Android" / "Sdk")
    ndks = sorted((Path(sdk) / "ndk").glob("*"), key=lambda p: [int(x) if x.isdigit() else 0 for x in p.name.split(".")])
    candidates = [Path(r) for r in roots if r] + list(reversed(ndks))
    for ndk in candidates:
        for host in ("windows-x86_64", "linux-x86_64", "darwin-x86_64"):
            b = ndk / "toolchains" / "llvm" / "prebuilt" / host / "bin"
            if (b / "ld.lld").exists() or (b / "ld.lld.exe").exists():
                return b
    raise AotError("Android NDK not found (set ANDROID_NDK_HOME)")


def tool(bin_dir: Path, name: str) -> str:
    exe = bin_dir / (name + ".exe")
    return str(exe if exe.exists() else bin_dir / name)


def store_packages(store: Path, cache: Path) -> list[Path]:
    index = json.loads((store / "index.json").read_text(encoding="utf-8"))
    out = []
    cache.mkdir(parents=True, exist_ok=True)
    for mod in index.get("mods", []):
        download = mod.get("download") or {}
        url, expected = download.get("url", ""), download.get("sha256", "").lower()
        if not url.startswith(RELEASES) or len(expected) != 64:
            continue
        target = cache / f"{mod['uid']}-{mod.get('version', '0')}.bl"
        if not target.is_file() or sha256(target.read_bytes()) != expected:
            print(f"downloading {url.rsplit('/', 1)[-1]}")
            with urllib.request.urlopen(url) as r:
                data = r.read()
            if sha256(data) != expected:
                print(f"  skipped: sha256 differs from the index")
                continue
            target.write_bytes(data)
        out.append(target)
    return out


def manifest_id(names: dict[str, bytes]) -> str | None:
    raw = names.get("manifest.json")
    if raw is None:
        return None
    try:
        m = json.loads(raw.decode("utf-8-sig"))
    except ValueError:
        return None
    return m.get("id") if isinstance(m.get("id"), str) else None


def mod_dll(path: Path) -> tuple[str, bytes] | None:
    if path.is_dir():
        files = {"manifest.json": (path / "manifest.json").read_bytes()} if (path / "manifest.json").is_file() else {}
    else:
        with zipfile.ZipFile(path) as z:
            files = {"manifest.json": z.read("manifest.json")} if "manifest.json" in z.namelist() else {}
    mid = manifest_id(files)
    if not mid:
        return None
    if path.is_dir():
        dll, source = path / f"{mid}.dll", path / f"{mid}.cs"
        if dll.is_file():
            return mid, dll.read_bytes()
        if not source.is_file():
            return None
        return mid, compile_source(path, mid)
    with zipfile.ZipFile(path) as z:
        names = set(z.namelist())
        if f"{mid}.dll" in names:
            return mid, z.read(f"{mid}.dll")
        if f"{mid}.cs" not in names:
            return None
        folder = Path(tempfile.mkdtemp(prefix="blaot-"))
        try:
            root = folder.resolve()
            for name in names:
                if not (root / name).resolve().is_relative_to(root):
                    raise AotError(f"{path.name}: {name!r} leaves the package")
            z.extractall(folder)
            return mid, compile_source(folder, mid)
        finally:
            shutil.rmtree(folder, ignore_errors=True)


def compile_source(folder: Path, mid: str) -> bytes:
    dll = csharp_build.tool()
    r = subprocess.run(["dotnet", str(dll), str(folder / f"{mid}.cs"), str(RUNTIME)],
                       capture_output=True, text=True, encoding="utf-8")
    report = csharp_build.parse_report(r.stdout)
    if report["status"] == "failed":
        raise AotError(f"{mid}: the C# did not compile: " + "; ".join(report["errors"][:3]))
    out = folder / "bin" / f"{mid}.dll"
    if not out.is_file():
        raise AotError(f"{mid}: the compiler wrote no bin/{mid}.dll")
    return out.read_bytes()


def aot_one(name: str, dll: Path, refs: str, cross: Path, bin_dir: Path, work: Path) -> Path:
    folder = work / name
    folder.mkdir(parents=True, exist_ok=True)
    asm, obj, so = folder / f"{name}.s", folder / f"{name}.o", folder / f"{name}.dll.so"
    env = dict(os.environ, MONO_THREADS_SUSPEND="coop", MONO_PATH=refs)
    opts = f"asmonly,static,outfile={asm},mtriple=aarch64-linux-android,temp-path={folder}"
    r = subprocess.run([str(cross), f"--aot={opts}", str(dll)], env=env, capture_output=True, text=True)
    if r.returncode != 0 or not asm.is_file():
        raise AotError(f"{name}: mono-aot-cross failed: {(r.stdout + r.stderr)[-600:]}")
    subprocess.run([tool(bin_dir, "clang"), "--target=aarch64-linux-android24", "-c", str(asm), "-o", str(obj)], check=True)
    subprocess.run([tool(bin_dir, "ld.lld"), "-shared", "--no-relax", "--build-id", "-z", "noexecstack",
                    "-o", str(so), str(obj)], check=True)
    subprocess.run([tool(bin_dir, "llvm-strip"), "--strip-debug", str(so)], check=True)
    asm.unlink(missing_ok=True)
    obj.unlink(missing_ok=True)
    return so


def catalog_tool(store: Path):
    tools = store.resolve().parent / "tools"
    if not (tools / "publish.py").is_file():
        raise AotError(f"no publish.py next to {store}: point --store at the catalog clone")
    sys.path.insert(0, str(tools))
    import publish
    if not os.environ.get("BL_GITHUB_TOKEN"):
        token_file = ROOT / "tokenmodscsharp.txt"
        if token_file.is_file():
            os.environ["BL_GITHUB_TOKEN"] = token_file.read_text(encoding="utf-8").strip()
    return publish


def publish_images(out: Path, key: str, store: Path, dry: bool) -> None:
    pub = catalog_tool(store)
    manifest = json.loads((out / f"{key}.json").read_text(encoding="utf-8"))
    tag = f"aot-{key}"
    try:
        pub.prepare_repo(dry)
        if dry:
            print(f"  [dry-run] would upload {len(manifest['images'])} image(s) to Release {tag}")
        else:
            release = pub.gh("GET", f"{pub.API}/releases/tags/{tag}")
            if not release:
                release = pub.gh("POST", f"{pub.API}/releases", {
                    "tag_name": tag,
                    "target_commitish": pub.git_text("rev-parse", f"origin/{pub.BRANCH}"),
                    "name": f"AOT images {key}",
                    "body": (f"Precompiled mod code for Bunny Loader builds whose AOT key is `{key}`.\n\n"
                             "The app downloads these by itself; there is nothing to install by hand."),
                    "make_latest": "false",
                })
            existing = {a["name"]: a for a in release.get("assets", [])}
            for image in manifest["images"]:
                name = image["url"].rsplit("/", 1)[1]
                asset = existing.get(name)
                if asset:
                    digest = (asset.get("digest") or "").removeprefix("sha256:")
                    if not digest:
                        with urllib.request.urlopen(asset["browser_download_url"], timeout=600) as r:
                            digest = sha256(r.read())
                    image["sha256"], image["size"] = digest, asset["size"]
                    print(f"  {name}: already on the Release")
                    continue
                q = urllib.parse.urlencode({"name": name})
                pub.gh("POST", f"{pub.UPLOADS}/releases/{release['id']}/assets?{q}",
                       raw=(out / name).read_bytes(), content_type="application/gzip")
                print(f"  {name}: uploaded")
        target = store / "aot" / f"{key}.json"
        if dry:
            print(f"  [dry-run] would write {target}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8")
        pub.commit_and_push(f"AOT images for {key}", dry)
    except pub.Fail as e:
        raise AotError(str(e))


def main() -> int:
    parser = argparse.ArgumentParser(description="Builds AOT images of the catalog mods for the current app build.")
    parser.add_argument("--store", type=Path, default=STORE, help="store/ folder of the catalog clone")
    parser.add_argument("--pack", type=Path, action="append", default=[], help="extra .bl or package folder")
    parser.add_argument("--no-store", action="store_true", help="only the --pack packages")
    parser.add_argument("--jobs", type=int, default=3)
    parser.add_argument("--out", type=Path, help="output folder (default build/aot/<key>)")
    parser.add_argument("--apk", type=Path, help="take BunnyLoader.dll and the base library from this APK")
    parser.add_argument("--publish", action="store_true", help="upload the images and store/aot/<key>.json")
    parser.add_argument("--reuse", action="store_true", help="publish images already built for this key")
    parser.add_argument("--dry-run", action="store_true", help="with --publish: show what would be sent")
    args = parser.parse_args()
    if args.publish and (args.pack or args.no_store):
        print("error: --publish only sends catalog mods; drop --pack and --no-store")
        return 1

    global RUNTIME
    try:
        if args.apk:
            RUNTIME = runtime_from_apk(args.apk)
        key = read_key()
        version = key.split("-", 1)[1]
        cross = cross_compiler(version)
        bin_dir = ndk_bin()
        out = args.out or ROOT / "build" / "aot" / key
        if args.reuse:
            if not (out / f"{key}.json").is_file():
                raise AotError(f"nothing built for {key} in {out}")
            if args.publish:
                publish_images(out, key, args.store, args.dry_run)
            return 0
        work = ROOT / "build" / "aot" / "work"
        dlls = ROOT / "build" / "aot" / "dlls"
        for d in (work, dlls):
            shutil.rmtree(d, ignore_errors=True)
            d.mkdir(parents=True)
        out.mkdir(parents=True, exist_ok=True)

        packages = [] if args.no_store else store_packages(args.store, ROOT / "build" / "aot" / "packages")
        packages += args.pack
        mods: dict[str, Path] = {}
        for path in packages:
            try:
                found = mod_dll(path)
            except (AotError, zipfile.BadZipFile, OSError) as e:
                print(f"skipped {path.name}: {e}")
                continue
            if found is None:
                continue
            name, data = found
            target = dlls / f"{name}.dll"
            target.write_bytes(data)
            mods[name] = target
        print(f"{len(mods)} mod assembly(ies) for {key}")

        refs = os.pathsep.join([str(RUNTIME), str(dlls)])
        images = []

        def build(name: str):
            so = aot_one(name, mods[name], refs, cross, bin_dir, work)
            dll_sha = sha256(mods[name].read_bytes())
            packed = gzip.compress(so.read_bytes(), 9, mtime=0)
            file = f"{name}-{dll_sha[:12]}.dll.so.gz"
            (out / file).write_bytes(packed)
            return {"assembly": name, "dll": dll_sha, "url": f"{RELEASES}aot-{key}/{file}",
                    "sha256": sha256(packed), "size": len(packed)}, so.stat().st_size

        failed = 0
        with ThreadPoolExecutor(max(1, args.jobs)) as pool:
            futures = {pool.submit(build, n): n for n in sorted(mods)}
            for future, name in futures.items():
                try:
                    image, raw = future.result()
                    images.append(image)
                    print(f"  {name}: {raw // 1024} KB, {image['size'] // 1024} KB packed")
                except (AotError, subprocess.CalledProcessError) as e:
                    failed += 1
                    print(f"  {name}: FAILED {e}")
        manifest = {"format": 1, "key": key, "images": sorted(images, key=lambda i: i["assembly"])}
        (out / f"{key}.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8")
        shutil.rmtree(work, ignore_errors=True)
        total = sum(i["size"] for i in images)
        print(f"{len(images)} image(s), {total / 1048576:.1f} MB packed, {failed} failed -> {out}")
        if args.publish:
            publish_images(out, key, args.store, args.dry_run)
        return 1 if failed else 0
    except AotError as e:
        print(f"error: {e}")
        return 1


if __name__ == "__main__":
    sys.exit(main())

"""Verify downloaded dual-edition release hashes and portable package contracts.

Usage: python scripts/verify-release.py artifacts/release-v1.11.1 1.11.1
Does not execute Windows binaries or installers. Actual lifecycle tests run in CI.
"""
import hashlib
import json
from pathlib import Path
import re
import struct
import sys
import zipfile

root = Path(sys.argv[1])
version = sys.argv[2]
assert re.fullmatch(r"\d+\.\d+\.\d+", version)
products = {"Win-XinAi-De-Tools": ["x86", "x64", "arm64"], "Win-XinAi-De-Tools-MyGo": ["x64", "arm64"]}
expected = {f"{product}-{kind}{arch}.{extension}" for product, arches in products.items()
            for arch in arches for kind, extension in [("win-", "zip"), ("Setup-", "exe")]}
checksums = {}
for line in (root / "SHA256SUMS.txt").read_text(encoding="utf-8-sig").splitlines():
    match = re.fullmatch(r"([a-f0-9]{64})  ([A-Za-z0-9_.-]+)", line)
    assert match, f"Invalid checksum line: {line}"
    digest, name = match.groups()
    assert name not in checksums, f"Duplicate checksum: {name}"
    checksums[name] = digest
assert set(checksums) == expected, "Unexpected or missing release package names"
for name, expected_hash in checksums.items():
    path = root / name
    assert path.stat().st_size > 0
    with path.open("rb") as file:
        actual = hashlib.file_digest(file, "sha256").hexdigest()
    assert actual == expected_hash, f"Hash mismatch: {name}"
    print(f"SHA256 OK  {name}  {actual}")


def check_pe(data, arch):
    assert data[:2] == b"MZ"
    offset = struct.unpack_from("<I", data, 0x3C)[0]
    assert data[offset:offset + 4] == b"PE\0\0"
    assert struct.unpack_from("<H", data, offset + 4)[0] == {"x86": 0x14C, "x64": 0x8664, "arm64": 0xAA64}[arch]
    assert b"requireAdministrator" in data, "Required-administrator manifest missing"


for product, arches in products.items():
    for arch in arches:
        with zipfile.ZipFile(root / f"{product}-win-{arch}.zip") as archive:
            assert archive.testzip() is None, "ZIP CRC failure"
            names = set(archive.namelist())
            assert all(not name.startswith(("/", "\\")) and ".." not in Path(name).parts for name in names)
            check_pe(archive.read(product + ".exe"), arch)
            for name in ["LICENSE", "THIRD-PARTY-NOTICES.md", "NETWORK-RESET.md", "HYPER-V.md", "INSTALLATION.md"]:
                assert name in names, f"Missing package documentation: {name}"
            if product.endswith("-MyGo"):
                engine = "engine/"
                check_pe(archive.read(engine + "Win-XinAi-De-Tools.Engine.exe"), arch)
                frontend = archive.read(product + ".exe")
                assert version.encode("utf-16le") in frontend, "Missing native version resource"
                assert b"go1.27.2" in frontend, "Unexpected Go toolchain"
                assert b"mygo_noinspector" in frontend, "Native inspector was not excluded"
                assert "com.logdns.winxinai.mygo".encode("utf-16le") in frontend, "SDK package identifier resource missing"
                assert b"e529e1a86f63" in frontend, "Unexpected MyGo version"
                assert "Assets/Square150x150Logo.png" in names
                modules = json.loads(archive.read("licenses/go/modules.json"))
                assert any(module["path"] == "github.com/egoist/mygo" for module in modules)
                for module in modules:
                    prefix = "licenses/go/" + module["path"].replace("/", "_") + "@" + module["version"] + "/"
                    for license_file in module["licenses"]:
                        assert len(archive.read(prefix + license_file)) > 0
                assert "licenses/go/Go-LICENSE" in names
                assert "README.md" in names
            else:
                engine = ""
                for language in ["en-US", "zh-CN"]:
                    text = archive.read(f"Localization/Strings.{language}.xaml").decode("utf-8-sig")
                    assert version in text and "NetworkReset_Title" in text
                assert any("Microsoft.UI.Xaml" in name for name in names)
                assert any("libSkiaSharp" in name for name in names)
            for runtime in ["coreclr.dll", "hostfxr.dll", "Microsoft.AspNetCore.Server.Kestrel.Core.dll"]:
                assert engine + runtime in names, f"Missing self-contained runtime: {engine + runtime}"
            runtime_config = json.loads(archive.read(engine + ("Win-XinAi-De-Tools.Engine" if engine else product) + ".runtimeconfig.json"))
            assert "includedFrameworks" in runtime_config["runtimeOptions"], "Package is not self-contained"
            print(f"PORTABLE OK  {product} {arch}: CRC, native PE/manifest, version, runtime, docs and licenses")
print(f"RELEASE PACKAGES OK  v{version}: all {len(checksums)} downloaded hashes match; installers are not executed by this check")

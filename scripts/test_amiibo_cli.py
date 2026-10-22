"""Check amiibo CLI writes against independently rebuilt expected save bytes."""

import argparse
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import zlib

from test_cli import cli_command, main_fixture, wide

OUTFITS = ["英雄王の衣装", "慈愛の王女の衣装", "聖騎士の衣装", "賢王の衣装",
           "若き獅子の衣装", "草原の公女の衣装", "碧き風空の衣装", "蒼炎の勇者の衣装",
           "暁の巫女の衣装", "聖王女の衣装", "未来を選びし者の衣装", "導き手の衣装"]
KEYS = ["G_所持_AID_" + name for name in OUTFITS] + [f"G_BGM_Amiibo_{i:02}_ST_Play" for i in range(3, 15)]


def expected_unlock(original, keys):
    result = bytearray(original)
    offsets = [value for value in struct.unpack_from("<32I", original, 132) if value]
    user = next(offset for offset in offsets if original[offset:offset + 4] == b"RESU")
    start = user + 57
    length, count = struct.unpack_from("<I", original, start)[0], struct.unpack_from("<I", original, start + 36)[0]
    position, existing = start + 40, {}
    for _ in range(count):
        size = struct.unpack_from("<I", original, position)[0]
        key = original[position + 4:position + 4 + size].decode("utf-16-le")
        position += 4 + size
        kind = original[position]
        position += 1
        existing[key] = (kind, position)
        position += 4 if kind == 0 else 4 + struct.unpack_from("<I", original, position)[0]
    assert position == start + length
    addition, added = b"", 0
    for key in dict.fromkeys(keys):
        if key in existing:
            kind, offset = existing[key]
            assert kind == 0
            struct.pack_into("<i", result, offset, 1)
        else:
            addition += wide(key) + b"\x00" + struct.pack("<i", 1)
            added += 1
    if addition:
        result[position:position] = addition
        struct.pack_into("<I", result, start, length + len(addition))
        struct.pack_into("<I", result, start + 36, count + added)
        struct.pack_into("<I", result, user + 4, struct.unpack_from("<I", original, user + 4)[0] + len(addition))
        for index in range(32):
            slot = 132 + index * 4
            offset = struct.unpack_from("<I", original, slot)[0]
            if offset > user:
                struct.pack_into("<I", result, slot, offset + len(addition))
    struct.pack_into("<I", result, len(result) - 4, zlib.crc32(result[:-4]))
    return bytes(result)


def check_amiibo(command, real_directory, fixture):
    def run(*args, valid=True):
        result = subprocess.run([*command, "amiibo", *map(str, args)], capture_output=True, text=True, encoding="utf-8")
        assert (result.returncode == 0) == valid, (args, result.stderr)
        return result.stdout

    cases = [("missing", fixture)]
    if real_directory:
        cases += [(path.name, path.read_bytes()) for path in sorted(real_directory.glob("Manual*"))]
    with tempfile.TemporaryDirectory(prefix="fee-amiibo-cli-") as directory:
        root = Path(directory)
        for name, original in cases:
            source, output = root / "source", root / "output"
            source.write_bytes(original)
            listing = json.loads(run("list", source, "--json"))
            assert [row["Key"] for row in listing["Rewards"]] == KEYS
            for options, keys in [(["--all"], KEYS), (["--group", "outfits"], KEYS[:12]),
                                  (["--group", "music"], KEYS[12:]), (["--key", KEYS[0], "--key", KEYS[-1]], [KEYS[0], KEYS[-1]])]:
                run("unlock", source, output, *options)
                assert output.read_bytes() == expected_unlock(original, keys), f"{name}: unrelated bytes changed"
                second = root / "second"
                run("unlock", output, second, *options)
                assert second.read_bytes() == output.read_bytes(), "Repeated unlock changed bytes"
                second.unlink()
                output.unlink()
            for options in [[], ["--group", "unknown"], ["--key", "G_Amiibo_scan_count"],
                            ["--key", KEYS[0], "--key", "G_Amiibo_scan_count"], ["--all", "--key", KEYS[0]]]:
                run("unlock", source, output, *options, valid=False)
                assert not output.exists()
            run("unlock", source, source, "--all", valid=False)
            assert source.read_bytes() == original
            print(f"{name}: amiibo CLI selection/groups, exact byte diffs, idempotency and input/source guards passed.")
        for language in ("en", "zh-Hans", "zh-Hant", "ja", "ko", "de", "fr", "es", "it"):
            assert all(row["Name"] for row in json.loads(run("list", source, "--language", language))["Rewards"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", required=True, type=Path)
    parser.add_argument("--save-directory", type=Path)
    args = parser.parse_args()
    check_amiibo(cli_command(args.cli), args.save_directory, main_fixture())

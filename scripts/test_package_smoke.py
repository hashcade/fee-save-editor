"""Representative checks against the actual extracted, trimmed CLI executable."""

import json
import subprocess
import tempfile
from pathlib import Path

from test_cli import main_fixture
from test_amiibo_cli import expected_unlock


def check_package_cli(cli, version):
    def run(*args):
        return subprocess.run([str(cli), *map(str, args)], check=True, capture_output=True,
                              text=True, encoding="utf-8", timeout=30).stdout

    if run("--version").strip() != version:
        raise ValueError("The packaged CLI version does not match VERSION.")
    run("--help")
    with tempfile.TemporaryDirectory(prefix="fee-package-smoke-") as directory:
        root = Path(directory)
        source, copy, edited, restored, amiibo = (root / name for name in ("source", "copy", "edited", "restored", "amiibo"))
        original = main_fixture()
        source.write_bytes(original)
        json.loads(run("inspect", source, "--json"))
        run("copy", source, copy)
        if copy.read_bytes() != original:
            raise ValueError("The packaged CLI copy is not byte-exact.")
        before = json.loads(run("main", "show", source, "--json"))
        run("main", "set", source, edited, "--money", "12345")
        if json.loads(run("main", "show", edited, "--json")) != before | {"Money": 12345}:
            raise ValueError("The packaged CLI edited unrelated Main fields.")
        run("main", "set", edited, restored, "--money", str(before["Money"]))
        if restored.read_bytes() != original:
            raise ValueError("The packaged CLI edit is not exactly reversible.")
        keys = ["G_所持_AID_英雄王の衣装", "G_BGM_Amiibo_14_ST_Play"]
        run("amiibo", "unlock", source, amiibo, "--key", keys[0], "--key", keys[1])
        if amiibo.read_bytes() != expected_unlock(original, keys):
            raise ValueError("The packaged CLI variable insertion changed unrelated bytes.")
        if source.read_bytes() != original:
            raise ValueError("The packaged CLI overwrote its input.")
    print("Packaged CLI smoke: version, startup, copy, reversible editing, Unicode insertion and checksums passed.")


def check_gui_startup(gui):
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen([str(gui)], stdout=log, stderr=log)
        try:
            try:
                code = process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                print("Packaged GUI startup: process remained running without an early exit.")
                return
            log.seek(0)
            detail = log.read().decode("utf-8", errors="replace")[-3000:]
            raise ValueError(f"The packaged GUI exited early ({code}): {detail}")
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)

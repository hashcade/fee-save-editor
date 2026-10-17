#!/usr/bin/env python3
"""Check release versioning and the CI gate without Git or network writes."""

import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import release


class ReleaseTests(unittest.TestCase):
    def test_minor_version(self):
        self.assertEqual(release.bump_version((0, 2, 0), "minor"), (0, 3, 0))

    def test_requires_completed_success(self):
        for builds in ([], [{"status": "in_progress", "conclusion": ""}],
                       [{"status": "completed", "conclusion": "failure"}],
                       [{"status": "completed", "conclusion": "cancelled"}]):
            with self.subTest(builds=builds), patch.object(release, "output", return_value=json.dumps(builds)):
                with self.assertRaises(ValueError):
                    release.check_build("commit")
        with patch.object(release, "output", return_value='[{"status":"completed","conclusion":"success"}]') as output:
            release.check_build("commit")
            self.assertIn("commit", output.call_args.args)

    def test_release_uses_tag_workflow_without_repeating_host_tests(self):
        with tempfile.TemporaryDirectory(prefix="fee-release-test-") as directory:
            version = Path(directory) / "VERSION"
            version.write_text("0.2.0\n", encoding="utf-8")
            commands = []

            def command(*args, **kwargs):
                commands.append(args)
                return ""

            def output(*args):
                if args == ("git", "rev-parse", "HEAD"):
                    return "commit"
                if args == ("git", "diff", "--cached", "--name-only"):
                    return "VERSION"
                raise AssertionError(args)

            with patch.object(release, "VERSION_PATH", version), \
                    patch.object(release, "check_repository"), patch.object(release, "check_new_tag"), \
                    patch.object(release, "check_build") as check_build, \
                    patch.object(release, "command", side_effect=command), patch.object(release, "output", side_effect=output), \
                    patch("sys.argv", ["release.py", "minor", "--yes"]), contextlib.redirect_stdout(io.StringIO()):
                release.main()
            check_build.assert_not_called()
            self.assertEqual(version.read_text(encoding="utf-8"), "0.3.0\n")
            self.assertEqual(commands, [
                ("git", "add", "VERSION"), ("git", "commit", "-m", "chore: release v0.3.0"),
                ("git", "push", "origin", "main"), ("git", "tag", "-a", "v0.3.0", "-m", "v0.3.0"),
                ("git", "push", "origin", "v0.3.0"),
            ])

    def test_existing_tag_does_not_change_version_or_publish(self):
        with patch.object(release, "check_repository"), patch.object(release, "read_version", return_value=(0, 2, 0)), \
                patch.object(release, "check_new_tag", side_effect=ValueError("tag already exists")), \
                patch.object(release, "command") as command, patch.object(release, "VERSION_PATH") as version, \
                patch("sys.argv", ["release.py", "minor", "--yes"]), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(ValueError):
                release.main()
            command.assert_not_called()
            version.write_text.assert_not_called()


if __name__ == "__main__":
    unittest.main()

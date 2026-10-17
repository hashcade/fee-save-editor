#!/usr/bin/env python3
"""Check release versioning and the CI gate without Git or network writes."""

import contextlib
import io
import json
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch

import release


class ReleaseTests(unittest.TestCase):
    def test_retry_rejects_existing_release_or_api_failure(self):
        for result in (subprocess.CompletedProcess([], 0, '{"tag_name":"v0.3.0"}'),
                       subprocess.CompletedProcess([], 1, '{"status":"401"}'),
                       subprocess.CompletedProcess([], 1, 'network error')):
            with self.subTest(result=result), patch.object(release, "output", return_value="owner/repo"), \
                    patch.object(release.subprocess, "run", return_value=result), patch.object(release, "command") as command:
                with self.assertRaises(ValueError):
                    release.check_unpublished_tag("v0.3.0")
                command.assert_not_called()

    def test_retry_checks_remote_tag_and_ancestry(self):
        responses = ["owner/repo", "old-tag", "old-tag\trefs/tags/v0.3.0", "old-commit", "new-commit"]
        with patch.object(release, "output", side_effect=responses), \
                patch.object(release.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, '{"status":"404"}')), \
                patch.object(release, "command") as command:
            self.assertEqual(release.check_unpublished_tag("v0.3.0"), "old-tag")
            command.assert_called_once_with("git", "merge-base", "--is-ancestor", "v0.3.0^{commit}", "HEAD")

    def test_retry_uses_exact_lease_and_restores_local_tag_on_failure(self):
        failure = subprocess.CalledProcessError(1, "push")
        with patch.object(release, "command", side_effect=["", failure, ""]) as command:
            with self.assertRaises(subprocess.CalledProcessError):
                release.retry_unpublished_tag("v0.3.0", "old-tag")
            self.assertEqual([call.args for call in command.call_args_list], [
                ("git", "tag", "--force", "-a", "v0.3.0", "-m", "v0.3.0"),
                ("git", "push", "--force-with-lease=refs/tags/v0.3.0:old-tag", "origin", "refs/tags/v0.3.0"),
                ("git", "update-ref", "refs/tags/v0.3.0", "old-tag"),
            ])

    def test_retry_does_not_bump_version(self):
        with patch.object(release, "check_repository"), patch.object(release, "read_version", return_value=(0, 3, 0)), \
                patch.object(release, "check_unpublished_tag", return_value="old-tag"), \
                patch.object(release, "output", return_value="new-commit"), patch.object(release, "VERSION_PATH") as version, \
                patch.object(release, "retry_unpublished_tag") as retry, patch.object(release, "push_tag") as push, \
                patch("sys.argv", ["release.py", "--retry-unpublished", "--yes"]), contextlib.redirect_stdout(io.StringIO()):
            release.main()
            retry.assert_called_once_with("v0.3.0", "old-tag")
            version.write_text.assert_not_called()
            push.assert_not_called()

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

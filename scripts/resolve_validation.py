#!/usr/bin/env python3
"""Reuse a successful main-branch build only for a VERSION-only release commit."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def output(*args):
    return subprocess.run(args, cwd=ROOT, check=True, capture_output=True, text=True, timeout=30).stdout.strip()


def version_only_parent():
    commits = output("git", "rev-list", "--parents", "-n", "1", "HEAD").split()
    if len(commits) != 2 or not all(re.fullmatch(r"[0-9a-f]{40}", commit) for commit in commits):
        return None
    head, parent = commits
    if output("git", "diff", "--name-only", parent, head).splitlines() != ["VERSION"]:
        return None
    return parent


def build_runs(repository, commit):
    endpoint = f"repos/{repository}/actions/workflows/build.yml/runs?event=push&branch=main&head_sha={commit}&per_page=20"
    return json.loads(output("gh", "api", endpoint))["workflow_runs"]


def reusable_build(repository, commit, wait_seconds):
    deadline = time.monotonic() + wait_seconds
    while True:
        runs = [run for run in build_runs(repository, commit)
                if run["head_sha"] == commit and run["event"] == "push" and run["head_branch"] == "main"
                and run["path"] == ".github/workflows/build.yml"
                and run["head_repository"]["full_name"].casefold() == repository.casefold()]
        if not runs:
            return None
        latest = max(runs, key=lambda run: (run["run_number"], run["run_attempt"], run["id"]))
        if latest["status"] == "completed":
            return latest["html_url"] if latest["conclusion"] == "success" else None
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return None
        print(f"Waiting for the existing main build: {latest['html_url']}", flush=True)
        time.sleep(min(30, remaining))


def resolve(wait_seconds):
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        return None
    try:
        parent = version_only_parent()
        return reusable_build(repository, parent, wait_seconds) if parent else None
    except (subprocess.SubprocessError, OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        print(f"Cannot safely reuse validation ({type(error).__name__}); complete validation will run.")
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--wait-seconds", type=int, default=0)
    args = parser.parse_args()
    if args.wait_seconds < 0:
        parser.error("--wait-seconds must be nonnegative")
    url = resolve(args.wait_seconds)
    result = f"reuse={'true' if url else 'false'}\n"
    if path := os.environ.get("GITHUB_OUTPUT"):
        with Path(path).open("a", encoding="utf-8") as destination:
            destination.write(result)
    print(result.strip())
    message = f"Complete validation reused from {url}." if url else "Complete validation is required for this release."
    print(message)
    if path := os.environ.get("GITHUB_STEP_SUMMARY"):
        with Path(path).open("a", encoding="utf-8") as destination:
            destination.write(message + "\n")


if __name__ == "__main__":
    main()

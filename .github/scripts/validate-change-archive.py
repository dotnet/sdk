#!/usr/bin/env python3
"""Validate and extract a change artifact produced by an untrusted 'generate' job.

The *-on-comment workflows split work into a `generate` job that builds and runs
untrusted pull-request code (producing a .tar.gz of changed files) and an
`apply` job that commits those files with write permission. The archive and any
file list it ships must therefore be treated as hostile.

It:
  * opens the archive with Python's tarfile ``data`` extraction filter
    (https://docs.python.org/3/library/tarfile.html#tarfile.data_filter), which
    blocks absolute paths, parent-directory traversal, symlink/hardlink escapes
    and device/FIFO members;
  * additionally rejects any member under a ``.git`` directory or named
    ``.gitattributes``, any non regular-file/dir member, and any file whose name
    does not end with one of the explicitly allowed ``--ext`` suffixes (and, when
    ``--scope`` is given, any file outside that directory);
  * extracts into a private scratch directory and copies only the validated
    files into the destination working tree, preserving repository-relative
    paths and refusing to write outside the destination;
  * prints the NUL-separated list of staged repository-relative paths to stdout
    so the caller can ``git add`` exactly those paths.

Exit code is 0 on success (at least one file staged) and non-zero on any
validation failure or when no matching file is present.
"""
from __future__ import annotations

import argparse
import os
import shutil
import sys
import tarfile
import tempfile
from typing import NoReturn

# Upper bounds on what a legitimate generated change can contain
MAX_MEMBER_SIZE = 25 * 1024 * 1024  # 25 MiB per regular file
MAX_TOTAL_SIZE = 200 * 1024 * 1024  # 200 MiB across all regular files
MAX_MEMBER_COUNT = 5000  # total members (files + directories)


def fail(message: str) -> NoReturn:
    print(message, file=sys.stderr)
    raise SystemExit(1)


def is_git_path(name: str) -> bool:
    return ".git" in name.split("/") or name.endswith(".gitattributes")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate and extract an untrusted change archive into a working tree."
    )
    parser.add_argument("--archive", required=True, help="Path to the .tar.gz artifact to validate.")
    parser.add_argument("--dest", required=True, help="Destination working tree to copy validated files into.")
    parser.add_argument(
        "--ext",
        required=True,
        action="append",
        dest="exts",
        metavar="SUFFIX",
        help="Allowed file-name suffix (repeatable), e.g. --ext .xlf --ext .verified.sh",
    )
    parser.add_argument(
        "--scope",
        default="",
        help="Optional repository-relative directory prefix every file member must be under.",
    )
    args = parser.parse_args()

    exts = tuple(args.exts)
    scope = args.scope.strip("/")
    dest_root = os.path.realpath(args.dest)

    if not os.path.isfile(args.archive):
        fail(f"Archive not found: {args.archive}")

    scratch = tempfile.mkdtemp(prefix="__extract_")
    staged: list[str] = []
    try:
        with tarfile.open(args.archive) as tar:
            members = tar.getmembers()
            if len(members) > MAX_MEMBER_COUNT:
                fail(f"Archive has too many members ({len(members)} > {MAX_MEMBER_COUNT}).")
            total_size = 0
            for member in members:
                if not (member.isfile() or member.isdir()):
                    fail(f"Disallowed member type in archive: {member.name}")
                if "\\" in member.name:
                    fail(f"Backslash path separator in archive member name: {member.name}")
                if member.name.startswith("/"):
                    fail(f"Absolute path in archive: {member.name}")
                if ".." in member.name.split("/"):
                    fail(f"Path traversal in archive: {member.name}")
                if is_git_path(member.name):
                    fail(f".git path in archive: {member.name}")
                if member.isfile():
                    if scope and not (member.name == scope or member.name.startswith(scope + "/")):
                        fail(f"Member outside allowed scope '{scope}': {member.name}")
                    if not member.name.endswith(exts):
                        fail(f"Member with disallowed extension: {member.name}")
                    if member.size > MAX_MEMBER_SIZE:
                        fail(f"Member exceeds maximum size ({member.size} > {MAX_MEMBER_SIZE}): {member.name}")
                    total_size += member.size
                    if total_size > MAX_TOTAL_SIZE:
                        fail(f"Archive regular-file size exceeds maximum ({total_size} > {MAX_TOTAL_SIZE}).")

            # The 'data' filter re-validates traversal/links/special files during extraction.
            tar.extractall(scratch, filter="data")

            for member in members:
                if not member.isfile():
                    continue
                source = os.path.join(scratch, member.name)
                if not os.path.isfile(source) or os.path.islink(source):
                    fail(f"Extracted member is missing or not a regular file: {member.name}")
                destination = os.path.realpath(os.path.join(dest_root, member.name))
                if destination != dest_root and not destination.startswith(dest_root + os.sep):
                    fail(f"Refusing to write outside destination: {member.name}")
                os.makedirs(os.path.dirname(destination), exist_ok=True)
                shutil.copyfile(source, destination)
                staged.append(member.name)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)

    if not staged:
        fail("No files matching the allowed extensions were present in the archive.")

    # NUL-terminate each path (print0 convention) for safe `mapfile -d ''` / `xargs -0` consumption.
    for path in staged:
        sys.stdout.buffer.write(path.encode() + b"\0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

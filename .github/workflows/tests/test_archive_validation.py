import io
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import textwrap
import unittest
import zipfile


WORKFLOWS = Path(__file__).resolve().parents[1]
SOURCES = (WORKFLOWS / "shared" / "build-failure-analysis-fetch.md",)


def archive_bytes(entries):
    content = io.BytesIO()
    with zipfile.ZipFile(content, "w") as archive:
        for name, mode in entries:
            entry = zipfile.ZipInfo(name)
            entry.create_system = 3
            entry.external_attr = mode << 16
            archive.writestr(entry, b"binlog")
    return content.getvalue()


def production_validator(path):
    lines = path.read_text(encoding="utf-8").splitlines()
    start = next(index for index, line in enumerate(lines) if "Validate ZIP entry metadata before extraction" in line)
    end = next(index for index, line in enumerate(lines[start:], start) if "Extract validated binlogs" in line)
    return textwrap.dedent("\n".join(lines[start:end])) + "\n"


class ArchiveValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bash = os.environ.get("BFA_BASH") or (
            r"C:\Program Files\Git\bin\bash.exe" if os.name == "nt" else shutil.which("bash")
        )
        if not cls.bash:
            raise RuntimeError("Bash is required.")

    def run_validator(self, source, entries, max_entries=65536):
        with tempfile.TemporaryDirectory(prefix="bfa-archive-test-") as directory:
            root = Path(directory)
            archive = root / "archive.zip"
            archive.write_bytes(archive_bytes(entries))
            path_value = os.environ.get("PATH", "")
            if os.name == "nt":
                python3 = root / "python3"
                python3.write_text('#!/usr/bin/env bash\nexec python "$@"\n', encoding="utf-8")
                python3.chmod(0o755)
                path_value = str(root) + os.pathsep + path_value
            script = (
                "set +e\n"
                'for ZIP_TMP in "$ARCHIVE"; do\n'
                "  safe_name=test\n"
                f"  MAX_ZIP_ENTRIES={max_entries}\n"
                + production_validator(source)
                + "  echo accepted=true\n"
                "done\n"
            )
            return subprocess.run(
                [self.bash, "--noprofile", "--norc", "-eo", "pipefail", "-s"],
                input=script,
                text=True,
                capture_output=True,
                cwd=root,
                env={**os.environ, "ARCHIVE": str(archive), "PATH": path_value},
                timeout=30,
            )

    def test_archive_validation_rejects_unsafe_paths_types_and_counts(self):
        cases = (
            ("regular", (("nested/build.binlog", stat.S_IFREG | 0o644),), 65536, True),
            ("unspecified", (("build.binlog", 0),), 65536, True),
            ("traversal", (("../escape.binlog", stat.S_IFREG | 0o644),), 65536, False),
            ("absolute", (("/escape.binlog", stat.S_IFREG | 0o644),), 65536, False),
            ("drive", ((r"C:\escape.binlog", stat.S_IFREG | 0o644),), 65536, False),
            ("symlink", (("link.binlog", stat.S_IFLNK | 0o777),), 65536, False),
            ("character-device", (("device.binlog", stat.S_IFCHR | 0o600),), 65536, False),
            ("block-device", (("device.binlog", stat.S_IFBLK | 0o600),), 65536, False),
            ("fifo", (("pipe.binlog", stat.S_IFIFO | 0o600),), 65536, False),
            ("socket", (("socket.binlog", stat.S_IFSOCK | 0o600),), 65536, False),
            (
                "entry-cap",
                (
                    ("first.binlog", stat.S_IFREG | 0o644),
                    ("second.binlog", stat.S_IFREG | 0o644),
                ),
                1,
                False,
            ),
        )
        for source in SOURCES:
            for name, entries, max_entries, accepted in cases:
                with self.subTest(source=source.name, case=name):
                    result = self.run_validator(source, entries, max_entries)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual("accepted=true" in result.stdout, accepted, result.stdout)


if __name__ == "__main__":
    unittest.main()

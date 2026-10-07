import io
import os
from pathlib import Path
import shutil
import stat
import struct
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


def rewrite_eocd(content, **updates):
    data = bytearray(content)
    offset = data.rfind(b"PK\x05\x06")
    values = list(struct.unpack_from("<4s4H2LH", data, offset))
    indexes = {
        "disk_number": 1,
        "central_directory_disk": 2,
        "entries_on_disk": 3,
        "entry_count": 4,
        "central_directory_size": 5,
        "central_directory_offset": 6,
    }
    for name, value in updates.items():
        values[indexes[name]] = value
    struct.pack_into("<4s4H2LH", data, offset, *values)
    return bytes(data)


def zip64_archive(content, classic_sentinels=True, **updates):
    eocd_offset = content.rfind(b"PK\x05\x06")
    (
        _,
        disk_number,
        central_directory_disk,
        entries_on_disk,
        entry_count,
        central_directory_size,
        central_directory_offset,
        comment_length,
    ) = struct.unpack_from("<4s4H2LH", content, eocd_offset)
    if comment_length:
        raise ValueError("fixture helper requires an archive without an EOCD comment")
    values = {
        "disk_number": disk_number,
        "central_directory_disk": central_directory_disk,
        "entries_on_disk": entries_on_disk,
        "entry_count": entry_count,
        "central_directory_size": central_directory_size,
        "central_directory_offset": central_directory_offset,
    }
    values.update(updates)
    zip64_eocd = struct.pack(
        "<4sQ2H2L4Q",
        b"PK\x06\x06",
        44,
        45,
        45,
        values["disk_number"],
        values["central_directory_disk"],
        values["entries_on_disk"],
        values["entry_count"],
        values["central_directory_size"],
        values["central_directory_offset"],
    )
    locator = struct.pack("<4sLQL", b"PK\x06\x07", 0, eocd_offset, 1)
    eocd = (
        struct.pack(
            "<4s4H2LH",
            b"PK\x05\x06",
            0,
            0,
            0xFFFF,
            0xFFFF,
            0xFFFFFFFF,
            0xFFFFFFFF,
            0,
        )
        if classic_sentinels
        else content[eocd_offset:]
    )
    return content[:eocd_offset] + zip64_eocd + locator + eocd


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

    def run_validator_bytes(
        self,
        source,
        content,
        max_entries=65536,
        max_metadata_bytes=16 * 1024 * 1024,
    ):
        with tempfile.TemporaryDirectory(prefix="bfa-archive-test-") as directory:
            root = Path(directory)
            archive = root / "archive.zip"
            archive.write_bytes(content)
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
                f"  MAX_ZIP_METADATA_BYTES={max_metadata_bytes}\n"
                "  MAX_UNZIP_BYTES=2147483648\n"
                "  MAX_TOTAL_BYTES=4294967296\n"
                "  TOTAL_BYTES=0\n"
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

    def run_validator(
        self,
        source,
        entries,
        max_entries=65536,
        max_metadata_bytes=16 * 1024 * 1024,
    ):
        return self.run_validator_bytes(
            source,
            archive_bytes(entries),
            max_entries,
            max_metadata_bytes,
        )

    def test_archive_validation_rejects_unsafe_paths_types_and_counts(self):
        cases = (
            ("regular", (("nested/build.binlog", stat.S_IFREG | 0o644),), 65536, 16 * 1024 * 1024, True),
            ("unspecified", (("build.binlog", 0),), 65536, 16 * 1024 * 1024, True),
            ("traversal", (("../escape.binlog", stat.S_IFREG | 0o644),), 65536, 16 * 1024 * 1024, False),
            ("absolute", (("/escape.binlog", stat.S_IFREG | 0o644),), 65536, 16 * 1024 * 1024, False),
            ("drive", ((r"C:\escape.binlog", stat.S_IFREG | 0o644),), 65536, 16 * 1024 * 1024, False),
            ("symlink", (("link.binlog", stat.S_IFLNK | 0o777),), 65536, 16 * 1024 * 1024, False),
            ("character-device", (("device.binlog", stat.S_IFCHR | 0o600),), 65536, 16 * 1024 * 1024, False),
            ("block-device", (("device.binlog", stat.S_IFBLK | 0o600),), 65536, 16 * 1024 * 1024, False),
            ("fifo", (("pipe.binlog", stat.S_IFIFO | 0o600),), 65536, 16 * 1024 * 1024, False),
            ("socket", (("socket.binlog", stat.S_IFSOCK | 0o600),), 65536, 16 * 1024 * 1024, False),
            (
                "entry-cap",
                (
                    ("first.binlog", stat.S_IFREG | 0o644),
                    ("second.binlog", stat.S_IFREG | 0o644),
                ),
                1,
                16 * 1024 * 1024,
                False,
            ),
            (
                "metadata-cap",
                (("build.binlog", stat.S_IFREG | 0o644),),
                65536,
                1,
                False,
            ),
        )
        for source in SOURCES:
            for name, entries, max_entries, max_metadata_bytes, accepted in cases:
                with self.subTest(source=source.name, case=name):
                    result = self.run_validator(
                        source,
                        entries,
                        max_entries,
                        max_metadata_bytes,
                    )
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual("accepted=true" in result.stdout, accepted, result.stdout)

    def test_archive_preflight_rejects_unbounded_and_multidisk_metadata(self):
        regular = archive_bytes((("build.binlog", stat.S_IFREG | 0o644),))
        cases = (
            ("missing-eocd", b"not a zip", 65536, 16 * 1024 * 1024, False),
            (
                "multi-disk",
                rewrite_eocd(regular, disk_number=1),
                65536,
                16 * 1024 * 1024,
                False,
            ),
            (
                "classic-entry-cap",
                rewrite_eocd(regular, entries_on_disk=2, entry_count=2),
                1,
                16 * 1024 * 1024,
                False,
            ),
            (
                "classic-metadata-cap",
                rewrite_eocd(regular, central_directory_size=16 * 1024 * 1024 + 1),
                65536,
                16 * 1024 * 1024,
                False,
            ),
            ("zip64", zip64_archive(regular), 65536, 16 * 1024 * 1024, True),
            (
                "zip64-entry-cap",
                zip64_archive(regular, entries_on_disk=2, entry_count=2),
                1,
                16 * 1024 * 1024,
                False,
            ),
            (
                "zip64-nonsentinel-entry-cap",
                zip64_archive(
                    regular,
                    classic_sentinels=False,
                    entries_on_disk=2,
                    entry_count=2,
                ),
                1,
                16 * 1024 * 1024,
                False,
            ),
            (
                "zip64-metadata-cap",
                zip64_archive(
                    regular,
                    central_directory_size=16 * 1024 * 1024 + 1,
                ),
                65536,
                16 * 1024 * 1024,
                False,
            ),
        )
        for source in SOURCES:
            for name, content, max_entries, max_metadata_bytes, accepted in cases:
                with self.subTest(source=source.name, case=name):
                    result = self.run_validator_bytes(
                        source,
                        content,
                        max_entries,
                        max_metadata_bytes,
                    )
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual("accepted=true" in result.stdout, accepted, result.stdout)


if __name__ == "__main__":
    unittest.main()

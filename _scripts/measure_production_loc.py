#!/usr/bin/env python3
"""Count authored production LOC/files for ClientManager refactor measurement."""

from __future__ import annotations

import json
import re
import sys
import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

EXCLUDED_DIR_PARTS = {
    "tests",
    "docs",
    "_scripts",
    "bin",
    "obj",
    ".git",
    ".cursor",
    "node_modules",
}

VENDORED_PREFIXES = (
    "ClientManager.AdminUI/wwwroot/bootstrap/",
    "ClientManager.AdminUI/wwwroot/vendor/",
    "ClientManager.AdminUI/wwwroot/fonts/",
)

PRODUCTION_ROOTS = (
    ROOT / "ClientManager.Api",
    ROOT / "ClientManager.AdminUI",
    ROOT / "ClientManager.Shared",
    ROOT / "compose",
)

SOURCE_SUFFIXES = {".cs", ".razor", ".css", ".js", ".json", ".yml", ".yaml", ".py"}

GENERATED_FILE_NAMES = {
    "ClientManager.Api.xml",
    "ClientManager.Shared.xml",
}


@dataclass
class FileCount:
    path: str
    lines: int


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def is_excluded(path: Path) -> bool:
    parts = path.relative_to(ROOT).parts
    if any(part in EXCLUDED_DIR_PARTS for part in parts):
        return True

    posix = rel(path)
    if any(posix.startswith(prefix) for prefix in VENDORED_PREFIXES):
        return True

    if path.name in GENERATED_FILE_NAMES:
        return True

    return False


def strip_comments_and_blanks(text: str, suffix: str) -> list[str]:
    if suffix == ".cs":
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    elif suffix == ".razor":
        text = re.sub(r"@\*.*?\*@", "", text, flags=re.S)
        text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
        text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    elif suffix in {".js", ".css"}:
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = re.sub(r"^\s*//.*$", "", text, flags=re.M)

    lines = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped:
            lines.append(stripped)
    return lines


def count_file(path: Path) -> int:
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        text = path.read_text(encoding="utf-8", errors="ignore")
    return len(strip_comments_and_blanks(text, path.suffix.lower()))


def iter_production_files() -> list[Path]:
    files: list[Path] = []
    for root in PRODUCTION_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() not in SOURCE_SUFFIXES:
                continue
            if is_excluded(path):
                continue
            files.append(path)
    return sorted(files, key=lambda p: rel(p))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=ROOT / "tests" / "production-after.json",
        help="Report path (default: tests/production-after.json).",
    )
    args = parser.parse_args()
    files = iter_production_files()
    per_file = [FileCount(rel(path), count_file(path)) for path in files]
    total_lines = sum(item.lines for item in per_file)

    report = {
        "measured_at": datetime.now(timezone.utc).isoformat(),
        "root": str(ROOT),
        "total_files": len(per_file),
        "total_lines": total_lines,
        "excludes": {
            "directories": sorted(EXCLUDED_DIR_PARTS),
            "vendored_prefixes": list(VENDORED_PREFIXES),
            "generated_file_names": sorted(GENERATED_FILE_NAMES),
        },
        "production_roots": [str(p.relative_to(ROOT)) for p in PRODUCTION_ROOTS if p.exists()],
        "top_files_by_lines": sorted(
            (item.__dict__ for item in per_file),
            key=lambda item: item["lines"],
            reverse=True,
        )[:25],
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(json.dumps({"total_files": report["total_files"], "total_lines": report["total_lines"]}, indent=2))
    print(f"Report written to {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

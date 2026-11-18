"""Validate provenance and package the complete, playable Windows release."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import zipfile
from pathlib import Path

VERSION_RE = re.compile(r"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\Z")


def write_outputs(values: dict[str, str]) -> None:
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            for key, value in values.items():
                handle.write(f"{key}={value}\n")
    else:
        for key, value in values.items():
            print(f"{key}={value}")


def validate_version(raw: str) -> str:
    version = raw.removeprefix("v")
    if not VERSION_RE.fullmatch(version):
        raise ValueError(f"expected semantic version MAJOR.MINOR.PATCH, got {raw!r}")
    if any(int(part) > 65535 for part in version.split(".")):
        raise ValueError("version components must be between 0 and 65535 for Unity")
    return version


def version_command(raw: str) -> None:
    version = validate_version(raw)
    write_outputs(
        {
            "version": version,
            "tag": f"v{version}",
            "artifact": f"Bluff-{version}-Windows",
        }
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def package_command(build: Path, output: Path, version: str, commit: str) -> None:
    version = validate_version(version)
    build, output = build.resolve(), output.resolve()
    if output == build or build in output.parents:
        raise ValueError("output must be outside the player directory")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("commit must be a full 40-character Git SHA")
    required = (
        "Bluff.exe",
        "UnityPlayer.dll",
        "Bluff_Data/globalgamemanagers",
        "Bluff_Data/Managed/Bluff.Game.dll",
        "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
        "build-info.json",
    )
    for name in required:
        path = build / name
        if not path.is_file() or path.stat().st_size == 0:
            raise FileNotFoundError(f"required build file missing or empty: {path}")
    info = json.loads((build / "build-info.json").read_text(encoding="utf-8-sig"))
    for key, expected in {"version": version, "commit": commit, "target": "StandaloneWindows64"}.items():
        if info.get(key) != expected:
            raise ValueError(f"build {key} does not match requested release: {info.get(key)!r} != {expected!r}")
    if not info.get("unityVersion"):
        raise ValueError("build metadata is missing the Unity editor version")
    files = [source for source in sorted(build.rglob("*")) if source.is_file()]
    # Unity's debug-only directories should not be distributed to players.
    files = [source for source in files if not any("DoNotShip" in part for part in source.relative_to(build).parts)]
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"Bluff-{version}-Windows.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as handle:
        for source in files:
            handle.write(source, source.relative_to(build).as_posix())

    manifest = {
        "name": "Bluff",
        "version": version,
        "tag": f"v{version}",
        "commit": commit,
        "target": "StandaloneWindows64",
        "unityVersion": info["unityVersion"],
        "archive": archive.name,
    }
    manifest_path = output / "release-manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    (output / "SHA256SUMS.txt").write_text(
        f"{sha256(archive)}  {archive.name}\n{sha256(manifest_path)}  {manifest_path.name}\n", encoding="utf-8"
    )
    print(f"Packaged {len(files)} files: {archive} ({archive.stat().st_size:,} bytes)")


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    version_parser = subparsers.add_parser("version")
    version_parser.add_argument("value")
    package_parser = subparsers.add_parser("package")
    package_parser.add_argument("--build", type=Path, required=True)
    package_parser.add_argument("--output", type=Path, required=True)
    package_parser.add_argument("--version", required=True)
    package_parser.add_argument("--commit", required=True)
    args = parser.parse_args()
    if args.command == "version":
        version_command(args.value)
    else:
        package_command(args.build, args.output, args.version, args.commit)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (FileNotFoundError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)

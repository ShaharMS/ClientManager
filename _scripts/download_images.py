"""Download dependency images and build flattened ClientManager project images.

Dependency downloads pull the external images the repository uses. Project
builds are opt-in, versioned, and flattened so the exported ClientManager
images contain their runtime filesystem inline for single-image distribution.
Every downloaded or built image is exported into _scripts/.downloaded_images/
as a Docker tar archive without being tracked by git.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from configuration import CONFIGURATION


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
DOWNLOAD_IMAGES_SETTINGS = CONFIGURATION["scripts"]["download_images"]
DOWNLOAD_IMAGES_PATHS = DOWNLOAD_IMAGES_SETTINGS["paths"]
DOWNLOAD_IMAGES_DEFAULTS = DOWNLOAD_IMAGES_SETTINGS["defaults"]
DOWNLOAD_IMAGES_DOCKER = DOWNLOAD_IMAGES_SETTINGS["docker"]
DOWNLOADED_IMAGES_DIR = SCRIPT_DIR / DOWNLOAD_IMAGES_PATHS["download_directory"]
MANIFEST_PATH = DOWNLOADED_IMAGES_DIR / DOWNLOAD_IMAGES_PATHS["manifest_file"]
FAKE_DELIVERY_SCRIPT = SCRIPT_DIR / "upload_music_fetcher_fake_delivery.ps1"
DOCKER_TAG_PATTERN = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$")
DEPENDENCY_OVERRIDE_KEYS = frozenset(DOWNLOAD_IMAGES_DOCKER["dependency_images"].keys())


@dataclass(frozen=True)
class DockerfileTarget:
    project_name: str
    dockerfile_path: Path
    image_repository: str
    image_args: tuple[tuple[str, str], ...]

    def final_tag(self, build_version: str) -> str:
        return f"{self.image_repository}:{build_version}"

    def staged_tag(self, build_version: str) -> str:
        return f"{self.image_repository}:build-{build_version}"


PRODUCTION_TARGETS = (
    DockerfileTarget(
        project_name="ClientManager.AdminUI",
        dockerfile_path=REPO_ROOT / "ClientManager.AdminUI" / "Dockerfile",
        image_repository="clientmanager/adminui",
        image_args=(("SDK_IMAGE", "sdk"), ("ASPNET_IMAGE", "aspnet")),
    ),
    DockerfileTarget(
        project_name="ClientManager.Api",
        dockerfile_path=REPO_ROOT / "ClientManager.Api" / "Dockerfile",
        image_repository="clientmanager/api",
        image_args=(("SDK_IMAGE", "sdk"), ("ASPNET_IMAGE", "aspnet")),
    ),
    DockerfileTarget(
        project_name="ClientManager.StorageApi",
        dockerfile_path=REPO_ROOT / "ClientManager.StorageApi" / "Dockerfile",
        image_repository="clientmanager/storageapi",
        image_args=(("SDK_IMAGE", "sdk"), ("ASPNET_IMAGE", "aspnet")),
    ),
    DockerfileTarget(
        project_name="ClientManager.DataAccess.Tests",
        dockerfile_path=REPO_ROOT / "ClientManager.DataAccess.Tests" / "Dockerfile",
        image_repository="clientmanager/dataaccess-tests",
        image_args=(("SDK_IMAGE", "sdk"), ("RUNTIME_IMAGE", "runtime")),
    ),
)


def resolve_observability_image(image_key: str) -> str:
    docker_settings = CONFIGURATION["scripts"]["launch_observability_ui"]["docker"]
    override = docker_settings.get("image_overrides", {}).get(image_key)
    if override:
        return override

    image_name = docker_settings["images"][image_key]
    registry_prefix = docker_settings.get("registry_prefix")
    if registry_prefix:
        return f"{registry_prefix.rstrip('/')}/{image_name}"

    return image_name


def get_development_images() -> list[str]:
    return [
        resolve_observability_image("jaeger"),
        resolve_observability_image("prometheus"),
        resolve_observability_image("grafana"),
    ]


def resolve_dependency_image(image_key: str) -> str:
    configured_image = DOWNLOAD_IMAGES_DOCKER["dependency_images"].get(image_key)
    if configured_image:
        return configured_image

    if image_key in {"jaeger", "prometheus", "grafana"}:
        return resolve_observability_image(image_key)

    raise RuntimeError(f"No default dependency image configured for '{image_key}'.")


def get_dependency_images(overrides: dict[str, str]) -> dict[str, str]:
    images = {
        dependency_key: resolve_dependency_image(dependency_key)
        for dependency_key in DEPENDENCY_OVERRIDE_KEYS
    }
    images.update(overrides)
    return images


def get_dependency_download_images(dependency_images: dict[str, str]) -> list[str]:
    return [
        dependency_images["jaeger"],
        dependency_images["prometheus"],
        dependency_images["grafana"],
        dependency_images["sdk"],
        dependency_images["aspnet"],
        dependency_images["runtime"],
    ]


def get_project_images(build_version: str) -> list[str]:
    return [target.final_tag(build_version) for target in PRODUCTION_TARGETS]


def unique_images(image_list: list[str]) -> list[str]:
    unique_list: list[str] = []
    seen: set[str] = set()

    for image in image_list:
        if image in seen:
            continue

        seen.add(image)
        unique_list.append(image)

    return unique_list


def ensure_docker_available() -> None:
    try:
        subprocess.run(
            ["docker", "version", "--format", "{{.Server.Version}}"],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
        )
    except FileNotFoundError as error:
        raise RuntimeError("Docker is not installed or not available on PATH.") from error
    except subprocess.CalledProcessError as error:
        stderr = (error.stderr or "").strip()
        detail = f" Docker reported: {stderr}" if stderr else ""
        raise RuntimeError(f"Docker is installed but not ready.{detail}") from error


def run_command(
    command: list[str],
    *,
    capture_output: bool = False,
    text: bool = True,
) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
    completed = subprocess.run(
        command,
        cwd=REPO_ROOT,
        check=False,
        capture_output=capture_output,
        text=text,
    )
    if completed.returncode != 0:
        stderr = completed.stderr if capture_output and isinstance(completed.stderr, str) else ""
        stdout = completed.stdout if capture_output and isinstance(completed.stdout, str) else ""
        detail = stderr.strip() or stdout.strip()
        message = f"Command failed with exit code {completed.returncode}: {' '.join(command)}"
        if detail:
            message = f"{message}\n{detail}"
        raise RuntimeError(message)

    return completed


def ensure_local_image(image: str) -> None:
    completed = subprocess.run(
        ["docker", "image", "inspect", image],
        cwd=REPO_ROOT,
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
    )
    if completed.returncode == 0:
        return

    stderr = (completed.stderr or "").strip()
    detail = f" Docker reported: {stderr}" if stderr else ""
    raise RuntimeError(
        f"Expected local image '{image}' is not available for export.{detail}"
    )


def get_archive_path(image: str) -> Path:
    safe_name = re.sub(r"[^A-Za-z0-9._-]+", "_", image)
    return DOWNLOADED_IMAGES_DIR / f"{safe_name}.tar"


def resolve_local_path(raw_path: str) -> Path:
    expanded_path = Path(os.path.expandvars(os.path.expanduser(raw_path)))
    if expanded_path.is_absolute():
        return expanded_path.resolve()

    return (REPO_ROOT / expanded_path).resolve()


def get_powershell_executable() -> str:
    for candidate in ("pwsh", "powershell"):
        resolved = shutil.which(candidate)
        if resolved:
            return resolved

    raise RuntimeError("PowerShell is not installed or not available on PATH.")


def upload_fake_delivery(image_path: str) -> None:
    resolved_image_path = resolve_local_path(image_path)
    if not resolved_image_path.exists():
        raise RuntimeError(f"Fake delivery image path does not exist: {resolved_image_path}")
    if not resolved_image_path.is_file():
        raise RuntimeError(f"Fake delivery image path is not a file: {resolved_image_path}")
    if not FAKE_DELIVERY_SCRIPT.exists():
        raise RuntimeError(f"PowerShell fake-delivery uploader was not found: {FAKE_DELIVERY_SCRIPT}")

    command = [
        get_powershell_executable(),
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(FAKE_DELIVERY_SCRIPT),
        "-ImagePath",
        str(resolved_image_path),
    ]
    run_command(command)


def load_manifest_entries() -> dict[str, dict[str, object]]:
    if not MANIFEST_PATH.exists():
        return {}

    try:
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise RuntimeError(f"Existing image manifest is invalid JSON: {MANIFEST_PATH}") from error

    entries: dict[str, dict[str, object]] = {}
    for entry in manifest.get("images", []):
        image = entry.get("image")
        archive = entry.get("archive")
        if not isinstance(image, str) or not isinstance(archive, str):
            continue

        archive_path = REPO_ROOT / Path(archive)
        if archive_path.exists():
            entries[image] = entry

    return entries


def export_images(images: list[str]) -> None:
    if not images:
        return

    DOWNLOADED_IMAGES_DIR.mkdir(parents=True, exist_ok=True)

    manifest_entries = load_manifest_entries()
    for image in images:
        ensure_local_image(image)
        archive_path = get_archive_path(image)
        if archive_path.exists():
            archive_path.unlink()
        run_command(["docker", "save", "-o", str(archive_path), image])
        manifest_entries[image] = {
            "image": image,
            "archive": str(archive_path.relative_to(REPO_ROOT)).replace("\\", "/"),
            "sizeBytes": archive_path.stat().st_size,
        }

    manifest = {
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "images": sorted(manifest_entries.values(), key=lambda entry: str(entry["image"])),
    }
    MANIFEST_PATH.write_text(f"{json.dumps(manifest, indent=2)}\n", encoding="utf-8")


def parse_dependency_overrides(raw_overrides: list[str]) -> dict[str, str]:
    overrides: dict[str, str] = {}
    for raw_override in raw_overrides:
        key, separator, value = raw_override.partition("=")
        dependency_key = key.strip().lower()
        dependency_value = value.strip()

        if separator != "=" or dependency_key not in DEPENDENCY_OVERRIDE_KEYS or not dependency_value:
            valid_keys = ", ".join(sorted(DEPENDENCY_OVERRIDE_KEYS))
            raise RuntimeError(
                f"Invalid dependency override '{raw_override}'. Use KEY=IMAGE with one of: {valid_keys}."
            )

        overrides[dependency_key] = dependency_value

    return overrides


def validate_build_version(build_version: str) -> None:
    if not DOCKER_TAG_PATTERN.fullmatch(build_version):
        raise RuntimeError(
            "Build version must be a valid Docker tag component containing only letters, digits, underscores, periods, or hyphens."
        )


def get_image_config(image: str) -> dict[str, object]:
    completed = run_command(["docker", "image", "inspect", image], capture_output=True)
    payload = json.loads(completed.stdout)
    if not payload:
        raise RuntimeError(f"Docker did not return image configuration for {image}.")

    return payload[0].get("Config") or {}


def create_container(image: str) -> str:
    completed = run_command(["docker", "create", image], capture_output=True)
    container_id = completed.stdout.strip()
    if not container_id:
        raise RuntimeError(f"Docker did not return a container id for image {image}.")

    return container_id


def remove_container(container_id: str) -> None:
    subprocess.run(
        ["docker", "rm", "-f", container_id],
        cwd=REPO_ROOT,
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        text=True,
    )


def remove_image(image: str) -> None:
    subprocess.run(
        ["docker", "image", "rm", image],
        cwd=REPO_ROOT,
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        text=True,
    )


def get_import_changes(config: dict[str, object]) -> list[str]:
    changes: list[str] = []

    for env_value in config.get("Env") or []:
        changes.append(f"ENV {env_value}")

    working_directory = config.get("WorkingDir")
    if isinstance(working_directory, str) and working_directory:
        changes.append(f"WORKDIR {working_directory}")

    for exposed_port in sorted((config.get("ExposedPorts") or {}).keys()):
        changes.append(f"EXPOSE {exposed_port}")

    user = config.get("User")
    if isinstance(user, str) and user:
        changes.append(f"USER {user}")

    entrypoint = config.get("Entrypoint")
    if isinstance(entrypoint, list) and entrypoint:
        changes.append(f"ENTRYPOINT {json.dumps(entrypoint)}")

    command = config.get("Cmd")
    if isinstance(command, list) and command:
        changes.append(f"CMD {json.dumps(command)}")

    return changes


def flatten_image(source_image: str, target_image: str) -> None:
    image_config = get_image_config(source_image)
    container_id = create_container(source_image)

    try:
        with tempfile.TemporaryDirectory(prefix="clientmanager-image-export-") as temp_directory:
            archive_path = Path(temp_directory) / "filesystem.tar"
            run_command(["docker", "export", "-o", str(archive_path), container_id])

            import_command = ["docker", "import"]
            for change in get_import_changes(image_config):
                import_command.extend(["--change", change])

            import_command.extend([str(archive_path), target_image])
            run_command(import_command)
    finally:
        remove_container(container_id)


def build_project_images(
    dependency_images: dict[str, str],
    package_sources: list[str],
    build_version: str,
) -> list[str]:
    validate_build_version(build_version)
    built_images: list[str] = []
    joined_package_sources = ";".join(package_sources)

    for target in PRODUCTION_TARGETS:
        staged_tag = target.staged_tag(build_version)
        final_tag = target.final_tag(build_version)
        build_command = [
            "docker",
            "build",
            "-f",
            str(target.dockerfile_path),
            "-t",
            staged_tag,
        ]

        for argument_name, dependency_key in target.image_args:
            build_command.extend(["--build-arg", f"{argument_name}={dependency_images[dependency_key]}"])

        if joined_package_sources:
            build_command.extend(["--build-arg", f"NUGET_SOURCES={joined_package_sources}"])

        build_command.append(str(REPO_ROOT))
        run_command(build_command)
        flatten_image(staged_tag, final_tag)
        remove_image(staged_tag)
        built_images.append(final_tag)

    return built_images


def print_plan(
    *,
    download_dependencies: bool,
    build_projects: bool,
    build_version: str,
    dependency_images: dict[str, str],
    package_sources: list[str],
    upload_fake_delivery_image: str | None,
) -> None:
    if download_dependencies:
        print("Dependency images:")
        for image in get_dependency_download_images(dependency_images):
            print(f"  pull {image}")

    if build_projects:
        print("Project image builds:")
        print(f"  version {build_version}")
        for target in PRODUCTION_TARGETS:
            print(
                f"  build+flatten {target.final_tag(build_version)} from {target.dockerfile_path.relative_to(REPO_ROOT)}"
            )
        if package_sources:
            print("Package sources:")
            for package_source in package_sources:
                print(f"  restore {package_source}")

    if upload_fake_delivery_image:
        resolved_image_path = resolve_local_path(upload_fake_delivery_image)
        print("Fake delivery staging upload:")
        print(f"  powershell {FAKE_DELIVERY_SCRIPT.relative_to(REPO_ROOT)} --ImagePath {resolved_image_path}")
        print("  packages the fake delivery into content.zip and injects it into Music Fetcher's staging area")

    export_targets = unique_images(
        [
            *(get_dependency_download_images(dependency_images) if download_dependencies else []),
            *(get_project_images(build_version) if build_projects else []),
        ]
    )
    if export_targets:
        print("Exported image archives:")
        for image in export_targets:
            print(f"  save {image} -> {get_archive_path(image).relative_to(REPO_ROOT)}")


def execute_actions(
    *,
    download_dependencies: bool,
    build_projects: bool,
    build_version: str,
    dependency_images: dict[str, str],
    package_sources: list[str],
    upload_fake_delivery_image: str | None,
) -> None:
    if download_dependencies or build_projects:
        ensure_docker_available()

    exported_images: list[str] = []

    if download_dependencies:
        dependency_downloads = get_dependency_download_images(dependency_images)
        for image in dependency_downloads:
            run_command(["docker", "pull", image])
        exported_images.extend(dependency_downloads)

    if build_projects:
        built_images = build_project_images(dependency_images, package_sources, build_version)
        exported_images.extend(built_images)

    export_images(unique_images(exported_images))

    if upload_fake_delivery_image:
        upload_fake_delivery(upload_fake_delivery_image)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download ClientManager dependency images and build flattened project images locally."
    )
    parser.add_argument(
        "--download-dependencies",
        action="store_true",
        help="Download the external dependency images used by ClientManager and export them into _scripts/.downloaded_images/.",
    )
    parser.add_argument(
        "--build-projects",
        action="store_true",
        help="Build all ClientManager project images, flatten their runtime dependencies inline, and export them into _scripts/.downloaded_images/.",
    )
    parser.add_argument(
        "--build-version",
        default=DOWNLOAD_IMAGES_DEFAULTS["build_version"],
        help="Docker tag suffix and archive naming version for images built with --build-projects.",
    )
    parser.add_argument(
        "--upload-fake-delivery",
        metavar="IMAGE_PATH",
        help="Upload a local image into Music Fetcher's Hetzner staging area by invoking _scripts/upload_music_fetcher_fake_delivery.ps1.",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="Print the pull and build plan without running Docker.",
    )
    parser.add_argument(
        "--dependency-image-override",
        action="append",
        default=[],
        metavar="NAME=IMAGE",
        help="Override external dependency image references. Supported keys: jaeger, prometheus, grafana, sdk, aspnet, runtime.",
    )
    parser.add_argument(
        "--package-source",
        action="append",
        default=None,
        metavar="URL",
        help="Alternative NuGet package source to use during --build-projects. Repeat to add multiple sources.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    explicit_actions_requested = args.download_dependencies or args.build_projects or bool(args.upload_fake_delivery)
    preview_all_actions = args.list and not explicit_actions_requested
    package_sources = list(args.package_source) if args.package_source else list(DOWNLOAD_IMAGES_DEFAULTS["package_sources"])

    download_dependencies = args.download_dependencies or preview_all_actions
    build_projects = args.build_projects or preview_all_actions

    try:
        if not explicit_actions_requested and not args.list:
            raise RuntimeError(
                "Specify at least one action: --download-dependencies, --build-projects, and/or --upload-fake-delivery."
            )

        if package_sources and not build_projects:
            raise RuntimeError("--package-source is only valid together with --build-projects or when previewing a build plan with --list.")

        dependency_images = get_dependency_images(parse_dependency_overrides(args.dependency_image_override))

        if args.list:
            print_plan(
                download_dependencies=download_dependencies,
                build_projects=build_projects,
                build_version=args.build_version,
                dependency_images=dependency_images,
                package_sources=package_sources,
                upload_fake_delivery_image=args.upload_fake_delivery,
            )
            return 0

        execute_actions(
            download_dependencies=download_dependencies,
            build_projects=build_projects,
            build_version=args.build_version,
            dependency_images=dependency_images,
            package_sources=package_sources,
            upload_fake_delivery_image=args.upload_fake_delivery,
        )
        return 0
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
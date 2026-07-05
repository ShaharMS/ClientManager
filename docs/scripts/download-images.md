# download_images.py

Downloads dependency Docker images and optionally builds flattened ClientManager project images for offline distribution.

## Prerequisites

- Docker installed and running (for pull/build operations)

## Usage

```powershell
python _scripts/download_images.py --help
```

Exports images as tar archives under `_scripts/.downloaded_images/` (gitignored).

## Related

- [Development and operations](../development-and-operations.md) — Docker Compose notes

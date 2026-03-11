# Camera Copier

A production-quality .NET 10 console application that monitors one or more folders for photos and videos, determines their capture date, and copies them into a structured destination directory organized by date.

## Features

- **Startup scan** – processes all existing files on launch
- **Filesystem watching** – reacts to new/renamed files in real time
- **Periodic scan** – catches any events missed by the watcher
- **Duplicate detection** – SHA256 hashing prevents reprocessing identical files
- **Conflict resolution** – files with the same name but different content are renamed with a numeric suffix
- **Parallel processing** – configurable number of concurrent workers
- **Graceful shutdown** – cancellation token support throughout
- **Structured logging** – via `Microsoft.Extensions.Logging`

## Supported File Types

| Category | Extensions |
|----------|------------|
| Images | `.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.heif` |
| Videos | `.mp4`, `.mov` |

## Destination Structure

Files are copied (not moved) to:

```
<DestinationFolder>/<yyyy-MM-dd>/<original-filename>
```

Example: `E:/Photos/2025-08-14/IMG_1234.JPG`

## Setup & Configuration

Edit `appsettings.json` before running:

```json
{
  "SourceFolders": [
    "C:/CameraImports",
    "D:/PhoneSync"
  ],
  "DestinationFolder": "E:/Photos",
  "ProcessedHashFile": "processed.json",
  "ScanIntervalSeconds": 300,
  "MaxConcurrentProcessors": 2
}
```

| Field | Description |
|-------|-------------|
| `SourceFolders` | Folders to monitor for new photos/videos |
| `DestinationFolder` | Root output folder |
| `ProcessedHashFile` | JSON file storing SHA256 hashes of processed files |
| `ScanIntervalSeconds` | How often (in seconds) to run a full periodic scan |
| `MaxConcurrentProcessors` | Number of parallel file-processing workers |

## Running

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### From source

```bash
git clone https://github.com/DanTheMan827/camera-copier.git
cd camera-copier

# Edit src/CameraCopier/appsettings.json with your paths

dotnet run --project src/CameraCopier
```

### Published binary

Download the latest release for your platform from the [Releases](../../releases) page, extract, and run:

```bash
./CameraCopier   # Linux / macOS
CameraCopier.exe # Windows
```

## Building

```bash
dotnet build                        # Debug
dotnet build --configuration Release # Release
```

## Testing

```bash
dotnet test
```

## Release Process

Releases are automated via GitHub Actions.

1. Tag a commit with a semantic version:

   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

2. The [release workflow](.github/workflows/release.yml) will:
   - Set the project version to `1.2.3`
   - Build and test
   - Publish self-contained single-file binaries for **Linux x64**, **Windows x64**, and **macOS x64**
   - Create a GitHub Release with the binaries attached

## Architecture

```
src/CameraCopier/
├── Configuration/
│   └── AppSettings.cs          # Configuration model
├── Services/
│   ├── HashService.cs          # SHA256 file hashing
│   ├── ProcessedHashStore.cs   # Persistent hash tracking
│   ├── MetadataService.cs      # EXIF / QuickTime metadata extraction
│   ├── ProcessingQueue.cs      # Thread-safe channel-backed queue
│   ├── CopyService.cs          # File copy with conflict resolution
│   ├── FileProcessor.cs        # Per-file processing pipeline
│   ├── FolderScanner.cs        # Recursive folder enumeration
│   └── FileWatcherService.cs   # Background hosted service
├── Program.cs                  # DI host setup
└── appsettings.json
```


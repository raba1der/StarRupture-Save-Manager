# Star Rupture Save Manager

A cross-platform desktop application for managing and repairing save files for the game **Star Rupture**. The primary UI is now Avalonia (`net8.0`), backed by a shared platform-neutral core library.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Screenshots
<img width="374" height="337" alt="image" src="https://github.com/user-attachments/assets/4ec138e2-6c0b-417f-9013-64e0e10211d6" />

<img width="374" height="337" alt="image" src="https://github.com/user-attachments/assets/ebaf49dc-dcc2-4d30-b171-6a61d9584a4e" />

<img width="374" height="337" alt="image" src="https://github.com/user-attachments/assets/90766047-d01d-4704-aaa2-d145cb8589a9" />


## Features

### Save File Repair
- **Fix Drones** – Automatically detects and removes drones with invalid movement targets that cause save corruption
- **Remove All Drones** – Completely removes all drone entities from your save file
- **Non-destructive** – Original save files are backed up with `_original.sav` suffix before modifications

### Session Management
- **Browse Sessions** – View all your Star Rupture save sessions in one place
- **Copy Saves** – Transfer save files between different game sessions
- **Delete Sessions** – Remove entire game sessions with confirmation protection

### FTP Integration
- **Upload Saves** – Sync your save files to a remote FTP server
- **Download Saves** – Retrieve save files from your FTP server
- **Secure Storage** – FTP credentials encrypted per-user (DPAPI on Windows, AES key on non-Windows)
- **FTPS Support** – Explicit TLS encryption for secure transfers

### Additional Features
- **Auto-detection** – Automatically locates your Steam save game folder
- **Auto-updates** – Checks GitHub for new releases and notifies you when updates are available
- **Progress Logging** – Real-time feedback during all operations
- **Diagnostic Logging** – Comprehensive logs saved to `%LOCALAPPDATA%\SRSM\` for troubleshooting

## Interface

The application provides a tabbed interface with five main sections:
1. **Save Browser** – View and fix save files
2. **Session Manager** – Manage game sessions
3. **FTP Upload** – Upload saves to remote server
4. **FTP Download** – Download saves from remote server
5. **Settings** – Configure paths and FTP credentials

## Requirements

- .NET 8.0 SDK (for build from source)
- Linux desktop, Windows, or macOS for the Avalonia UI

### Primary UI (Avalonia)

- Project: `src/StarRuptureSaveManager.Avalonia/StarRuptureSaveManager.Avalonia.csproj`
- Future feature development targets this UI.

Run:

```bash
dotnet run --project src/StarRuptureSaveManager.Avalonia/StarRuptureSaveManager.Avalonia.csproj
```

### Core Library

The repository now includes a platform-neutral core library project:

- `StarRuptureSaveManager.Core.csproj` (`net8.0`)
- Contains `Models/`, `Services/`, `Fixers/`, and `Utils/`
- The legacy WPF app (`StarRuptureSaveManager.csproj`) remains for backward compatibility on Windows only.

Build only the core on Linux/macOS:

```bash
dotnet build StarRuptureSaveManager.Core.csproj -c Release
```

Current Avalonia status:
- Save Browser: implemented
- Session Manager: implemented
- FTP Sync: implemented
- Settings: implemented

Parity checklist:
- `docs/AVALONIA_PARITY_CHECKLIST.md`

Linux packaging:
- `scripts/package-linux.sh` (linux-x64)
- `scripts/package-linux.sh --with-arm64` (linux-x64 + linux-arm64)

### Release Tagging Flow

The GitHub release workflow runs automatically when you push a tag matching `v*`.

```bash
# From main with committed changes
git pull
git tag v1.0.9
git push origin v1.0.9
```

This triggers `.github/workflows/release.yml`, which:
- runs core regression tests
- builds Avalonia Linux artifacts for `linux-x64` and `linux-arm64`
- uploads `.tar.gz` files to the GitHub Release for that tag

### WPF Status (Legacy)

- `StarRuptureSaveManager.csproj` is now legacy.
- New features should be implemented in Avalonia + Core.

## Installation

### Option 1: Download Release (Recommended)
Download the latest release from the [Releases](https://github.com/AlienXAXS/StarRupture-Save-Fixer/releases) page or the [StarRupture Utilities Website](https://starrupture-utilities.com)

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/AlienXAXS/StarRupture-Save-Fixer.git
cd StarRupture-Save-Fixer

# Build the project
dotnet build -c Release

# Build all projects
dotnet build StarRuptureSaveManager.sln -c Release

# Run core regression tests
dotnet test tests/StarRuptureSaveManager.Core.Tests/StarRuptureSaveManager.Core.Tests.csproj -c Debug

# Package Linux artifacts (tar.gz)
./scripts/package-linux.sh
```

## Usage

1. **Launch the application** – Run `StarRuptureSaveFixer.exe`
2. **Select a session** – Your save sessions are automatically detected and listed
3. **Choose a save file** – Select the save you want to fix or manage
4. **Apply fixes** – Click "Fix Drones" or "Remove All Drones" as needed

The application automatically backs up your original save before making any changes.

## Save File Location

Star Rupture save files are typically located at:
```
C:\Program Files (x86)\Steam\userdata\<steam-id>\1631270\remote\Saved\SaveGames\
```

The application automatically detects this path. If auto-detection fails, you can set a custom path in Settings.

## How It Works

Star Rupture save files use a custom compressed format:
1. **4-byte header** – Contains the uncompressed JSON size (little-endian)
2. **zlib wrapper** – 2-byte header (0x78 0x9C)
3. **Deflate payload** – Compressed JSON game data
4. **Adler32 checksum** – 4-byte integrity check

The tool:
1. Reads and decompresses the save file
2. Parses the JSON structure to locate entity data
3. Identifies problematic entities (drones with invalid movement targets)
4. Removes or repairs the identified issues
5. Recompresses with proper zlib format and saves the fixed file

## Project Structure

```
StarRuptureSaveFixer/
├── MainWindow.xaml           # Main application window
├── Converters/               # WPF value converters
├── Fixers/
│   ├── IFixer.cs             # Interface for save file fixers
│   ├── DroneFixer.cs         # Fixes drones with invalid targets
│   └── DroneRemover.cs       # Removes all drones
├── Models/
│   ├── SaveFile.cs           # Save file data model
│   ├── SaveSession.cs        # Game session model
│   ├── FtpSettings.cs        # FTP configuration
│   └── AppSettings.cs        # Application settings
├── Services/
│   ├── SaveFileService.cs    # Save file compression/decompression
│   ├── SessionManager.cs     # Game session management
│   ├── FtpService.cs         # FTP operations
│   ├── UpdateChecker.cs      # GitHub release checking
│   └── SettingsService.cs    # Settings persistence
├── ViewModels/               # MVVM view models
└── Views/                    # XAML UI definitions
```

## Configuration

Application settings are stored at:
```
%APPDATA%\StarRuptureSaveFixer\settings.json
```

FTP passwords are encrypted per-user for security (DPAPI on Windows, local AES key file on non-Windows).

Diagnostic logs are stored at:
```
%LOCALAPPDATA%\SRSM\YYYY-MM-DD_HH-mm-ss.log
```

Log files contain detailed information about all operations performed by the application, including:
- FTP/SFTP connection attempts and file transfers
- Save file loading, processing, and saving operations
- Application errors and warnings
- Session management activities

**Note**: Passwords are never logged for security. Log files are created per session and include timestamps for easy troubleshooting.

## Contributing

Contributions are welcome! If you encounter a new type of save file corruption:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/new-fixer`)
3. Implement a new `IFixer` class for the issue
4. Submit a Pull Request

## Disclaimer

**Always backup your save files before using this tool.** While the application creates automatic backups, unexpected issues may occur. Use at your own risk.

## Credits

- **Author:** AlienX
- **Website:** [StarRupture-Utilities.com](https://starrupture-utilities.com)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

*This tool is not affiliated with the developers of Star Rupture.*

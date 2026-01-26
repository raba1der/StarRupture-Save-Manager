# Logging Implementation Summary

## Overview
Comprehensive logging has been added to Star Rupture Save Manager (SRSM) that writes to `%LOCALAPPDATA%\SRSM\YYYY-MM-DD_HH-mm-ss.log`.

## Log File Location
- **Directory**: `%LOCALAPPDATA%\SRSM\`
- **Filename Format**: `2024-01-15_14-30-45.log`
- **One log file per application session**

## What Gets Logged

### Application Lifecycle
- Application startup with version, OS, and .NET information
- Session ID for tracking
- Unhandled exceptions
- Application shutdown

### FTP/FTPS/SFTP Operations
- Connection attempts (host, port, protocol, username)
- Connection results (success/failure with reasons)
- File uploads and downloads
- Directory listings
- Progress and completion status
- **Passwords are NEVER logged**

### File Operations
- Save file loading and saving
- File sizes and paths (sanitized for privacy)
- Compression/decompression operations
- Backup file creation

### Settings Management
- Settings loading and saving
- Password encryption/decryption (without logging actual passwords)
- Configuration changes

### Error Handling
- Exception details with type, message, and stack traces
- Inner exceptions
- Operation context

## Log Entry Format
```
[2024-01-15 14:30:45.123] [INFO ] [T001] [Context] Message
[Timestamp] [Level] [ThreadID] [Component] Description
```

### Log Levels
- **INFO**: Normal operations
- **WARN**: Warnings and cancelled operations  
- **ERROR**: Errors with exception details
- **DEBUG**: Debug information (only in DEBUG builds)
- **FTP**: FTP/SFTP specific operations
- **FILE**: File operation tracking

## Privacy & Security

### What IS Logged
? Hostnames and ports
? Usernames
? File paths (sanitized - only filename and parent folder)
? File sizes
? Operation results
? Error messages

### What is NOT Logged
? Passwords (FTP/SFTP/etc.)
? Full file paths (privacy)
? Authentication tokens
? Encrypted password data

## Security Features
- **Password Sanitization**: Automatic regex-based removal of passwords from any log message
- **Token Sanitization**: Removes authentication tokens
- **Path Sanitization**: Only logs `...\ParentFolder\filename` instead of full paths

## Implementation Details

### LoggingService (Singleton)
- Thread-safe with lock synchronization
- Automatic initialization on first use
- Session ID tracking
- Automatic log file creation
- Exception-safe (logging failures don't crash the app)

### Integration Points
1. **App.xaml.cs**: Global exception handler and application lifecycle
2. **FtpService**: All FTP operations
3. **SftpService**: All SFTP operations
4. **SaveFileService**: Save file compression/decompression
5. **SettingsService**: Configuration management

## Usage Examples

### In Code
```csharp
private readonly LoggingService _logger = LoggingService.Instance;

// Log info
_logger.LogInfo("Operation completed successfully", "ComponentName");

// Log warning
_logger.LogWarning("Configuration file not found, using defaults", "Settings");

// Log error with exception
_logger.LogError("Failed to connect", exception, "FtpService");

// Log FTP operation
_logger.LogFtpOperation("Upload", host, port, protocol, "File: test.sav");

// Log file operation
_logger.LogFileOperation("Load", filePath, fileSize);
```

## Sample Log Output
```
[2024-01-15 14:30:45.001] [INFO ] [T001] === Star Rupture Save Manager - Logging Started ===
[2024-01-15 14:30:45.002] [INFO ] [T001] Session ID: a3f8c91d
[2024-01-15 14:30:45.003] [INFO ] [T001] Application Version: 1.0.8
[2024-01-15 14:30:45.004] [INFO ] [T001] OS: Microsoft Windows NT 10.0.22631.0
[2024-01-15 14:30:45.005] [INFO ] [T001] .NET Version: 8.0.1
[2024-01-15 14:30:47.123] [INFO ] [T001] [SettingsService] Loading application settings
[2024-01-15 14:30:47.456] [FTP  ] [T005] FTP Operation: TestConnection | Host: example.com:22 | Protocol: SFTP | Username: user
[2024-01-15 14:30:48.789] [INFO ] [T005] [FtpService] SFTP connection successful to example.com:22
[2024-01-15 14:30:50.123] [FILE ] [T008] File Operation: LoadSaveFile | Path: ...\Session1\AutoSave0.sav
[2024-01-15 14:30:50.456] [INFO ] [T008] [SaveFileService] Decompressed JSON content: 15432 characters
```

## Benefits
1. **Troubleshooting**: Detailed logs help diagnose issues
2. **Auditing**: Track what operations were performed
3. **Performance**: Identify slow operations
4. **Privacy**: Sensitive data is automatically sanitized
5. **Thread-Safe**: Works correctly in multi-threaded WPF application
6. **Non-Intrusive**: Logging failures don't affect app functionality

## Maintenance
- Log files are not automatically deleted
- Users can manually delete old logs from `%LOCALAPPDATA%\SRSM\`
- Each session creates a new log file (prevents file locking issues)
- File size is typically small (a few KB to a few MB per session)

## Future Enhancements
- [ ] Add log rotation (delete logs older than X days)
- [ ] Add configurable log levels
- [ ] Add log viewer in the application
- [ ] Export logs for bug reports

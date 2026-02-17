# Avalonia Parity Checklist

Use this checklist to validate behavior parity between the legacy WPF UI and the Avalonia UI.

## Scope

- Project: `src/StarRuptureSaveManager.Avalonia/StarRuptureSaveManager.Avalonia.csproj`
- Core under test: `StarRuptureSaveManager.Core.csproj`
- Goal: same user-visible outcomes for save/session/FTP/settings workflows.

## Test Matrix

1. Environment
- Linux desktop
- Windows desktop
- At least one profile with no saves, one with valid saves, one with corrupted/edge-case saves.

2. Save Browser
- Refresh session list.
- Load save file and run `Fix Drones`.
- Load save file and run `Remove All Drones`.
- Confirm original file is backed up and fixed file is written.
- Confirm backup naming avoids collisions when backup already exists.
- Confirm backup files cannot be re-processed accidentally.

3. Session Manager
- Copy save between two non-root sessions.
- Create session with unique name.
- Attempt create with duplicate name.
- Delete non-root session.
- Confirm delete requires explicit confirmation control.
- Confirm root session cannot be deleted.

4. FTP Sync
- Test connection for FTP / FTPS / SFTP.
- Save and reload FTP settings (including password decrypt/reload).
- Upload selected file as remote target filename.
- Download remote filename into selected local session.
- Confirm overwrite safeguards:
  - Upload blocks when remote file exists unless overwrite is enabled.
  - Download blocks when local file exists unless overwrite is enabled.
- Confirm invalid remote filename (`/` or `\`) is rejected.

5. Settings
- Auto-detected path visible.
- Save custom path when valid.
- Reject nonexistent custom path.
- Reset to auto and save.
- Confirm custom path updates Save Browser / Session Manager / FTP tabs.

## Execution Notes

- Record pass/fail with date, OS, and commit hash.
- For every failure, capture expected vs actual behavior and file a bug with repro steps.

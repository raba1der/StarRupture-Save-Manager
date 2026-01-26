using FluentFTP;
using StarRuptureSaveFixer.Models;
using System.IO;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StarRuptureSaveFixer.Services;

public partial class FtpService
{
    private readonly SftpService _sftpService = new();
    private readonly LoggingService _logger = LoggingService.Instance;

    private bool IsSftp(FtpSettings settings) => settings.Protocol == FileTransferProtocol.SFTP;

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        FtpSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        var protocolName = settings.Protocol.ToString();
        _logger.LogFtpOperation("TestConnection", settings.Host, settings.Port, protocolName, $"Username: {settings.Username}");

        if (IsSftp(settings))
        {
            var result = await _sftpService.TestConnectionAsync(settings, password, cancellationToken);
            _logger.LogInfo($"SFTP connection test result: {result.Success} - {result.Message}", "FtpService");
            return result;
        }

        try
        {
            using var client = CreateClient(settings, password);
            await client.Connect(cancellationToken).ConfigureAwait(false);

            if (client.IsConnected)
            {
                await client.Disconnect(cancellationToken).ConfigureAwait(false);
                _logger.LogInfo($"FTP connection successful to {settings.Host}:{settings.Port}", "FtpService");
                return (true, "Connection successful!");
            }

            _logger.LogWarning($"FTP connection failed to {settings.Host}:{settings.Port}", "FtpService");
            return (false, "Failed to connect to server.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"FTP connection cancelled to {settings.Host}:{settings.Port}", "FtpService");
            return (false, "Connection cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"FTP connection error to {settings.Host}:{settings.Port}", ex, "FtpService");
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    public async Task<bool> UploadFileAsync(
        string localPath,
        string remotePath,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null)
    {
        if (IsSftp(settings))
            return await _sftpService.UploadFileAsync(localPath, remotePath, settings, password, progress);

        try
        {
            using var client = CreateClient(settings, password);

            progress?.Report((0, "Connecting..."));
            await client.Connect().ConfigureAwait(false);

            if (!client.IsConnected)
            {
                progress?.Report((0, "Failed to connect"));
                return false;
            }

            var fileName = Path.GetFileName(localPath);
            var fullRemotePath = CombineRemotePath(remotePath, fileName);

            progress?.Report((10, $"Uploading {fileName}..."));

            // Set up progress tracking
            client.Config.TransferChunkSize = 65536;

            var result = await client.UploadFile(
                localPath,
                fullRemotePath,
                FtpRemoteExists.NoCheck,
                createRemoteDir: true,
                progress: new Progress<FtpProgress>(p =>
                {
                    var percent = 10 + (int)(p.Progress * 0.85); //10-95%
                    progress?.Report((percent, $"Uploading {fileName}... {p.Progress:F0}%"));
                })).ConfigureAwait(false);

            await client.Disconnect().ConfigureAwait(false);

            if (result == FtpStatus.Success)
            {
                progress?.Report((100, "Upload complete!"));
                return true;
            }

            progress?.Report((0, $"Upload failed: {result}"));
            return false;
        }
        catch (Exception ex)
        {
            progress?.Report((0, $"Upload error: {ex.Message}"));
            return false;
        }
    }

    public async Task<(bool Success, int UploadedCount, int FailedCount)> UploadFilesAsync(
        IEnumerable<string> localPaths,
        string remotePath,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null)
    {
        if (IsSftp(settings))
            return await _sftpService.UploadFilesAsync(localPaths, remotePath, settings, password, progress);

        var files = localPaths.ToList();
        if (files.Count == 0)
            return (true, 0, 0);

        int uploaded = 0;
        int failed = 0;

        try
        {
            using var client = CreateClient(settings, password);

            progress?.Report((0, "Connecting..."));
            await client.Connect().ConfigureAwait(false);

            if (!client.IsConnected)
            {
                progress?.Report((0, "Failed to connect"));
                return (false, 0, files.Count);
            }

            for (int i = 0; i < files.Count; i++)
            {
                var localPath = files[i];
                var fileName = Path.GetFileName(localPath);
                var fullRemotePath = CombineRemotePath(remotePath, fileName);

                var basePercent = (int)((double)i / files.Count * 100);
                progress?.Report((basePercent, $"Uploading {fileName} ({i + 1}/{files.Count})..."));

                try
                {
                    var result = await client.UploadFile(
                        localPath,
                        fullRemotePath,
                        FtpRemoteExists.Overwrite,
                        createRemoteDir: true).ConfigureAwait(false);

                    if (result == FtpStatus.Success)
                        uploaded++;
                    else
                        failed++;
                }
                catch
                {
                    failed++;
                }
            }

            await client.Disconnect().ConfigureAwait(false);

            var finalStatus = failed == 0
                ? $"All {uploaded} files uploaded successfully!"
                : $"Uploaded {uploaded} files, {failed} failed.";

            progress?.Report((100, finalStatus));
            return (failed == 0, uploaded, failed);
        }
        catch (Exception ex)
        {
            progress?.Report((0, $"Upload error: {ex.Message}"));
            return (false, uploaded, failed + (files.Count - uploaded - failed));
        }
    }

    public async Task<List<FtpDirectoryItem>> ListRemoteDirectoryAsync(
        string remotePath,
        FtpSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        _logger.LogFtpOperation("ListDirectory", settings.Host, settings.Port, settings.Protocol.ToString(), $"Path: {remotePath}");

        if (IsSftp(settings))
        {
            var result = await _sftpService.ListRemoteDirectoryAsync(remotePath, settings, password, cancellationToken);
            _logger.LogInfo($"SFTP directory listing returned {result.Count} items", "FtpService");
            return result;
        }

        var items = new List<FtpDirectoryItem>();

        try
        {
            using var client = CreateClient(settings, password);
            await client.Connect(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!client.IsConnected)
            {
                _logger.LogWarning("FTP client not connected for directory listing", "FtpService");
                return items;
            }

            var listing = await client.GetListing(remotePath, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in listing)
            {
                items.Add(new FtpDirectoryItem
                {
                    Name = item.Name,
                    FullPath = item.FullName,
                    IsDirectory = item.Type == FtpObjectType.Directory,
                    Size = item.Size,
                    Modified = item.Modified
                });
            }

            await client.Disconnect(cancellationToken).ConfigureAwait(false);
            _logger.LogInfo($"FTP directory listing returned {items.Count} items from {remotePath}", "FtpService");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("FTP directory listing cancelled", "FtpService");
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError($"FTP directory listing error for {remotePath}", ex, "FtpService");
            // Return empty list on other errors
        }

        return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name).ToList();
    }

    public async Task<bool> UploadFileAsAsync(
        string localPath,
        string remotePath,
        string remoteFileName,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogFtpOperation("UploadFileAs", settings.Host, settings.Port, settings.Protocol.ToString(), 
   $"Remote: {remotePath}/{remoteFileName}");
        _logger.LogFileOperation("Upload", localPath, new FileInfo(localPath).Length);

        if (IsSftp(settings))
     {
      var result = await _sftpService.UploadFileAsAsync(localPath, remotePath, remoteFileName, settings, password, progress, cancellationToken);
    _logger.LogInfo($"SFTP upload result: {result}", "FtpService");
            return result;
        }

        try
 {
      using var client = CreateClient(settings, password);

        progress?.Report((0, "Connecting..."));
  await client.Connect(cancellationToken).ConfigureAwait(false);

     cancellationToken.ThrowIfCancellationRequested();

   if (!client.IsConnected)
   {
    progress?.Report((0, "Failed to connect"));
     _logger.LogWarning("FTP client failed to connect during upload", "FtpService");
    return false;
  }

  var fullRemotePath = CombineRemotePath(remotePath, remoteFileName);

   progress?.Report((10, $"Uploading as {remoteFileName}..."));

   client.Config.TransferChunkSize = 65536;

            var result = await client.UploadFile(
      localPath,
      fullRemotePath,
  FtpRemoteExists.NoCheck,
     createRemoteDir: true,
    progress: new Progress<FtpProgress>(p =>
   {
      if (!cancellationToken.IsCancellationRequested)
 {
      var percent = 10 + (int)(p.Progress * 0.85);
       progress?.Report((percent, $"Uploading as {remoteFileName}... {p.Progress:F0}%"));
          }
   }),
   token: cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

            await client.Disconnect(cancellationToken).ConfigureAwait(false);

      if (result == FtpStatus.Success)
       {
       progress?.Report((100, $"Successfully uploaded as {remoteFileName}!"));
           _logger.LogInfo($"FTP upload successful: {remoteFileName}", "FtpService");
    return true;
   }

  progress?.Report((0, $"Upload failed: {result}"));
            _logger.LogWarning($"FTP upload failed: {result}", "FtpService");
   return false;
     }
        catch (OperationCanceledException)
        {
   progress?.Report((0, "Upload cancelled."));
    _logger.LogWarning("FTP upload cancelled", "FtpService");
 throw;
        }
        catch (Exception ex)
        {
      progress?.Report((0, $"Upload error: {ex.Message}"));
    _logger.LogError("FTP upload error", ex, "FtpService");
    return false;
        }
    }

    public async Task<bool> RemotePathExistsAsync(
        string remotePath,
        FtpSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (IsSftp(settings))
            return await _sftpService.RemotePathExistsAsync(remotePath, settings, password, cancellationToken);

        try
        {
            using var client = CreateClient(settings, password);
            await client.Connect(cancellationToken).ConfigureAwait(false);

            if (!client.IsConnected)
                return false;

            // FluentFTP provides a DirectoryExists async method
            var exists = await client.DirectoryExists(remotePath, cancellationToken).ConfigureAwait(false);

            await client.Disconnect(cancellationToken).ConfigureAwait(false);
            return exists;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadDirectoryAsync(
        string remotePath,
        string localPath,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsSftp(settings))
            return await _sftpService.DownloadDirectoryAsync(remotePath, localPath, settings, password, progress, cancellationToken);

        try
        {
            using var client = CreateClient(settings, password);

            progress?.Report((0, "Connecting..."));
            await client.Connect(cancellationToken).ConfigureAwait(false);

            if (!client.IsConnected)
            {
                progress?.Report((0, "Failed to connect"));
                return false;
            }

            // Ensure local directory exists
            Directory.CreateDirectory(localPath);

            // Get listing for the remote directory
            var listing = await client.GetListing(remotePath, cancellationToken).ConfigureAwait(false);

            var entries = listing.Where(i => i.Type == FtpObjectType.File || i.Type == FtpObjectType.Directory).ToList();
            int total = entries.Count;
            int completed = 0;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Type == FtpObjectType.File)
                {
                    var remoteFile = entry.FullName;
                    var localFile = Path.Combine(localPath, entry.Name);

                    progress?.Report(((int)((double)completed / Math.Max(1, total) * 100), $"Downloading {entry.Name}..."));

                    var result = await client.DownloadFile(localFile, remoteFile, FtpLocalExists.Overwrite, token: cancellationToken).ConfigureAwait(false);

                    // FluentFTP returns a bool for DownloadFile; treat false as failure
                    if (result != FtpStatus.Success)
                    {
                        // continue but report
                    }

                    completed++;
                }
                else if (entry.Type == FtpObjectType.Directory)
                {
                    var subRemote = entry.FullName;
                    var subLocal = Path.Combine(localPath, entry.Name);
                    Directory.CreateDirectory(subLocal);

                    // Recurse into subdirectory
                    await DownloadDirectoryAsync(subRemote, subLocal, settings, password, progress, cancellationToken).ConfigureAwait(false);
                    completed++;
                }
            }

            await client.Disconnect(cancellationToken).ConfigureAwait(false);
            progress?.Report((100, "Download complete"));
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress?.Report((0, $"Download error: {ex.Message}"));
            return false;
        }
    }

    public async Task<bool> DownloadFileAsync(
        string remoteFilePath,
        string localFilePath,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogFtpOperation("DownloadFile", settings.Host, settings.Port, settings.Protocol.ToString(), 
  $"Remote: {remoteFilePath}");
        _logger.LogFileOperation("Download", localFilePath);

        if (IsSftp(settings))
        {
   var result = await _sftpService.DownloadFileAsync(remoteFilePath, localFilePath, settings, password, progress, cancellationToken);
   _logger.LogInfo($"SFTP download result: {result}", "FtpService");
            return result;
 }

      try
     {
     using var client = CreateClient(settings, password);

   progress?.Report((0, "Connecting..."));
  await client.Connect(cancellationToken).ConfigureAwait(false);

        if (!client.IsConnected)
 {
    progress?.Report((0, "Failed to connect"));
     _logger.LogWarning("FTP client failed to connect during download", "FtpService");
  return false;
    }

      // Ensure local directory exists
    var localDir = Path.GetDirectoryName(localFilePath);
     if (!string.IsNullOrEmpty(localDir))
    Directory.CreateDirectory(localDir);

     client.Config.TransferChunkSize = 65536;

    var result = await client.DownloadFile(
     localFilePath,
    remoteFilePath,
   FtpLocalExists.Overwrite,
      progress: new Progress<FtpProgress>(p =>
   {
var percent = (int)(p.Progress);
      progress?.Report((percent, $"Downloading {Path.GetFileName(remoteFilePath)}... {percent}%"));
     }),
    token: cancellationToken).ConfigureAwait(false);

    await client.Disconnect(cancellationToken).ConfigureAwait(false);

    if (result == FtpStatus.Success || result == FtpStatus.Skipped)
      {
   progress?.Report((100, "Download complete"));
     _logger.LogInfo($"FTP download successful: {Path.GetFileName(remoteFilePath)}", "FtpService");
    _logger.LogFileOperation("Downloaded", localFilePath, new FileInfo(localFilePath).Length);
         return true;
  }

    progress?.Report((0, $"Download failed: {result}"));
       _logger.LogWarning($"FTP download failed: {result}", "FtpService");
   return false;
 }
        catch (OperationCanceledException)
        {
   _logger.LogWarning("FTP download cancelled", "FtpService");
  throw;
 }
catch (Exception ex)
     {
  progress?.Report((0, $"Download error: {ex.Message}"));
        _logger.LogError("FTP download error", ex, "FtpService");
    return false;
        }
    }

    private AsyncFtpClient CreateClient(FtpSettings settings, string password)
    {
        var client = new AsyncFtpClient(settings.Host, settings.Username, password, settings.Port);

        client.Config.EncryptionMode = settings.Protocol == FileTransferProtocol.FTPS
            ? FtpEncryptionMode.Explicit
            : FtpEncryptionMode.None;

        client.Config.DataConnectionType = settings.PassiveMode
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        // Reasonable timeouts
        client.Config.ConnectTimeout = 10000;
        client.Config.ReadTimeout = 30000;
        client.Config.DataConnectionConnectTimeout = 10000;

        return client;
    }

    private string CombineRemotePath(string basePath, string fileName)
    {
        if (string.IsNullOrEmpty(basePath) || basePath == "/")
            return "/" + fileName;

        basePath = basePath.TrimEnd('/');
        return basePath + "/" + fileName;
    }
}

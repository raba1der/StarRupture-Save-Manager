using Renci.SshNet;
using Renci.SshNet.Sftp;
using StarRuptureSaveFixer.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StarRuptureSaveFixer.Services;

public class SftpService
{
    private readonly LoggingService _logger = LoggingService.Instance;

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        FtpSettings settings,
  string password,
     CancellationToken cancellationToken = default)
    {
  _logger.LogFtpOperation("SFTP TestConnection", settings.Host, settings.Port, "SFTP", $"Username: {settings.Username}");

 return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(settings, password);

                cancellationToken.ThrowIfCancellationRequested();

                client.Connect();

                if (client.IsConnected)
                {
                    client.Disconnect();
                    return (true, "Connection successful!");
                }

                return (false, "Failed to connect to server.");
            }
            catch (OperationCanceledException)
            {
                return (false, "Connection cancelled.");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }, cancellationToken);
    }

    public async Task<bool> UploadFileAsync(
        string localPath,
      string remotePath,
        FtpSettings settings,
        string password,
     IProgress<(int Percent, string Status)>? progress = null,
      CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(settings, password);

                progress?.Report((0, "Connecting..."));
                client.Connect();

                cancellationToken.ThrowIfCancellationRequested();

                if (!client.IsConnected)
                {
                    progress?.Report((0, "Failed to connect"));
                    return false;
                }

                var fileName = Path.GetFileName(localPath);
                var fullRemotePath = CombineRemotePath(remotePath, fileName);

                progress?.Report((10, $"Uploading {fileName}..."));

                using var fileStream = File.OpenRead(localPath);
                var fileSize = fileStream.Length;
                long uploadedBytes = 0;

                client.UploadFile(fileStream, fullRemotePath, uploaded =>
                       {
                           if (!cancellationToken.IsCancellationRequested)
                           {
                               uploadedBytes = (long)uploaded;
                               var percent = fileSize > 0 ? (int)((uploadedBytes * 85) / fileSize) + 10 : 10;
                               var uploadProgress = fileSize > 0 ? (uploadedBytes * 100) / fileSize : 0;
                               progress?.Report((percent, $"Uploading {fileName}... {uploadProgress}%"));
                           }
                       });

                cancellationToken.ThrowIfCancellationRequested();

                client.Disconnect();

                progress?.Report((100, "Upload complete!"));
                return true;
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, "Upload cancelled."));
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report((0, $"Upload error: {ex.Message}"));
                return false;
            }
        }, cancellationToken);
    }

    public async Task<(bool Success, int UploadedCount, int FailedCount)> UploadFilesAsync(
   IEnumerable<string> localPaths,
    string remotePath,
    FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var files = localPaths.ToList();
            if (files.Count == 0)
                return (true, 0, 0);

            int uploaded = 0;
            int failed = 0;

            try
            {
                using var client = CreateClient(settings, password);

                progress?.Report((0, "Connecting..."));
                client.Connect();

                cancellationToken.ThrowIfCancellationRequested();

                if (!client.IsConnected)
                {
                    progress?.Report((0, "Failed to connect"));
                    return (false, 0, files.Count);
                }

                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var localPath = files[i];
                    var fileName = Path.GetFileName(localPath);
                    var fullRemotePath = CombineRemotePath(remotePath, fileName);

                    var basePercent = (int)((double)i / files.Count * 100);
                    progress?.Report((basePercent, $"Uploading {fileName} ({i + 1}/{files.Count})..."));

                    try
                    {
                        using var fileStream = File.OpenRead(localPath);
                        client.UploadFile(fileStream, fullRemotePath, true);
                        uploaded++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                client.Disconnect();

                var finalStatus = failed == 0
                     ? $"All {uploaded} files uploaded successfully!"
                    : $"Uploaded {uploaded} files, {failed} failed.";

                progress?.Report((100, finalStatus));
                return (failed == 0, uploaded, failed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report((0, $"Upload error: {ex.Message}"));
                return (false, uploaded, failed + (files.Count - uploaded - failed));
            }
        }, cancellationToken);
    }

    public async Task<List<FtpDirectoryItem>> ListRemoteDirectoryAsync(
        string remotePath,
        FtpSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
       {
           var items = new List<FtpDirectoryItem>();

           try
           {
               using var client = CreateClient(settings, password);
               client.Connect();

               cancellationToken.ThrowIfCancellationRequested();

               if (!client.IsConnected)
                   return items;

               var listing = client.ListDirectory(remotePath);

               cancellationToken.ThrowIfCancellationRequested();

               foreach (var item in listing)
               {
                   // Skip . and ..
                   if (item.Name == "." || item.Name == "..")
                       continue;

                   items.Add(new FtpDirectoryItem
                   {
                       Name = item.Name,
                       FullPath = item.FullName,
                       IsDirectory = item.IsDirectory,
                       Size = item.Length,
                       Modified = item.LastWriteTime
                   });
               }

               client.Disconnect();
           }
           catch (OperationCanceledException)
           {
               throw;
           }
           catch
           {
               // Return empty list on other errors
           }

           return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name).ToList();
       }, cancellationToken);
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
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(settings, password);

                progress?.Report((0, "Connecting..."));
                client.Connect();

                cancellationToken.ThrowIfCancellationRequested();

                if (!client.IsConnected)
                {
                    progress?.Report((0, "Failed to connect"));
                    return false;
                }

                var fullRemotePath = CombineRemotePath(remotePath, remoteFileName);

                progress?.Report((10, $"Uploading as {remoteFileName}..."));

                using var fileStream = File.OpenRead(localPath);
                var fileSize = fileStream.Length;
                long uploadedBytes = 0;

                client.UploadFile(fileStream, fullRemotePath, uploaded =>
             {
                 if (!cancellationToken.IsCancellationRequested)
                 {
                     uploadedBytes = (long)uploaded;
                     var percent = fileSize > 0 ? (int)((uploadedBytes * 85) / fileSize) + 10 : 10;
                     var uploadProgress = fileSize > 0 ? (uploadedBytes * 100) / fileSize : 0;
                     progress?.Report((percent, $"Uploading as {remoteFileName}... {uploadProgress}%"));
                 }
             });

                cancellationToken.ThrowIfCancellationRequested();

                client.Disconnect();

                progress?.Report((100, $"Successfully uploaded as {remoteFileName}!"));
                return true;
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, "Upload cancelled."));
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report((0, $"Upload error: {ex.Message}"));
                return false;
            }
        }, cancellationToken);
    }

    public async Task<bool> RemotePathExistsAsync(
        string remotePath,
        FtpSettings settings,
     string password,
     CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
      {
          try
          {
              using var client = CreateClient(settings, password);
              client.Connect();

              cancellationToken.ThrowIfCancellationRequested();

              if (!client.IsConnected)
                  return false;

              var exists = client.Exists(remotePath);

              client.Disconnect();
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
      }, cancellationToken);
    }

    public async Task<bool> DownloadDirectoryAsync(
   string remotePath,
        string localPath,
FtpSettings settings,
     string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(settings, password);

                progress?.Report((0, "Connecting..."));
                client.Connect();

                cancellationToken.ThrowIfCancellationRequested();

                if (!client.IsConnected)
                {
                    progress?.Report((0, "Failed to connect"));
                    return false;
                }

                // Ensure local directory exists
                Directory.CreateDirectory(localPath);

                // Get listing for the remote directory
                var listing = client.ListDirectory(remotePath)
                   .Where(i => i.Name != "." && i.Name != "..")
                     .ToList();

                int total = listing.Count;
                int completed = 0;

                foreach (var entry in listing)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsRegularFile)
                    {
                        var remoteFile = entry.FullName;
                        var localFile = Path.Combine(localPath, entry.Name);

                        progress?.Report(((int)((double)completed / Math.Max(1, total) * 100), $"Downloading {entry.Name}..."));

                        using var fileStream = File.Create(localFile);
                        client.DownloadFile(remoteFile, fileStream);

                        completed++;
                    }
                    else if (entry.IsDirectory)
                    {
                        var subRemote = entry.FullName;
                        var subLocal = Path.Combine(localPath, entry.Name);
                        Directory.CreateDirectory(subLocal);

                        // Recurse into subdirectory
                        DownloadDirectoryRecursive(client, subRemote, subLocal, cancellationToken);
                        completed++;
                    }
                }

                client.Disconnect();
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
        }, cancellationToken);
    }

    public async Task<bool> DownloadFileAsync(
        string remoteFilePath,
        string localFilePath,
        FtpSettings settings,
        string password,
        IProgress<(int Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
 {
     try
     {
         using var client = CreateClient(settings, password);

         progress?.Report((0, "Connecting..."));
         client.Connect();

         cancellationToken.ThrowIfCancellationRequested();

         if (!client.IsConnected)
         {
             progress?.Report((0, "Failed to connect"));
             return false;
         }

         // Ensure local directory exists
         var localDir = Path.GetDirectoryName(localFilePath);
         if (!string.IsNullOrEmpty(localDir))
             Directory.CreateDirectory(localDir);

         // Get file size for progress tracking
         var fileInfo = client.Get(remoteFilePath);
         var fileSize = fileInfo.Length;
         long downloadedBytes = 0;

         using var fileStream = File.Create(localFilePath);

         client.DownloadFile(remoteFilePath, fileStream, downloaded =>
                 {
                     if (!cancellationToken.IsCancellationRequested)
                     {
                         downloadedBytes = (long)downloaded;
                         var percent = fileSize > 0 ? (int)((downloadedBytes * 100) / fileSize) : 0;
                         progress?.Report((percent, $"Downloading {Path.GetFileName(remoteFilePath)}... {percent}%"));
                     }
                 });

         cancellationToken.ThrowIfCancellationRequested();

         client.Disconnect();

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
 }, cancellationToken);
    }

    private void DownloadDirectoryRecursive(SftpClient client, string remotePath, string localPath, CancellationToken cancellationToken)
    {
        var listing = client.ListDirectory(remotePath)
            .Where(i => i.Name != "." && i.Name != "..")
    .ToList();

        foreach (var entry in listing)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsRegularFile)
            {
                var remoteFile = entry.FullName;
                var localFile = Path.Combine(localPath, entry.Name);

                using var fileStream = File.Create(localFile);
                client.DownloadFile(remoteFile, fileStream);
            }
            else if (entry.IsDirectory)
            {
                var subRemote = entry.FullName;
                var subLocal = Path.Combine(localPath, entry.Name);
                Directory.CreateDirectory(subLocal);

                DownloadDirectoryRecursive(client, subRemote, subLocal, cancellationToken);
            }
        }
    }

    private SftpClient CreateClient(FtpSettings settings, string password)
    {
        var client = new SftpClient(settings.Host, settings.Port, settings.Username, password);

        // Set timeouts
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
        client.OperationTimeout = TimeSpan.FromSeconds(30);

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

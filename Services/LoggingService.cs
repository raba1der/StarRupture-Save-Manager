using System.IO;
using System.Text;

namespace StarRuptureSaveFixer.Services;

public class LoggingService
{
    private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
    private readonly string _logFilePath;
    private readonly object _lockObject = new();
    private readonly string _sessionId;

    public static LoggingService Instance => _instance.Value;

    private LoggingService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logFolder = Path.Combine(localAppData, "SRSM");

        if (!Directory.Exists(logFolder))
            Directory.CreateDirectory(logFolder);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFilePath = Path.Combine(logFolder, $"{timestamp}.log");
        _sessionId = Guid.NewGuid().ToString("N")[..8];

        // Write initial header
        WriteLogEntry("INFO", "=== Star Rupture Save Manager - Logging Started ===");
        WriteLogEntry("INFO", $"Session ID: {_sessionId}");
        WriteLogEntry("INFO", $"Application Version: {GetApplicationVersion()}");
        WriteLogEntry("INFO", $"OS: {Environment.OSVersion}");
        WriteLogEntry("INFO", $".NET Version: {Environment.Version}");
    }

    public void LogInfo(string message, string? context = null)
    {
        WriteLogEntry("INFO", message, context);
    }

    public void LogWarning(string message, string? context = null)
    {
        WriteLogEntry("WARN", message, context);
    }

    public void LogError(string message, Exception? exception = null, string? context = null)
    {
        var errorMessage = exception != null
      ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}"
  : message;

        WriteLogEntry("ERROR", errorMessage, context);

        if (exception != null)
        {
            WriteLogEntry("ERROR", $"StackTrace: {exception.StackTrace}", context);

            if (exception.InnerException != null)
            {
                WriteLogEntry("ERROR", $"Inner Exception: {exception.InnerException.GetType().Name} - {exception.InnerException.Message}", context);
            }
        }
    }

    public void LogDebug(string message, string? context = null)
    {
#if DEBUG
        WriteLogEntry("DEBUG", message, context);
#endif
    }

    public void LogFtpOperation(string operation, string host, int port, string protocol, string? additionalInfo = null)
    {
        var message = $"FTP Operation: {operation} | Host: {host}:{port} | Protocol: {protocol}";
        if (!string.IsNullOrEmpty(additionalInfo))
            message += $" | {additionalInfo}";

        WriteLogEntry("FTP", message);
    }

    public void LogFileOperation(string operation, string filePath, long? fileSize = null)
    {
        var message = $"File Operation: {operation} | Path: {SanitizePath(filePath)}";
        if (fileSize.HasValue)
            message += $" | Size: {FormatFileSize(fileSize.Value)}";

        WriteLogEntry("FILE", message);
    }

    private void WriteLogEntry(string level, string message, string? context = null)
    {
        try
        {
            lock (_lockObject)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var threadId = Environment.CurrentManagedThreadId;

                var logBuilder = new StringBuilder();
                logBuilder.Append($"[{timestamp}] ");
                logBuilder.Append($"[{level,-5}] ");
                logBuilder.Append($"[T{threadId:D3}] ");

                if (!string.IsNullOrEmpty(context))
                    logBuilder.Append($"[{context}] ");

                logBuilder.Append(SanitizeMessage(message));

                File.AppendAllText(_logFilePath, logBuilder.ToString() + Environment.NewLine);
            }
        }
        catch
        {
            // Silently fail - don't want logging to crash the app
        }
    }

    private string SanitizeMessage(string message)
    {
        // Remove potential password leaks from connection strings and error messages
        var sanitized = message;

        // Remove password patterns
        if (sanitized.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                @"password[:\s=]+[^\s,;]+",
                "password=***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Remove potential auth tokens
        if (sanitized.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                   @"token[:\s=]+[^\s,;]+",
               "token=***",
             System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    private string SanitizePath(string path)
    {
        // Only show filename and parent folder for privacy
        try
        {
            var fileName = Path.GetFileName(path);
            var parentDir = Path.GetFileName(Path.GetDirectoryName(path));
            return string.IsNullOrEmpty(parentDir) ? fileName : $"...\\{parentDir}\\{fileName}";
        }
        catch
        {
            return Path.GetFileName(path) ?? path;
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    public void Dispose()
    {
        WriteLogEntry("INFO", "=== Logging Session Ended ===");
    }
}

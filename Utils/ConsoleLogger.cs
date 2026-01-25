using System;
using System.Linq;

namespace StarRuptureSaveFixer.Utils;

/// <summary>
/// Provides logging events for UI consumption. Previously wrote to console; now raises events so WPF UI can display progress.
/// </summary>
public static class ConsoleLogger
{
    /// <summary>
    /// Raised for general log messages. Parameters: level, message
    /// </summary>
    public static event Action<string, string>? MessageLogged;

    /// <summary>
    /// Raised for progress/status updates intended for the UI's processing text field.
    /// </summary>
    public static event Action<string>? ProgressLogged;

    public static void Info(string message)
    {
        MessageLogged?.Invoke("INFO", message);
    }

    public static void Success(string message)
    {
        MessageLogged?.Invoke("SUCCESS", message);
    }

    public static void Warning(string message)
    {
        MessageLogged?.Invoke("WARNING", message);
    }

    public static void Error(string message)
    {
        MessageLogged?.Invoke("ERROR", message);
    }

    public static void Progress(string message)
    {
        // Progress messages also surface as general log messages with level "PROGRESS"
        //MessageLogged?.Invoke("PROGRESS", message);
        ProgressLogged?.Invoke(message);
    }

    /// <summary>
    /// Writes a plain message without any tag
    /// </summary>
    public static void Plain(string message)
    {
        MessageLogged?.Invoke("", message);
    }

    /// <summary>
    /// Writes a header with a separator line
    /// </summary>
    public static void Header(params string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return;

        // Find the longest line
        int maxLength = lines.Max(line => line?.Length ?? 0);

        foreach (string line in lines)
        {
            MessageLogged?.Invoke("HEADER", line);
        }

        MessageLogged?.Invoke("HEADER", new string('=', maxLength));
    }
}

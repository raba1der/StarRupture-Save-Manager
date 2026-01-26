using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarRuptureSaveFixer.Views;

public partial class FtpFolderBrowserDialog : Window
{
    private readonly FtpService _ftpService;
    private readonly FtpSettings _settings;
    private readonly string _password;
    private readonly string _initialPath;
    private string _currentPath = "/";
    private CancellationTokenSource? _cts;
    private bool _isLoading;

    private const string REMOTE_SAVEGAMES_PATH = "/StarRupture/Saved/SaveGames";

    public string SelectedPath { get; private set; } = "/";
    public ObservableCollection<FtpDirectoryItem> Items { get; } = new();

    public FtpFolderBrowserDialog(FtpSettings settings, string password, string initialPath = "/")
    {
        InitializeComponent();

        _ftpService = new FtpService();
        _settings = settings;
        _password = password;
        _initialPath = string.IsNullOrEmpty(initialPath) ? "/" : initialPath;

        FolderListBox.ItemsSource = Items;

        // Set window title based on protocol
        Title = GetProtocolDisplayName() + " - Browse Remote Folder";

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private string GetProtocolDisplayName()
    {
        return _settings.Protocol switch
        {
            FileTransferProtocol.FTP => "FTP",
            FileTransferProtocol.FTPS => "FTPS",
            FileTransferProtocol.SFTP => "SFTP",
            _ => "FTP"
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Don't block the dialog from showing - test connection after it's visible
        await Task.Yield(); // Allow UI to render

        // Test connection first before loading directory
        if (!await TestConnectionAsync())
        {
            StatusTextBlock.Text = $"Connection failed. Please check your {GetProtocolDisplayName()} settings.";
            SelectButton.IsEnabled = false;

            // Show error message and close
            await Task.Delay(100); // Brief delay to ensure message is visible
            DialogResult = false;
            Close();
            return;
        }

        // Try to auto-detect StarRupture save games path
        string pathToLoad = await TryDetectSaveGamesPathAsync();

        await LoadDirectoryAsync(pathToLoad);
    }

    private async Task<string> TryDetectSaveGamesPathAsync()
    {
        // If an initial path was explicitly provided, use it
        if (!string.IsNullOrEmpty(_initialPath) && _initialPath != "/")
            return _initialPath;

        Dispatcher.Invoke(() => StatusTextBlock.Text = "Detecting StarRupture save folder...");

        try
        {
            var exists = await _ftpService.RemotePathExistsAsync(REMOTE_SAVEGAMES_PATH, _settings, _password);
            if (exists)
            {
                Dispatcher.Invoke(() => StatusTextBlock.Text = "Found StarRupture save folder!");
                return REMOTE_SAVEGAMES_PATH;
            }
        }
        catch
        {
            // Silently fail and fall back to root
        }

        Dispatcher.Invoke(() => StatusTextBlock.Text = "Starting at root folder");
        return "/";
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CancelCurrentOperation();
    }

    private async Task<bool> TestConnectionAsync()
    {
        CancelCurrentOperation();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var protocolName = GetProtocolDisplayName();
        SetLoading(true, $"Testing {protocolName} connection...");
        StatusTextBlock.Text = $"Testing {protocolName} connection...";

        try
        {
            var (success, message) = await _ftpService.TestConnectionAsync(_settings, _password, token);

            if (token.IsCancellationRequested)
                return false;

            if (!success)
            {
                StatusTextBlock.Text = $"{protocolName} connection failed: {message}";
                MessageBox.Show($"Failed to connect to {protocolName} server:\n\n{message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            StatusTextBlock.Text = $"{protocolName} connection successful";
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Connection test cancelled";
            return false;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"{GetProtocolDisplayName()} connection error: {ex.Message}";
            MessageBox.Show($"Failed to connect to {GetProtocolDisplayName()} server:\n\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading, string message = "Loading...")
    {
        _isLoading = loading;
        LoadingOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        LoadingText.Text = message;
        UpButton.IsEnabled = !loading;
        RefreshButton.IsEnabled = !loading;
        FolderListBox.IsEnabled = !loading;
        CancelOperationButton.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

        if (loading)
        {
            SelectButton.IsEnabled = false;
        }
    }

    private void CancelCurrentOperation()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task LoadDirectoryAsync(string path)
    {
        CancelCurrentOperation();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var protocolName = GetProtocolDisplayName();
        SetLoading(true, $"Connecting to {protocolName} server...");
        StatusTextBlock.Text = "Connecting...";
        Items.Clear();

        try
        {
            var items = await _ftpService.ListRemoteDirectoryAsync(path, _settings, _password, token);

            if (token.IsCancellationRequested)
                return;

            // Update UI on main thread
            foreach (var item in items.Where(i => i.IsDirectory))
            {
                Items.Add(item);
            }

            _currentPath = path;
            CurrentPathTextBox.Text = path;
            SelectButton.IsEnabled = true;
            StatusTextBlock.Text = $"Found {Items.Count} folder(s)";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Operation cancelled";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void CancelOperationButton_Click(object sender, RoutedEventArgs e)
    {
        CancelCurrentOperation();
        SetLoading(false);
        StatusTextBlock.Text = "Operation cancelled";
    }

    private async void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        if (_currentPath == "/" || string.IsNullOrEmpty(_currentPath))
            return;

        var parentPath = GetParentPath(_currentPath);
        await LoadDirectoryAsync(parentPath);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        await LoadDirectoryAsync(_currentPath);
    }

    private async void FolderListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isLoading)
            return;

        if (FolderListBox.SelectedItem is FtpDirectoryItem item && item.IsDirectory)
        {
            await LoadDirectoryAsync(item.FullPath);
        }
    }

    private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection changed - could update status if needed
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        // If a folder is selected in the list, assume the user meant to pick that folder
        if (FolderListBox.SelectedItem is FtpDirectoryItem selectedItem && selectedItem.IsDirectory)
        {
            SelectedPath = selectedItem.FullPath;
        }
        else
        {
            // Otherwise use the folder currently being browsed
            SelectedPath = _currentPath;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelCurrentOperation();
        DialogResult = false;
        Close();
    }

    private static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return "/";

        path = path.TrimEnd('/');
        var lastSlash = path.LastIndexOf('/');

        if (lastSlash <= 0)
            return "/";

        return path.Substring(0, lastSlash);
    }
}

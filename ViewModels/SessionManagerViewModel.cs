using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace StarRuptureSaveFixer.ViewModels;

public class SessionManagerViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager;

    private SaveSession? _sourceSession;
    private SaveSession? _targetSession;
    private SaveFileInfo? _selectedSourceFile;
    private string _newSessionName = "";
    private string _statusMessage = "";

    public SessionManagerViewModel(string? customPath = null)
    {
        _sessionManager = new SessionManager();
        _sessionManager.CustomSavePath = customPath;

        Sessions = new ObservableCollection<SaveSession>();

        CopySaveCommand = new RelayCommand(CopySave, () => CanCopySave);
        DeleteSessionCommand = new RelayCommand(DeleteSession, () => CanDeleteSession);
        RefreshCommand = new RelayCommand(Refresh);

        Refresh();
    }

    public void UpdateCustomPath(string? customPath)
    {
        _sessionManager.CustomSavePath = customPath;
    }

    public ObservableCollection<SaveSession> Sessions { get; }

    public SaveSession? SourceSession
    {
        get => _sourceSession;
        set
        {
            if (SetProperty(ref _sourceSession, value))
            {
                SelectedSourceFile = null;
                OnPropertyChanged(nameof(SourceFiles));
                OnPropertyChanged(nameof(CanCopySave));
            }
        }
    }

    public SaveSession? TargetSession
    {
        get => _targetSession;
        set
        {
            if (SetProperty(ref _targetSession, value))
            {
                OnPropertyChanged(nameof(TargetFiles));
                OnPropertyChanged(nameof(CanCopySave));
            }
        }
    }

    public SaveFileInfo? SelectedSourceFile
    {
        get => _selectedSourceFile;
        set
        {
            if (SetProperty(ref _selectedSourceFile, value))
            {
                OnPropertyChanged(nameof(CanCopySave));
            }
        }
    }

    public IEnumerable<SaveFileInfo> SourceFiles =>
        SourceSession?.SaveFiles ?? Enumerable.Empty<SaveFileInfo>();

    public IEnumerable<SaveFileInfo> TargetFiles =>
        TargetSession?.SaveFiles ?? Enumerable.Empty<SaveFileInfo>();

    public string NewSessionName
    {
        get => _newSessionName;
        set
        {
            if (SetProperty(ref _newSessionName, value))
            {
                OnPropertyChanged(nameof(CanCreateSession));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool CanCreateSession => false; // Feature removed

    public bool CanCopySave =>
        SelectedSourceFile != null &&
        TargetSession != null &&
        SourceSession != TargetSession;

    public bool CanDeleteSession =>
        TargetSession != null &&
        !string.IsNullOrEmpty(TargetSession.Name); // Can't delete root

    public ICommand CopySaveCommand { get; }
    public ICommand DeleteSessionCommand { get; }
    public ICommand RefreshCommand { get; }

    public void Refresh()
    {
        var previousSource = SourceSession?.FullPath;
        var previousTarget = TargetSession?.FullPath;

        Sessions.Clear();

        foreach (var session in _sessionManager.GetAllSessions())
        {
            Sessions.Add(session);
        }

        // Try to restore selections
        if (previousSource != null)
            SourceSession = Sessions.FirstOrDefault(s => s.FullPath == previousSource);
        if (previousTarget != null)
            TargetSession = Sessions.FirstOrDefault(s => s.FullPath == previousTarget);

        StatusMessage = $"Found {Sessions.Count} session(s).";
    }

    private void CopySave()
    {
        if (SelectedSourceFile == null || TargetSession == null)
            return;

        var result = _sessionManager.CopySaveToSession(
            SelectedSourceFile.FullPath,
            TargetSession.FullPath);

        if (result)
        {
            StatusMessage = $"Copied '{SelectedSourceFile.FileName}' to '{TargetSession.DisplayName}'.";
            Refresh();
        }
        else
        {
            StatusMessage = "Failed to copy save file.";
        }
    }

    private void DeleteSession()
    {
        if (TargetSession == null || string.IsNullOrEmpty(TargetSession.Name))
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete session '{TargetSession.Name}'?\n\n" +
            $"This will permanently delete {TargetSession.SaveCount} save file(s).",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var sessionName = TargetSession.Name;
        var deleted = _sessionManager.DeleteSession(TargetSession.FullPath);

        if (deleted)
        {
            StatusMessage = $"Session '{sessionName}' deleted.";
            TargetSession = null;
            Refresh();
        }
        else
        {
            StatusMessage = "Failed to delete session.";
        }
    }

    private static bool IsValidFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        return !name.Any(c => invalidChars.Contains(c));
    }
}

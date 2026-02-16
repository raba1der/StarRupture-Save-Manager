using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class SessionManagerViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager = new();

    private SaveSession? _sourceSession;
    private SaveSession? _targetSession;
    private SaveFileInfo? _selectedSourceFile;
    private SaveSession? _selectedSessionForDelete;
    private string _newSessionName = "";
    private string _statusMessage = "";

    public SessionManagerViewModel()
    {
        Sessions = new ObservableCollection<SaveSession>();

        CopySaveCommand = new RelayCommand(CopySave, () => CanCopySave);
        CreateSessionCommand = new RelayCommand(CreateSession, () => CanCreateSession);
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
                ((RelayCommand)CopySaveCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public SaveSession? TargetSession
    {
        get => _targetSession;
        set
        {
            if (SetProperty(ref _targetSession, value))
                ((RelayCommand)CopySaveCommand).RaiseCanExecuteChanged();
        }
    }

    public SaveFileInfo? SelectedSourceFile
    {
        get => _selectedSourceFile;
        set
        {
            if (SetProperty(ref _selectedSourceFile, value))
                ((RelayCommand)CopySaveCommand).RaiseCanExecuteChanged();
        }
    }

    public SaveSession? SelectedSessionForDelete
    {
        get => _selectedSessionForDelete;
        set
        {
            if (SetProperty(ref _selectedSessionForDelete, value))
                ((RelayCommand)DeleteSessionCommand).RaiseCanExecuteChanged();
        }
    }

    public string NewSessionName
    {
        get => _newSessionName;
        set
        {
            if (SetProperty(ref _newSessionName, value))
                ((RelayCommand)CreateSessionCommand).RaiseCanExecuteChanged();
        }
    }

    public IEnumerable<SaveFileInfo> SourceFiles =>
        SourceSession?.SaveFiles ?? Enumerable.Empty<SaveFileInfo>();

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool CanCopySave =>
        SelectedSourceFile != null &&
        TargetSession != null &&
        SourceSession != null &&
        SourceSession.FullPath != TargetSession.FullPath;

    public bool CanCreateSession => !string.IsNullOrWhiteSpace(NewSessionName);
    public bool CanDeleteSession => SelectedSessionForDelete != null && SelectedSessionForDelete.Name != "";

    public ICommand CopySaveCommand { get; }
    public ICommand CreateSessionCommand { get; }
    public ICommand DeleteSessionCommand { get; }
    public ICommand RefreshCommand { get; }

    public void Refresh()
    {
        var previousSource = SourceSession?.FullPath;
        var previousTarget = TargetSession?.FullPath;

        Sessions.Clear();
        foreach (var session in _sessionManager.GetAllSessions())
            Sessions.Add(session);

        if (!string.IsNullOrWhiteSpace(previousSource))
            SourceSession = Sessions.FirstOrDefault(s => s.FullPath == previousSource);
        if (!string.IsNullOrWhiteSpace(previousTarget))
            TargetSession = Sessions.FirstOrDefault(s => s.FullPath == previousTarget);

        StatusMessage = $"Found {Sessions.Count} session(s).";
        ((RelayCommand)DeleteSessionCommand).RaiseCanExecuteChanged();
    }

    private void CopySave()
    {
        if (SelectedSourceFile == null || TargetSession == null)
            return;

        var result = _sessionManager.CopySaveToSession(SelectedSourceFile.FullPath, TargetSession.FullPath);
        StatusMessage = result
            ? $"Copied '{SelectedSourceFile.FileName}' to '{TargetSession.DisplayName}'."
            : "Failed to copy save file.";
        Refresh();
    }

    private void CreateSession()
    {
        var trimmed = NewSessionName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        var created = _sessionManager.CreateSession(trimmed);
        StatusMessage = created != null
            ? $"Session '{trimmed}' created."
            : $"Failed to create session '{trimmed}' (already exists or invalid root).";

        NewSessionName = "";
        Refresh();
    }

    private void DeleteSession()
    {
        if (SelectedSessionForDelete == null)
            return;

        var session = SelectedSessionForDelete;
        var ok = _sessionManager.DeleteSession(session.FullPath);
        StatusMessage = ok
            ? $"Session '{session.DisplayName}' deleted."
            : $"Failed to delete '{session.DisplayName}'.";

        SelectedSessionForDelete = null;
        Refresh();
    }
}

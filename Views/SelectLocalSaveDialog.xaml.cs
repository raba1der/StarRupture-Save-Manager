using StarRuptureSaveFixer.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace StarRuptureSaveFixer.Views;

public partial class SelectLocalSaveDialog : Window
{
    public ObservableCollection<SaveFileInfo> Files { get; } = new();

    public SaveFileInfo? SelectedFile => FilesListBox.SelectedItem as SaveFileInfo;

    // Accept sessions so we can derive SessionName from parent
    public SelectLocalSaveDialog(IEnumerable<SaveSession> sessions)
    {
        InitializeComponent();
        FilesListBox.ItemsSource = Files;

        foreach (var s in sessions)
        {
            foreach (var f in s.SaveFiles)
            {
                // Ensure SessionName is set from the session
                f.SessionName = s.Name;
                Files.Add(f);
            }
        }

        FilesListBox.SelectionChanged += FilesListBox_SelectionChanged;
    }

    private void FilesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = FilesListBox.SelectedItem != null;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
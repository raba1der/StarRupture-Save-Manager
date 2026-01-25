using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;

namespace StarRuptureSaveFixer.Views;

public partial class SaveBrowserView : UserControl
{
    public SaveBrowserView()
    {
        InitializeComponent();
        this.DataContextChanged += SaveBrowserView_DataContextChanged;
    }

    private void SaveBrowserView_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldNpc)
        {
            oldNpc.PropertyChanged -= DataContext_PropertyChanged;
        }
        if (e.NewValue is INotifyPropertyChanged newNpc)
        {
            newNpc.PropertyChanged += DataContext_PropertyChanged;
        }
    }

    private void DataContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "LogOutput")
        {
            // Ensure UI update on UI thread
            Dispatcher.Invoke(() =>
            {
                // Move caret to end and scroll
                LogTextBox.CaretIndex = LogTextBox.Text.Length;
                LogTextBox.ScrollToEnd();
            });
        }
    }
}

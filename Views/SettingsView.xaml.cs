using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace StarRuptureSaveFixer.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Ignore errors opening browser
        }
    }
}

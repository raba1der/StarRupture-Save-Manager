using StarRuptureSaveFixer.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace StarRuptureSaveFixer.Views;

public partial class FtpDownloadView : UserControl
{
 public FtpDownloadView()
 {
 InitializeComponent();
 Loaded += OnLoaded;
 }

 private void OnLoaded(object sender, RoutedEventArgs e)
 {
 // Set password from ViewModel after loading (can't bind PasswordBox directly)
 if (DataContext is FtpDownloadViewModel vm && !string.IsNullOrEmpty(vm.Password))
 {
 PasswordBox.Password = vm.Password;
 }
 }

 private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
 {
 if (DataContext is FtpDownloadViewModel vm)
 {
 vm.Password = PasswordBox.Password;
 }
 }
}

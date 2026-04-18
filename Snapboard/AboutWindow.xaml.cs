using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Snapboard.Helpers;

namespace Snapboard;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        VersionText.Text = $"Version {VersionInfo.Display}";
    }

    private void OnHyperlinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Swallow — worst case the user just can't click the link.
        }
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

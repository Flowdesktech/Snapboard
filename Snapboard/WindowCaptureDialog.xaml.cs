using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Snapboard.Helpers;

namespace Snapboard;

/// <summary>
/// PicPick-style compact window picker: a single dropdown listing every
/// visible top-level window. Selecting one and clicking Capture (or pressing
/// Enter) closes the dialog and returns the chosen handle — <see cref="App"/>
/// does the actual capture + clipboard + toast so the UI stays snappy.
/// </summary>
public partial class WindowCaptureDialog : Window
{
    private readonly ObservableCollection<WindowRow> _rows = new();

    /// <summary>The handle the user picked, or null on cancel.</summary>
    public WindowEnumerator.WindowInfo? PickedWindow { get; private set; }

    public WindowCaptureDialog()
    {
        InitializeComponent();
        WindowCombo.ItemsSource = _rows;
    }

    // ---------------------------------------------------------- Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DarkTitleBar.Apply(this);
        Reload();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------- Population

    private void Reload()
    {
        _rows.Clear();
        CaptureButton.IsEnabled = false;
        StatusText.Text = "Loading open windows…";

        var dispatcher = Dispatcher;
        System.Threading.Tasks.Task.Run(() =>
        {
            List<WindowRow> rows;
            try
            {
                var infos = WindowEnumerator.EnumerateTopLevelWindows(includeMinimized: true);
                rows = new List<WindowRow>(infos.Count);
                foreach (var info in infos)
                {
                    BitmapSource? icon = null;
                    try
                    {
                        if (info.Icon != null)
                        {
                            icon = Imaging.CreateBitmapSourceFromHIcon(
                                info.Icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            icon.Freeze();
                        }
                    }
                    catch { }

                    string processLabel = string.IsNullOrEmpty(info.ProcessName)
                        ? ""
                        : info.ProcessName + (info.IsMinimized ? " · minimized" : "");

                    rows.Add(new WindowRow(info, info.Title, processLabel, icon));
                }
            }
            catch (Exception ex)
            {
                dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Could not list windows: " + ex.Message;
                });
                return;
            }

            dispatcher.Invoke(() =>
            {
                _rows.Clear();
                foreach (var row in rows) _rows.Add(row);

                if (_rows.Count == 0)
                {
                    StatusText.Text = "No matching windows are currently open.";
                    CaptureButton.IsEnabled = false;
                    return;
                }

                StatusText.Text = _rows.Count == 1 ? "1 window found." : $"{_rows.Count} windows found.";
                WindowCombo.SelectedIndex = 0;
                WindowCombo.Focus();
            });
        });
    }

    // ---------------------------------------------------------- Handlers

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CaptureButton.IsEnabled = WindowCombo.SelectedItem is WindowRow;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Reload();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if (WindowCombo.SelectedItem is not WindowRow row) return;
        PickedWindow = row.Info;
        DialogResult = true;
        Close();
    }

    // ---------------------------------------------------------- ViewModel

    public sealed class WindowRow
    {
        public WindowEnumerator.WindowInfo Info { get; }
        public string Title { get; }
        public string ProcessLabel { get; }
        public BitmapSource? IconSource { get; }

        public WindowRow(WindowEnumerator.WindowInfo info, string title,
            string processLabel, BitmapSource? iconSource)
        {
            Info = info;
            Title = title;
            ProcessLabel = processLabel;
            IconSource = iconSource;
        }
    }
}

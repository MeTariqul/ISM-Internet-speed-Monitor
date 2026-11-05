using System.Windows;

namespace SpeedMonitor;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        string version = (typeof(AboutWindow).Assembly.GetName().Version ?? new System.Version(1, 0, 0)).ToString(3);
        VersionText.Text = $"Version {version}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}

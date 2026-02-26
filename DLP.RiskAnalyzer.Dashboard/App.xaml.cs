using System.Windows;

namespace DLP.RiskAnalyzer.Dashboard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Show login window first
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}


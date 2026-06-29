using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using DLP.RiskAnalyzer.Dashboard.Constants;
using DLP.RiskAnalyzer.Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Dashboard;

public partial class LoginWindow : Window
{
    private readonly ILogger<LoginWindow> _logger;
    private readonly string _apiBaseUrl;
    private static string? _authToken;

    public static string? AuthToken => _authToken;

    public LoginWindow()
    {
        InitializeComponent();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddDebug();
            // builder.AddConsole(); // Optional if needed
        });
        _logger = loggerFactory.CreateLogger<LoginWindow>();

        // Load configuration
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(appDirectory, UIConstants.Configuration.ConfigFileName);
        
        _logger.LogInformation("App directory: {AppDir}", appDirectory);
        _logger.LogInformation("Config file exists: {Exists}", File.Exists(configPath));
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appDirectory)
            .AddJsonFile(UIConstants.Configuration.ConfigFileName, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Try to get API URL from config
        var apiUrlFromConfig = configuration["ApiBaseUrl"];
        _logger.LogInformation("ApiBaseUrl from config: {Url}", apiUrlFromConfig ?? "NULL");
        
        _apiBaseUrl = apiUrlFromConfig ?? UIConstants.DefaultApiBaseUrl;
        
        _logger.LogInformation("Final API Base URL: {Url}", _apiBaseUrl);
        
        // If config file doesn't exist, show a warning
        if (!File.Exists(configPath))
        {
            _logger.LogWarning("appsettings.json not found! Using default: {Url}", _apiBaseUrl);
        }
        
        ApiClient.Initialize(_apiBaseUrl);

        // Focus on username field
        Loaded += (s, e) => UsernameTextBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError(UIConstants.ValidationMessages.UsernameRequired);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError(UIConstants.ValidationMessages.PasswordRequired);
            return;
        }

        LoginButton.IsEnabled = false;
        ErrorMessageText.Visibility = Visibility.Collapsed;

        try
        {
            var loginRequest = new
            {
                username = username,
                password = password
            };

            var response = await ApiClient.Instance.PostAsJsonAsync("/api/auth/login", loginRequest);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                
                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                {
                    _authToken = loginResponse.Token;
                    
                    // Save credentials if Remember Me is checked
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        await SaveCredentialsAsync(username);
                    }
                    else
                    {
                        await ClearSavedCredentialsAsync();
                    }

                    // Open main window
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    
                    // Close login window
                    this.Close();
                }
                else
                {
                    ShowError(UIConstants.ValidationMessages.InvalidServerResponse);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ShowError(UIConstants.ValidationMessages.InvalidCredentials);
            }
        }
        catch (HttpRequestException ex)
        {
            ShowError(string.Format(UIConstants.ValidationMessages.ConnectionFailedForm, _apiBaseUrl));
            _logger.LogError(ex, "Connection error to API at {Url}", _apiBaseUrl);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(UIConstants.ValidationMessages.ErrorOccurredForm, ex.Message));
            _logger.LogError(ex, "An unexpected error occurred during login");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            UIConstants.DialogMessages.ForgotPasswordMessage,
            UIConstants.DialogMessages.ForgotPasswordTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowError(string message)
    {
        ErrorMessageText.Text = message;
        ErrorMessageText.Visibility = Visibility.Visible;
    }

    private async Task SaveCredentialsAsync(string username)
    {
        try
        {
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                UIConstants.Configuration.AppDataFolderName,
                UIConstants.Configuration.ConfigFileName);

            var configDir = System.IO.Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir!);
            }

            var config = new
            {
                Username = username,
                RememberMe = true
            };

            await File.WriteAllTextAsync(configPath, System.Text.Json.JsonSerializer.Serialize(config));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save user credentials");
        }
    }

    private Task ClearSavedCredentialsAsync()
    {
        try
        {
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                UIConstants.Configuration.AppDataFolderName,
                UIConstants.Configuration.ConfigFileName);

            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear saved user credentials");
        }
        
        return Task.CompletedTask;
    }

    private class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}


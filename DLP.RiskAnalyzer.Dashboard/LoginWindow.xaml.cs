using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace DLP.RiskAnalyzer.Dashboard;

public partial class LoginWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private static string? _authToken;

    public static string? AuthToken => _authToken;

    public LoginWindow()
    {
        InitializeComponent();

        // Load configuration
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(appDirectory, "appsettings.json");
        
        System.Diagnostics.Debug.WriteLine($"[LoginWindow] App directory: {appDirectory}");
        System.Diagnostics.Debug.WriteLine($"[LoginWindow] Config file path: {configPath}");
        System.Diagnostics.Debug.WriteLine($"[LoginWindow] Config file exists: {File.Exists(configPath)}");
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(appDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Try to get API URL from config
        var apiUrlFromConfig = configuration["ApiBaseUrl"];
        System.Diagnostics.Debug.WriteLine($"[LoginWindow] ApiBaseUrl from config: {apiUrlFromConfig ?? "NULL"}");
        
        _apiBaseUrl = apiUrlFromConfig ?? "http://localhost:5001"; // Default to 5001 instead of 8000
        
        // Debug: Log the API URL being used
        System.Diagnostics.Debug.WriteLine($"[LoginWindow] Final API Base URL: {_apiBaseUrl}");
        
        // If config file doesn't exist, show a warning
        if (!File.Exists(configPath))
        {
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] WARNING: appsettings.json not found! Using default: {_apiBaseUrl}");
        }
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiBaseUrl)
        };

        // Focus on username field
        Loaded += (s, e) => UsernameTextBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password");
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

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

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
                    ShowError("Invalid response from server");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ShowError("Invalid username or password");
            }
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Cannot connect to API. Please check if the API is running on {_apiBaseUrl}");
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] Connection error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] Attempted URL: {_apiBaseUrl}/api/auth/login");
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] Exception: {ex}");
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
            "Please contact your system administrator to reset your password.",
            "Forgot Password",
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
                "DLP.RiskAnalyzer",
                "config.json");

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
        catch
        {
            // Silently fail if we can't save credentials
        }
    }

    private Task ClearSavedCredentialsAsync()
    {
        try
        {
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DLP.RiskAnalyzer",
                "config.json");

            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch
        {
            // Silently fail if we can't clear credentials
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


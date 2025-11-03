using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace DLP.RiskAnalyzer.Dashboard;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public MainWindow()
    {
        InitializeComponent();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:8000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiBaseUrl)
        };

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        try
        {
            // Load incidents
            var incidents = await _httpClient.GetFromJsonAsync<List<IncidentResponse>>("/api/incidents?limit=100");

            if (incidents != null)
            {
                IncidentsDataGrid.ItemsSource = incidents;

                // Update summary
                TotalIncidentsText.Text = incidents.Count.ToString();
                HighRiskText.Text = incidents.Count(i => i.RiskLevel == "High" || i.RiskLevel == "Critical").ToString();
                
                var avgRisk = incidents.Where(i => i.RiskScore.HasValue)
                                      .Select(i => i.RiskScore!.Value)
                                      .DefaultIfEmpty(0)
                                      .Average();
                AvgRiskScoreText.Text = avgRisk.ToString("F1");

                UniqueUsersText.Text = incidents.Select(i => i.UserEmail).Distinct().Count().ToString();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = 0;
    }

    private void InvestigationButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = 1;
    }

    private async void UsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Load timeline for selected user
        // Implementation here
    }
}


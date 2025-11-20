using System.Runtime.InteropServices;

namespace DLP.RiskAnalyzer.Shared.Helpers;

/// <summary>
/// Helper for environment detection and configuration
/// </summary>
public static class EnvironmentHelper
{
    /// <summary>
    /// Check if running inside a Docker container
    /// </summary>
    public static bool IsDocker()
    {
        return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    }

    /// <summary>
    /// Check if running on Windows
    /// </summary>
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    /// Get Redis connection string with Docker Desktop compatibility logic
    /// </summary>
    public static string GetRedisConnectionString(string configuredHost, int port)
    {
        var host = configuredHost;
        var isDocker = IsDocker();

        if (isDocker && host == "localhost")
        {
            // If running inside Docker container, use host.docker.internal to access Docker Desktop services
            host = "host.docker.internal";
        }
        else if (!isDocker && host == "localhost" && IsWindows())
        {
            // If running on Windows host (outside Docker), use 127.0.0.1 for better reliability
            host = "127.0.0.1";
        }

        return $"{host}:{port}";
    }

    /// <summary>
    /// Get Database connection string with Docker Desktop compatibility logic
    /// </summary>
    public static string GetDatabaseConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        var isDocker = IsDocker();

        if (isDocker && connectionString.Contains("Host=localhost"))
        {
            // If running inside Docker container, use host.docker.internal
            connectionString = connectionString.Replace("Host=localhost", "Host=host.docker.internal");
        }
        else if (!isDocker && connectionString.Contains("Host=localhost") && IsWindows())
        {
            // If running on Windows host, use 127.0.0.1 for better reliability
            connectionString = connectionString.Replace("Host=localhost", "Host=127.0.0.1");
        }

        return connectionString;
    }
}


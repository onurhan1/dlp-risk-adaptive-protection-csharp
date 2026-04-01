namespace DLP.RiskAnalyzer.Dashboard.Constants;

public static class UIConstants
{
    public const string DefaultApiBaseUrl = "http://localhost:5001";
    
    public static class ValidationMessages
    {
        public const string UsernameRequired = "Please enter your username";
        public const string PasswordRequired = "Please enter your password";
        public const string InvalidServerResponse = "Invalid response from server";
        public const string InvalidCredentials = "Invalid username or password";
        public const string ConnectionFailedForm = "Cannot connect to API. Please check if the API is running on {0}";
        public const string ErrorOccurredForm = "An error occurred: {0}";
    }

    public static class DialogMessages
    {
        public const string ForgotPasswordTitle = "Forgot Password";
        public const string ForgotPasswordMessage = "Please contact your system administrator to reset your password.";
    }

    public static class Configuration
    {
        public const string AppDataFolderName = "DLP.RiskAnalyzer";
        public const string ConfigFileName = "config.json";
    }
}

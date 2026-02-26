namespace DLP.RiskAnalyzer.Analyzer.Models;

public class OpenAIChatResponse
{
    public List<OpenAIChatChoice>? Choices { get; set; }
}

public class OpenAIChatChoice
{
    public OpenAIChatMessage? Message { get; set; }
}

public class OpenAIChatMessage
{
    public string? Content { get; set; }
}

public class OpenAIModelsResponse
{
    public List<OpenAIModel>? Data { get; set; }
}

public class OpenAIModel
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public long? Created { get; set; }
    public string? OwnedBy { get; set; }
}

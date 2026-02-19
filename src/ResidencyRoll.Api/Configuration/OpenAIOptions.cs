namespace ResidencyRoll.Api.Configuration;

/// <summary>
/// Configuration options for OpenAI API via Azure OpenAI or OpenAI directly.
/// These options are used by the Semantic Kernel to initialize the chat completion service.
/// </summary>
public class OpenAIOptions
{
    /// <summary>
    /// The model deployment name or model ID (e.g., "gpt-4", "gpt-4-turbo").
    /// </summary>
    public required string OpenAIModel { get; set; }

    /// <summary>
    /// The endpoint URL for the OpenAI API.
    /// For Azure OpenAI: https://{resource-name}.openai.azure.com/
    /// For OpenAI direct: https://api.openai.com/v1
    /// </summary>
    public required string OpenAIEndpoint { get; set; }

    /// <summary>
    /// The API key for authenticating with the OpenAI service.
    /// Should be loaded from secrets management (appsettings.json for development only).
    /// </summary>
    public required string OpenAIApiKey { get; set; }
}

using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.OpenAI;
using Loan.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

using AppChatMessage = Loan.Application.Abstractions.ChatMessage;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace Loan.Infrastructure.AzureOpenAI;

public class AzureOpenAIChatModel : IChatModel
{
    private readonly ChatClient? _chatClient;
    private readonly string _deploymentName;
    private readonly string _endpoint;
    private readonly bool _isConfigured;
    private readonly string _missingConfigReason;

    private readonly ITelemetryCollector? _telemetryCollector;

    public AzureOpenAIChatModel(IConfiguration configuration, ITelemetryCollector? telemetryCollector = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _telemetryCollector = telemetryCollector;

        _endpoint = configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? string.Empty;
        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        var isDefaultEndpoint = string.IsNullOrWhiteSpace(_endpoint) || _endpoint.Contains("YOUR-RESOURCE-NAME", StringComparison.OrdinalIgnoreCase);
        var isDefaultKey = string.IsNullOrWhiteSpace(apiKey);

        if (isDefaultEndpoint || isDefaultKey)
        {
            _isConfigured = false;
            _missingConfigReason = "Azure OpenAI configuration is incomplete. " +
                "Please configure 'AzureOpenAI:Endpoint', 'AzureOpenAI:ApiKey', and 'AzureOpenAI:DeploymentName' in User Secrets " +
                "(e.g., dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"<your-key>\" --project src/Loan.Web).";
            return;
        }

        try
        {
            var openAiClient = new AzureOpenAIClient(new Uri(_endpoint), new ApiKeyCredential(apiKey));
            _chatClient = openAiClient.GetChatClient(_deploymentName);
            _isConfigured = true;
            _missingConfigReason = string.Empty;
        }
        catch (Exception ex)
        {
            _isConfigured = false;
            _missingConfigReason = $"Failed to initialize Azure OpenAI client for endpoint '{_endpoint}': {ex.Message}";
        }
    }

    public async Task<string> GenerateCompletionAsync(
        IEnumerable<AppChatMessage> messages,
        double temperature = 0.2,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var chatMessages = ConvertMessages(messages);
        var options = new ChatCompletionOptions
        {
            Temperature = (float)temperature
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _chatClient!.CompleteChatAsync(chatMessages, options, cancellationToken);
        sw.Stop();

        _telemetryCollector?.RecordLlmLatency(sw.Elapsed.TotalMilliseconds);

        var completion = response.Value;
        if (completion.Usage != null)
        {
            _telemetryCollector?.RecordTokens(completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount);
        }

        if (completion.Content != null && completion.Content.Count > 0)
        {
            return completion.Content[0].Text ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<T> GenerateStructuredAsync<T>(
        IEnumerable<AppChatMessage> messages,
        double temperature = 0.1,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var systemPromptAddition = "\n\nCRITICAL INSTRUCTION: Respond ONLY with a single valid JSON object matching the requested schema. Do not include markdown code fence formatting (```json) or extra commentary.";
        
        var messageList = messages.ToList();
        if (messageList.Count > 0 && messageList[0].Role == "system")
        {
            messageList[0] = new AppChatMessage("system", messageList[0].Content + systemPromptAddition);
        }
        else
        {
            messageList.Insert(0, new AppChatMessage("system", systemPromptAddition));
        }

        var jsonResponse = await GenerateCompletionAsync(messageList, temperature, cancellationToken);
        
        var cleanedJson = jsonResponse.Trim();
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(7);
        }
        if (cleanedJson.StartsWith("```"))
        {
            cleanedJson = cleanedJson.Substring(3);
        }
        if (cleanedJson.EndsWith("```"))
        {
            cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
        }
        cleanedJson = cleanedJson.Trim();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<T>(cleanedJson, options)
            ?? throw new InvalidOperationException($"Failed to deserialize Azure OpenAI JSON completion to type '{typeof(T).Name}'. Raw response: {jsonResponse}");
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<AppChatMessage> messages,
        double temperature = 0.2,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var chatMessages = ConvertMessages(messages);
        var options = new ChatCompletionOptions
        {
            Temperature = (float)temperature
        };

        var updates = _chatClient!.CompleteChatStreamingAsync(chatMessages, options, cancellationToken);

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    private void EnsureConfigured()
    {
        if (!_isConfigured || _chatClient == null)
        {
            throw new InvalidOperationException(_missingConfigReason);
        }
    }

    private static List<OpenAIChatMessage> ConvertMessages(IEnumerable<AppChatMessage> messages)
    {
        var list = new List<OpenAIChatMessage>();
        foreach (var msg in messages)
        {
            switch (msg.Role.ToLowerInvariant())
            {
                case "system":
                    list.Add(new SystemChatMessage(msg.Content));
                    break;
                case "assistant":
                    list.Add(new AssistantChatMessage(msg.Content));
                    break;
                case "user":
                default:
                    list.Add(new UserChatMessage(msg.Content));
                    break;
            }
        }
        return list;
    }
}

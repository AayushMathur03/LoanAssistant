using Loan.Application.Abstractions;
using Loan.Infrastructure.SemanticKernel.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Loan.Infrastructure.SemanticKernel;

public class SemanticKernelAgentService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelAgentService> _logger;
    private readonly bool _isConfigured;

    public SemanticKernelAgentService(
        IConfiguration configuration,
        IdentityPlugin identityPlugin,
        CreditPlugin creditPlugin,
        PolicySearchPlugin policySearchPlugin,
        DraftSaverPlugin draftSaverPlugin,
        ILogger<SemanticKernelAgentService> logger)
    {
        _logger = logger;
        var builder = Kernel.CreateBuilder();

        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];
        var deployment = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey) && !endpoint.Contains("YOUR-RESOURCE-NAME"))
        {
            try
            {
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deployment,
                    endpoint: endpoint,
                    apiKey: apiKey);
                _isConfigured = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure OpenAI Chat Completion in Semantic Kernel.");
                _isConfigured = false;
            }
        }
        else
        {
            _isConfigured = false;
        }

        // Register Core In-Process Plugins
        builder.Plugins.AddFromObject(identityPlugin, "Identity");
        builder.Plugins.AddFromObject(creditPlugin, "Credit");
        builder.Plugins.AddFromObject(policySearchPlugin, "PolicySearch");
        builder.Plugins.AddFromObject(draftSaverPlugin, "DraftSaver");

        _kernel = builder.Build();
    }

    public Kernel Kernel => _kernel;
    public bool IsConfigured => _isConfigured;

    public async Task<string> RunAgentPromptAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured)
        {
            return "Semantic Kernel is running in offline mode. Azure OpenAI credentials are not configured.";
        }

        try
        {
            var prompt = $"{systemPrompt}\n\nUser: {userPrompt}\n\nAssistant:";
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic Kernel prompt execution failed.");
            return $"Agent Error: {ex.Message}";
        }
    }
}

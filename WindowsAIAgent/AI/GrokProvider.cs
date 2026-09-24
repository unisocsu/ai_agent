namespace WindowsAIAgent.AI;

public sealed class GrokProvider : IAIProvider
{
    private readonly OpenAICompatibleProvider inner;
    public string Name => "Grok";

    public GrokProvider(string apiKey, string model = "grok-4-1-fast-reasoning")
    {
        inner = new OpenAICompatibleProvider("https://api.x.ai/", apiKey, model);
    }

    public Task<AIResponse> SendAsync(IReadOnlyList<AIMessage> messages, IReadOnlyList<AIToolDefinition> tools, CancellationToken ct = default)
        => inner.SendAsync(messages, tools, ct);
}
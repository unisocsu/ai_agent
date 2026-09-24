namespace WindowsAIAgent.AI;

public sealed record AIMessage(string Role, string Content);
public sealed record AIResponse(string Content, IReadOnlyList<AIToolCall> ToolCalls);
public sealed record AIToolCall(string Id, string Name, string ArgumentsJson);

public interface IAIProvider
{
    string Name { get; }
    Task<AIResponse> SendAsync(
        IReadOnlyList<AIMessage> messages,
        IReadOnlyList<AIToolDefinition> tools,
        CancellationToken cancellationToken = default);
}

public sealed record AIToolDefinition(string Name, string Description, object Parameters);
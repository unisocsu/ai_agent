using System.Text.Json;

namespace WindowsAIAgent.Tools;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    object Parameters { get; }
    Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default);
}
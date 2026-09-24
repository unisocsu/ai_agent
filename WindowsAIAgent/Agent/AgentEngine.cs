using System.Text.Json;
using WindowsAIAgent.AI;
using WindowsAIAgent.Tools;

namespace WindowsAIAgent.Agent;

public sealed class AgentEngine
{
    private readonly List<IAgentTool> tools = new() { new FileTool(), new WriteFileTool(), new ProcessTool() };
    private readonly List<AIMessage> history = new();
    private readonly string workspace;

    public AgentEngine()
    {
        workspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowsAIAgent", "workspace");
        Directory.CreateDirectory(workspace);
    }

    public IReadOnlyList<AIToolDefinition> Definitions =>
        tools.Select(t => new AIToolDefinition(t.Name, t.Description, t.Parameters)).ToArray();

    public async Task<string> RunAsync(IAIProvider provider, string userMessage, CancellationToken ct = default)
    {
        if (history.Count == 0)
        {
            var instructions = await InstructionLoader.LoadAsync();
            history.Add(new AIMessage("system", instructions + $"\n\nWorkspace: {workspace}"));
        }

        history.Add(new AIMessage("user", userMessage));

        for (var step = 0; step < 8; step++)
        {
            var response = await provider.SendAsync(history, Definitions, ct);
            if (response.ToolCalls.Count == 0)
            {
                history.Add(new AIMessage("assistant", response.Content));
                return response.Content;
            }

            history.Add(new AIMessage("assistant", response.Content));

            foreach (var call in response.ToolCalls)
            {
                if (!tools.Any(t => t.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var tool = tools.First(t => t.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase));
                using var args = JsonDocument.Parse(call.ArgumentsJson);
                var result = await tool.ExecuteAsync(args.RootElement, ct);
                history.Add(new AIMessage("tool", $"Tool {call.Name} result:\n{result}"));
            }
        }

        return "Agent stopped after reaching the tool-call limit.";
    }
}
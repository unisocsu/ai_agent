namespace WindowsAIAgent.Agent;

public static class InstructionLoader
{
    public static async Task<string> LoadAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "agent.md");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }
}
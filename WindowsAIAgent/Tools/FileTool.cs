using System.Text.Json;

namespace WindowsAIAgent.Tools;

public sealed class FileTool : IAgentTool
{
    public string Name => "read_file";
    public string Description => "Read a UTF-8 text file from the workspace.";
    public object Parameters => new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } };

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        var path = arguments.GetProperty("path").GetString() ?? "";
        return await File.ReadAllTextAsync(path, ct);
    }
}

public sealed class WriteFileTool : IAgentTool
{
    public string Name => "write_file";
    public string Description => "Write UTF-8 text to a file, creating parent directories.";
    public object Parameters => new { type = "object", properties = new { path = new { type = "string" }, content = new { type = "string" } }, required = new[] { "path", "content" } };

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        var path = arguments.GetProperty("path").GetString() ?? "";
        var content = arguments.GetProperty("content").GetString() ?? "";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, content, ct);
        return $"Wrote {path}";
    }
}
using System.Diagnostics;
using System.Text.Json;

namespace WindowsAIAgent.Tools;

public sealed class ProcessTool : IAgentTool
{
    public string Name => "run_command";
    public string Description => "Run a Windows command in the configured workspace.";
    public object Parameters => new { type = "object", properties = new { command = new { type = "string" } }, required = new[] { "command" } };

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        var command = arguments.GetProperty("command").GetString() ?? "";
        var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", "/c " + command)
        {
            WorkingDirectory = Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start command.");
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return $"Exit code: {p.ExitCode}\nSTDOUT:\n{output}\nSTDERR:\n{error}";
    }
}
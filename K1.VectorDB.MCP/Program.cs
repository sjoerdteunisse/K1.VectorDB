using K1.VectorDB.MCP;
using K1.VectorDB.MCP.Prompts;
using K1.VectorDB.MCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Critical: redirect Console.Out → Console.Error ────────────────────────
// The MCP stdio transport owns stdout exclusively for JSON-RPC framing.
// VectorDb and LMStudioEmbedder may call Console.WriteLine; that would corrupt
// the wire protocol. Redirecting Console.Out ensures those writes land on
// stderr (visible in logs) instead of polluting stdout.
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);

// Route all framework logs to stderr as well.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton<GraphSessionService>()
    .AddMcpServer()
    .WithStdioServerTransport()
    // Tool types — each resolved from DI with constructor injection
    .WithTools<GraphLifecycleTools>()
    .WithTools<NodeTools>()
    .WithTools<EdgeTools>()
    .WithTools<SearchTools>()
    .WithTools<InspectionTools>()
    // Prompt type — returns the multi-phase repository documentation prompt
    .WithPrompts<RepositoryExplorerPrompt>();

await builder.Build().RunAsync();

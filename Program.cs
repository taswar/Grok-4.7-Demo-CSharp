#pragma warning disable OPENAI001 // The Entra ID authenticationPolicy constructor is an evaluation API.
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.Diagnostics;


var selectedCase = args.FirstOrDefault()?.ToLowerInvariant() ?? "1";
if (selectedCase is not ("1" or "2" or "3" or "4" or "5" or "all"))
{
    Console.WriteLine("Usage: dotnet run -- [1|2|3|4|5|all]");
    return;
}

var chat = FoundryChat.CreateClient();

// A shared deadline for the whole run; Ctrl+C cancels in-flight requests too.
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

string[] cases = selectedCase == "all" ? ["1", "2", "3", "4", "5"] : [selectedCase];
foreach (var useCase in cases)
{
    Console.WriteLine($"\n--- Use case {useCase} ---");
    try
    {
        switch (useCase)
        {
            case "1": await RunSpecDraftAsync(); break;
            case "2": await RunRefactoringReviewAsync(); break;
            case "3": await RunToolCallingAsync(); break;
            case "4": await RunImpactAssessmentAsync(); break;
            case "5": await RunCompileFixLoopAsync(); break;
        }
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Cancelled, or the demo's ten-minute limit was reached.");
        Environment.ExitCode = 1;
        break;
    }
    catch (Exception ex)
    {
        // Demo boundary: report the failure but let 'all' continue to the next case.
        Console.Error.WriteLine($"Use case {useCase} failed: {ex.Message}");
        Environment.ExitCode = 1;
    }
}


async Task RunSpecDraftAsync()
{
    var requirements = """
        Multi-location inventory system: 10K concurrent users, updates visible within
        two seconds, SQL Server migration, SAP REST integration, audit trail and a
        six-month delivery timeline. The team has limited NoSQL experience.
        """;

    var options = new ChatCompletionOptions { MaxOutputTokenCount = 1500 };

    ChatCompletion completion = await chat.CompleteChatAsync(
    [
        new SystemChatMessage("Draft a concise technical specification. Use one short paragraph or a few bullets per section, covering assumptions, architecture trade-offs, data model, APIs, reliability, security, rollout phases and open questions. Be brief and do not invent verified benchmarks."),
        new UserChatMessage(requirements)
    ], options, cancellation.Token);

    await File.WriteAllTextAsync("inventory-system-spec.md", completion.Content[0].Text);
    Console.WriteLine("Review inventory-system-spec.md with the engineering team.");
}

async Task RunRefactoringReviewAsync()
{
    var existingCode = """
        // InventoryService.cs (excerpt)
        var current = await GetStockLevel(sku);
        if (current >= quantity) await UpdateStockLevel(sku, current - quantity);

        // OrderManager.cs (excerpt)
        order.Status = "Cancelled";
        await SaveOrder(order);
        await SendCancellationEmail(order);
        """;

    ChatCompletion analysis = await chat.CompleteChatAsync(
    [
        new SystemChatMessage("Review the supplied C# excerpts. Identify concurrency, consistency and validation issues; distinguish facts from assumptions."),
        new UserChatMessage(existingCode)
    ], cancellationToken: cancellation.Token);

    var findings = analysis.Content[0].Text;
    Console.WriteLine(findings);

    ChatCompletion plan = await chat.CompleteChatAsync(
    [
        new SystemChatMessage("Propose a reviewable refactoring plan, test cases and a migration strategy. Ask for missing domain details instead of inventing them."),
        new UserChatMessage($"Excerpts:\n{existingCode}\n\nReview:\n{findings}")
    ], cancellationToken: cancellation.Token);

    Console.WriteLine("\nRefactoring proposal:\n" + plan.Content[0].Text);
}

async Task RunToolCallingAsync()
{
    var messages = new List<ChatMessage>
    {
        new SystemChatMessage("Use lookup_note for supplied local notes. Say explicitly when information is unavailable; do not claim live web access."),
        new UserChatMessage("What should I consider when adding distributed tracing to a .NET service?")
    };

    var options = new ChatCompletionOptions
    {
        Tools =
        {
            ChatTool.CreateFunctionTool(
                functionName: "lookup_note",
                functionDescription: "Look up a local note about tracing or sampling",
                functionParameters: BinaryData.FromString("""
                    {
                      "type": "object",
                      "properties": { "topic": { "type": "string", "description": "tracing or sampling" } },
                      "required": ["topic"]
                    }
                    """))
        }
    };

    for (var round = 0; round < 4; round++)
    {
        ChatCompletion completion = await chat.CompleteChatAsync(messages, options, cancellation.Token);
        if (completion.FinishReason != ChatFinishReason.ToolCalls)
        {
            Console.WriteLine(completion.Content[0].Text);
            return;
        }

        messages.Add(new AssistantChatMessage(completion)); // Preserve assistant tool calls and their IDs.
        foreach (var call in completion.ToolCalls)
        {
            var result = "Unknown tool or topic";
            if (call.FunctionName == "lookup_note")
            {
                using var arguments = JsonDocument.Parse(call.FunctionArguments);
                var topic = arguments.RootElement.GetProperty("topic").GetString();
                result = topic switch
                {
                    "tracing" => "Local note: use OpenTelemetry spans and propagate trace context across HTTP calls.",
                    "sampling" => "Local note: measure cost and choose a sampling strategy before production rollout.",
                    _ => "No local note exists for this topic."
                };
            }

            messages.Add(new ToolChatMessage(call.Id, result));
        }
    }

    Console.WriteLine("Stopped after four tool rounds; inspect the conversation before retrying.");
}


async Task RunImpactAssessmentAsync()
{
    var path = "Program.cs";
    var code = await File.ReadAllTextAsync(path, cancellation.Token);
    if (code.Length > 40_000)
        throw new InvalidOperationException("Select a smaller, relevant excerpt before sending it.");

    var proposedChange = "Add structured logging for each external API request.";
    ChatCompletion completion = await chat.CompleteChatAsync(
    [
        new SystemChatMessage("Assess the impact of a proposed C# change. List directly affected code, likely dependencies, test cases and unknowns. Do not claim to have read files not supplied."),
        new UserChatMessage($"Change: {proposedChange}\nFile: {path}\n```csharp\n{code}\n```")
    ], cancellationToken: cancellation.Token);

    await File.WriteAllTextAsync("impact-assessment.md", completion.Content[0].Text);
    Console.WriteLine("Review impact-assessment.md; only the supplied file was analyzed.");
}

async Task RunCompileFixLoopAsync()
{
    // Isolated scratch class library. Generated code is built, never run.
    var workDir = Path.Combine(Path.GetTempPath(), "grok-codegen-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(workDir);
    await RunDotnetAsync(workDir, "new classlib --output . --framework net10.0");
    var generatedPath = Path.Combine(workDir, "Generated.cs");

    var task = """
        Write a single C# file with a public static class `Temperature` exposing
        `CelsiusToFahrenheit(double c)` and `FahrenheitToCelsius(double f)`, both returning double.
        Return only the file contents.
        """;

    var messages = new List<ChatMessage>
    {
        new SystemChatMessage("You write C# that compiles on .NET 10. When given compiler errors, return the complete corrected file, not a diff. Output only C# source."),
        new UserChatMessage(task)
    };

    for (var round = 1; round <= 4; round++)
    {
        ChatCompletion completion = await chat.CompleteChatAsync(messages, cancellationToken: cancellation.Token);
        var source = StripFences(completion.Content[0].Text);
        await File.WriteAllTextAsync(generatedPath, source, cancellation.Token);

        var (compiled, output) = await RunDotnetAsync(workDir, "build --nologo -clp:ErrorsOnly");
        if (compiled)
        {
            Console.WriteLine($"Compiled on round {round}. Review {generatedPath} and add tests before use.");
            return;
        }

        Console.WriteLine($"Round {round} did not compile; sending errors back.");
        messages.Add(new AssistantChatMessage(source));
        messages.Add(new UserChatMessage($"That file did not compile. Fix it and return the complete file.\n\nCompiler output:\n{output}"));
    }

    Console.WriteLine("Stopped after four rounds without a clean build; inspect the last attempt manually.");
}

static string StripFences(string text)
{
    var trimmed = text.Trim();
    if (!trimmed.StartsWith("```")) return trimmed;
    var firstNewline = trimmed.IndexOf('\n');
    var body = firstNewline < 0 ? trimmed : trimmed[(firstNewline + 1)..];
    var closingFence = body.LastIndexOf("```", StringComparison.Ordinal);
    return (closingFence < 0 ? body : body[..closingFence]).Trim();
}

static async Task<(bool compiled, string output)> RunDotnetAsync(string workDir, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    })!;
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return (process.ExitCode == 0, stdout + stderr);
}


public static class FoundryChat
{
    public static ChatClient CreateClient()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChat).Assembly)
            .Build();

        var endpoint = config["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Set Foundry:Endpoint with dotnet user-secrets.");
        var deployment = config["Foundry:Deployment"]
            ?? throw new InvalidOperationException("Set Foundry:Deployment with dotnet user-secrets.");

        // DefaultAzureCredential resolves az login, Visual Studio, or managed identity.
        var tokenPolicy = new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default");
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri($"{endpoint.TrimEnd('/')}/openai/v1"),
            // Grok is slow to produce long completions; the default 100s per-attempt
            // timeout cancels the request before it returns. Give it room.
            NetworkTimeout = TimeSpan.FromMinutes(5),
        };

        return new ChatClient(model: deployment, authenticationPolicy: tokenPolicy, options: options);
    }
}
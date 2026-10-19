# Grok 4.7 C# Demo

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![C# Developers](https://img.shields.io/badge/For-C%23%20Developers-239120)
![Grok 4.7](https://img.shields.io/badge/Grok%204.7-Reasoning%20Model-orange)
![Azure Identity](https://img.shields.io/badge/Azure%20Identity-DefaultAzureCredential-0089D6)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)
![Made with Love](https://img.shields.io/badge/Made%20with-%E2%9D%A4-red)

A .NET 10 console app that demonstrates common Grok model patterns on Microsoft Foundry, using the official OpenAI C# SDK and Entra ID (Azure Identity) authentication. Each use case is a small, self-contained example.

## Why Grok 4.7 Matters

- Strong multi-step reasoning for planning, code analysis and refactoring tasks
- Large output budget suited to drafting specifications and detailed assessments
- Agentic tool/function calling for grounded, non-hallucinated answers
- Served through Microsoft Foundry's OpenAI-compatible endpoint, so existing OpenAI SDK code works

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An Azure subscription with a Microsoft Foundry resource
- A Grok model deployed on that resource (for example `grok-4`)
- Azure CLI, signed in with `az login` (used by `DefaultAzureCredential` locally)
- Your signed-in identity has the **Cognitive Services User** role on the Foundry resource

## Configuration

Settings are read from .NET user secrets.

```bash
dotnet user-secrets init
dotnet user-secrets set "Foundry:Endpoint" "https://<your-foundry-resource>.services.ai.azure.com"
dotnet user-secrets set "Foundry:Deployment" "grok-4"
```

| Setting | Required | Purpose |
|---|---|---|
| `Foundry:Endpoint` | Yes | Base URL of your Foundry resource. The app appends `/openai/v1`. |
| `Foundry:Deployment` | Yes | The model deployment name to call |

Authentication uses `DefaultAzureCredential`, which resolves your `az login` session, Visual Studio sign-in, or a managed identity in Azure. No API key is stored in the project.

## Usage

```bash
dotnet run -- [1|2|3|4|5|all]
```

Running with no argument runs case 1. Running `all` runs every case in order. A single `CancellationTokenSource` enforces a ten-minute budget for the whole run, and <kbd>Ctrl</kbd>+<kbd>C</kbd> cancels in-flight requests.

## Use cases

| # | Use case | What it shows | Key settings |
|---|---|---|---|
| 1 | Spec draft | Single prompt that drafts a concise technical specification to a file | `MaxOutputTokenCount` |
| 2 | Refactoring review | Two-step review: find issues in C# excerpts, then propose a plan | Chained completions |
| 3 | Tool calling | Function/tool calling loop against a local `lookup_note` tool | `ChatTool`, `ToolChatMessage` |
| 4 | Impact assessment | Assesses the impact of a proposed change on a supplied file | `File` input, size guard |
| 5 | Compile-fix loop | Generates C#, builds it in an isolated class library, feeds errors back | `Process`, retry loop |

Case 1 writes `inventory-system-spec.md`; case 4 writes `impact-assessment.md`. Case 5 builds generated code in a temporary directory and never runs it.

## Notes on timeouts

Grok is a reasoning model and can take a while to produce long completions. Two settings in the code keep requests from being cancelled by the default 100-second per-attempt network timeout:

- `OpenAIClientOptions.NetworkTimeout` is raised to 5 minutes in `FoundryChat.CreateClient()`.
- Case 1 caps output with `MaxOutputTokenCount` and asks for a concise spec so it returns quickly.

If you still hit `exceeded the configured timeout`, raise `NetworkTimeout` further, lower `MaxOutputTokenCount`, or switch to the streaming API.

## Troubleshooting

| Error | Likely cause |
|---|---|
| `exceeded the configured timeout of 0:01:40` | Completion took longer than the per-attempt `NetworkTimeout`; raise it or shrink the output |
| `Name or service not known` | Wrong `Foundry:Endpoint` value, or DNS/VPN blocking the host |
| `401 Unauthorized` | Identity is missing the Cognitive Services User role, or Azure CLI is signed in to the wrong tenant |
| `DeploymentNotFound` / `404` | `Foundry:Deployment` doesn't match a deployment on the resource |
| `InvalidOperationException: Set Foundry:...` | A required user secret is missing |

## Project structure

```
Program.cs                    Entry point, configuration, and all use cases
Grok-4.7-Demo-Csharp.csproj   .NET 10 project and package references
LICENSE                       MIT license
README.md                     This file
```

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

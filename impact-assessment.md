The only external API calls visible in the supplied `Program.cs` are `ChatClient.CompleteChatAsync` calls to the Foundry/OpenAI endpoint, plus token acquisition inside `BearerTokenPolicy` / `DefaultAzureCredential`. Local file I/O and `lookup_note` are not external APIs. No project file, tests, or other source files were supplied.

## Directly affected code

All chat traffic is created in one place, so that is the right choke point:

- `FoundryChat.CreateClient()` — builds `OpenAIClientOptions` (`Endpoint`, `NetworkTimeout = 5 minutes`) and `new ChatClient(..., authenticationPolicy: tokenPolicy, options)`. A pipeline policy or HTTP logging handler here covers every call without editing each use case.
- `BearerTokenPolicy` / `DefaultAzureCredential` (`https://ai.azure.com/.default`) — separate Entra token requests. They are not logged by a chat-only wrapper unless credential/pipeline logging is added too.

Call sites that issue those requests (behavior must stay the same; only observation is added):

| Location | External calls |
|---|---|
| `RunSpecDraftAsync` | 1 `CompleteChatAsync`, then write `inventory-system-spec.md` |
| `RunRefactoringReviewAsync` | 2 sequential `CompleteChatAsync` |
| `RunToolCallingAsync` | 1–4 `CompleteChatAsync` (loop stops on non-tool finish or after 4 rounds). Tool results are local. |
| `RunImpactAssessmentAsync` | 1 `CompleteChatAsync`, then write `impact-assessment.md` |
| Top-level `foreach` | Case `"5"` is commented out. `"all"` runs 1–4 only. |

There is no `ILogger`, host, or DI today. Output is `Console.WriteLine` / `Console.Error`. The shared `CancellationTokenSource` (10 minutes) and `Console.CancelKeyPress` handler must still cancel in-flight work.

Per-attempt timeout (5 minutes) can emit more HTTP tries than logical `CompleteChatAsync` calls if the client retries. Logging “each external API request” should say whether it means each HTTP attempt or each logical completion.

## Likely dependencies

Not declared in this file; inferred only from the APIs already used:

- `System.ClientModel.Primitives` is already imported — a custom `PipelinePolicy` on `OpenAIClientOptions` is the smallest hook and avoids new packages if the evaluation `ChatClient` constructor accepts policies.
- `Microsoft.Extensions.Logging` (and a console/JSON provider) if “structured” means `ILogger` properties rather than hand-rolled JSON. Configuration is already `Microsoft.Extensions.Configuration` via user secrets only.
- `Azure.Identity` / `Azure.Core` logging if token requests count. Do not log bearer tokens or `Foundry:Endpoint` secrets.
- No generic host exists. A manual `LoggerFactory` is enough; pulling in `Microsoft.Extensions.Hosting` is optional and wider than this change.

## Test cases

- One structured event per logical completion: case 1 → 1, case 2 → 2, case 4 → 1. Case 3 → one event per round, and no event for `lookup_note`.
- Fields that can be known here: deployment name, HTTP method, path (not full secret query), status, duration, finish reason, round index, cancellation/timeout/exception type. No prompt or response body unless explicitly required.
- Missing `Foundry:Endpoint` or `Foundry:Deployment` still throws before any request and logs nothing remote.
- Ctrl+C and the 10-minute CTS still set exit code 1 and do not hang; a cancelled attempt is logged once.
- A thrown client error in one case still lets `"all"` continue; exit code stays 1.
- `NetworkTimeout` failure is distinguishable from the demo deadline.
- Auth header, user-secrets values, and tool argument text are absent from logs.
- Existing outputs unchanged: spec file, refactoring console text, tool answer or “four tool rounds” message, `impact-assessment.md`.

## Unknowns

- Whether token acquisition counts as an “external API request,” and whether SDK retries should be separate events. Retry policy is not configured in this file.
- `OPENAI001` evaluation constructor: which pipeline/logging extension points this package version actually supports was not verifiable here.
- No `.csproj` or test project was supplied, so current packages, analyzers, and tests are unknown.
- Redaction and retention rules for prompts are unknown. Case 4 sends the full source file; case 1–3 send requirements and code excerpts.
- Case `"5"` (`RunCompileFixLoopAsync`) is commented out; its request count is unknown.
- Correlation IDs returned by the service, if any, are not read today.
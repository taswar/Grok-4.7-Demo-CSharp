# Contributing

Thanks for helping improve this sample. It's a small .NET 10 console app with five use cases that call Grok 4.7 on Microsoft Foundry, using the official OpenAI C# SDK and Entra ID. Contributions that make it clearer, more correct, or more useful to other .NET developers are welcome.

## Ways to contribute

- **Report a bug.** A use case fails, or the README doesn't match what the code does.
- **Fix the docs.** Typos, unclear steps, missing troubleshooting cases.
- **Improve the sample.** For example, a streaming version of case 1, or a clearer way to handle long reasoning times.
- **Share results.** If you ran the cases against your own code or specs, an issue describing what you saw helps others.

For anything larger than a small fix, open an issue first so we can agree on the approach before you write code.

## Set up

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and the Azure CLI.
2. Clone the repo and build it:

   ```bash
   git clone https://github.com/taswar/Grok-4.7-Demo-CSharp.git
   cd Grok-4.7-Demo-CSharp
   dotnet build
   ```

3. To run it, deploy Grok 4.7 on a Microsoft Foundry resource, set `Foundry:Endpoint` and `Foundry:Deployment` with user secrets, and sign in with `az login`, as described in the [README](README.md#configuration). Your account needs the **Cognitive Services User** role on the resource. The sample uses Entra ID only, with no API key.

## Guidelines

- **Never commit secrets.** No keys, endpoints, tenant IDs or tokens in code, config files or screenshots. Settings belong in user secrets.
- **Keep case 5 safe.** It builds generated code in a temporary class library and never runs it. Keep it that way, and keep the retry limit.
- **Keep tool calling local.** Case 3's `lookup_note` tool reads only supplied notes. Don't give it web, file system or command access.
- **Keep the timeouts.** Grok is a reasoning model and can take minutes on long answers. Keep the raised `NetworkTimeout` and the shared ten-minute budget, or explain why you changed them.
- **Keep it a sample.** The code should stay short and readable. Prefer clarity over abstraction, and add a dependency only when it clearly helps.
- **Match the existing style.** Target `net10.0`, keep nullable reference types on, and follow the naming and layout already in `Program.cs`.
- **Keep the README in sync.** If you add a case, change settings or change output, update the README in the same pull request, including the use case table.
- **One change per pull request.** Small, focused pull requests are easier to review and merge.

## Pull request checklist

- [ ] `dotnet build` succeeds with no new warnings.
- [ ] Any case you changed runs against your own Foundry deployment, or you explain why you couldn't run it.
- [ ] No secrets, endpoints or personal data are in the diff.
- [ ] The README reflects any change in cases, settings or output.
- [ ] The pull request description says what changed and why.

The build workflow runs on every pull request and has to pass before a merge.

## Reporting a bug

Open an issue and include:

- Which case you ran (`dotnet run -- 1` to `5`, or `all`) and what you expected.
- The full error message, with any endpoints removed.
- Your .NET SDK version (`dotnet --version`) and operating system.
- Your deployment name, and whether the error was a timeout.

## Security issues

Please don't report security problems in a public issue. Use **Security → Report a vulnerability** on the repo to send a private report.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE) that covers this project.

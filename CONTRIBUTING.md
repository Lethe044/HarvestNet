# Contributing to HarvestNet

Thanks for considering a contribution. This project is young and there is a lot of room
to make it better, whether that's a bug fix, a new healing provider, a new output sink,
or just a clearer error message.

## Getting set up

You need the .NET 8 SDK. After cloning the repo:

```bash
dotnet restore HarvestNet.sln
dotnet build HarvestNet.sln
dotnet test tests/HarvestNet.Tests/HarvestNet.Tests.csproj
```

If everything passes, you're ready to make changes.

## Project layout

- `src/HarvestNet.Core` is the library: crawling, extraction, self-healing and export.
  This is where most of the logic lives.
- `src/HarvestNet.Cli` is the `harvestnet` command line tool, a thin layer over the
  library that reads JSON recipes.
- `tests/HarvestNet.Tests` holds unit tests. New logic should come with new tests where
  it's reasonable to add them.
- `samples/QuickStart` is a runnable example that scrapes a public practice site
  (quotes.toscrape.com), useful for checking that a change didn't break the common path.

## Making a change

1. Open an issue first for anything non-trivial, so we can agree on the approach before
   you put time into it. Small fixes and typos can just go straight to a pull request.
2. Keep pull requests focused. A PR that does one thing is much easier to review than one
   that mixes a refactor with a new feature.
3. Add or update tests for the behavior you're changing.
4. Run `dotnet build` and `dotnet test` locally before opening the PR.
5. Write a clear PR description: what changed and why, not just what.

## Adding a new healing provider

If you want to add support for another LLM API, implement `IHealingProvider` in
`src/HarvestNet.Core/Healing`, following the pattern used by `GroqHealingProvider` or
`GeminiHealingProvider`. It should accept a `HealingRequest`, build a prompt with
`SelectorHealingPrompt`, call the API, and return the selector or null on any failure.
Providers must never throw for a failed API call; the caller treats a null return as
"healing did not work this time" and moves on.

## Adding a new output sink

Implement `IResultSink<T>` in `src/HarvestNet.Core/Export`. Sinks are used concurrently
by the spider, so guard shared state (a file handle, a database connection) with a lock
or semaphore, the way `CsvSink` and `SqliteSink` do.

## Code style

- No em dashes in code, comments, commit messages or documentation. Use a regular hyphen.
- Prefer clear names over clever ones.
- Keep public APIs documented with XML doc comments; they show up in IntelliSense and in
  the generated NuGet package.

## Reporting bugs

Please include the recipe or code snippet that triggers the issue, the site or a minimal
HTML sample if the problem is extraction-related, and the exact error message. That's
usually enough to reproduce and fix quickly.

## Code of conduct

Be respectful. Disagreements about code are fine and expected; personal attacks are not.

## Publishing a release (maintainers only)

Publishing to NuGet.org happens automatically through GitHub Actions using NuGet's
Trusted Publishing, so no API key is stored anywhere in this repository or its secrets.

1. Bump the `<Version>` in `src/HarvestNet.Core/HarvestNet.Core.csproj` and
   `src/HarvestNet.Cli/HarvestNet.Cli.csproj`, and add an entry to CHANGELOG.md.
2. Commit and push that change to `main`.
3. Create a GitHub Release with a tag matching the new version (for example `v1.1.0`).
   Publishing the release triggers `.github/workflows/publish-nuget.yml`, which builds,
   tests, packs and pushes both packages to NuGet.org.

If the automated publish ever needs to be done by hand (CI outage, and so on):

```bash
dotnet pack src/HarvestNet.Core/HarvestNet.Core.csproj -c Release -o ./nupkg
dotnet pack src/HarvestNet.Cli/HarvestNet.Cli.csproj -c Release -o ./nupkg
dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

This requires a personal NuGet.org API key generated from account settings; it is a
fallback only and should not be needed in normal operation.

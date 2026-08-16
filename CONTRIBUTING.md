# Contributing to SimpleFlux

Thanks for taking an interest! SimpleFlux is a small project that values
simplicity — keep changes minimal, focused, and backed by a clear explanation.

## Getting started

```bash
# Requires the .NET 10 SDK (see global.json for the pinned version)
dotnet restore
dotnet build
```

Run the sample app (requires [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local Azure Table Storage):

```bash
azurite                        # or: docker run -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
dotnet run --project sample/SimpleFlux.Sample
```

## Tests

The repository does not yet have a test project — adding one is a high-priority
work item (the CI workflow already runs `dotnet test` automatically once a test
project exists). If you add tests, keep them focused on the library:

- event round-trip (write → read → identical event)
- version assignment and stream metadata
- projection replay
- batch semantics (grouping by stream, ordering)

## Making changes

1. Create a branch: `git checkout -b fix/description`
2. Make your change with clear, conventional commits:
   `type(scope): short description` (`feat`, `fix`, `docs`, `test`, `ci`, `refactor`, `chore`)
3. Verify: `dotnet build` must be warning-free (warnings are errors in the library project)
4. Push and open a pull request against `main` with a summary of the change and
   the verification you ran.

## Release-related changes

Versioning is CI-driven (see `RELEASING.md`) — **never change the package version
inline in the csproj**. Prerelease and stable releases are published by the
`Publish Prerelease` and `Publish Release` GitHub Actions workflows.

## Style

- .editorconfig at the repo root defines the style (file-scoped namespaces, 4-space
  indentation, nullable annotations enabled)
- Public API members need XML docs (`GenerateDocumentationFile` is on for the
  library and warnings are treated as errors)
- Keep the public API small — the project's whole premise is *simple* event sourcing
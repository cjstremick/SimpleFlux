# SimpleFlux

Simple Flux is a simple event sourcing library for .NET. It is based on Azure Tables and is inspired by the excellent Streamstone project.

| CI | NuGet |
|---|---|
| [![CI](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml) | [![NuGet](https://img.shields.io/nuget/v/SimpleFlux)](https://www.nuget.org/packages/SimpleFlux) |

## Getting Started

See the example project for a simple example of how to use SimpleFlux:

```bash
dotnet restore
dotnet build
dotnet run --project sample/SimpleFlux.Sample
```

The sample needs an Azure Storage emulator (Azurite) running for
`UseDevelopmentStorage=true`.

## Releasing

Prereleases and stable releases are published through GitHub Actions — see
[RELEASING.md](RELEASING.md) for the complete guide.

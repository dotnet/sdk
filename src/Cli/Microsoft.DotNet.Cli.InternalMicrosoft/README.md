# Microsoft.DotNet.Cli.InternalMicrosoft

This internal library owns the implementation of the .NET CLI's
Microsoft-internal machine classification. It contains the detector, cache, provider
pipeline, platform probes, and result contract.

The project root contains the detector's consumer-facing contract and entry point.
Shared implementation details are under `Internal`, and individual detection
mechanisms are under `Providers`.

The library does not gather or export telemetry. `src/Cli/dotnet/Telemetry` composes the
detector and maps its result to telemetry tags.

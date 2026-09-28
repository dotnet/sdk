# Telemetry Integration Demo

Minimal example showing how to capture telemetry activities from `Microsoft.Dotnet.Installation`.

## Usage

```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Microsoft.Dotnet.Installation",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = activity => activity.SetTag("caller", "MyTool"),
    ActivityStopped = activity => Console.WriteLine($"{activity.DisplayName}: {activity.Duration.TotalMilliseconds}ms")
};
ActivitySource.AddActivityListener(listener);

// Use the library - activities are captured automatically
```

## Available Activities

| Activity | Tags |
|----------|------|
| `download` | `download.version`, `download.url`, `download.bytes`, `download.from_cache` |
| `extract` | `download.version` |

## Production Requirements

If collecting telemetry, you must:
- Display a first-run notice
- Honor `DOTNET_CLI_TELEMETRY_OPTOUT`
- Provide documentation

See: https://learn.microsoft.com/dotnet/core/tools/telemetry

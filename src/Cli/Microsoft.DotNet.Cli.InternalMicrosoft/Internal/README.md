# Internal implementation

This directory contains implementation details that the detector and providers share.
The detector's consumer-facing contract and main entry point remain in the project root.

| File | Purpose |
| --- | --- |
| `GitHubMembershipClient.cs` | Checks GitHub organization membership without retaining credentials. |
| `InternalMicrosoftDetectionContext.cs` | Supplies platform state, process execution, HTTP access, and other external dependencies. |
| `InternalMicrosoftDetectionModels.cs` | Defines probe, process, cache, and serialization models used during detection. |
| `InternalMicrosoftDetectionProvider.cs` | Defines the provider contract and provider execution stages. |
| `InternalMicrosoftDetectionUtilities.cs` | Provides shared identity parsing, validation, and failure conversion. |
| `InternalMicrosoftDetectorOptions.cs` | Defines detector timeouts and provider-stage construction. |

Code in this directory must not emit telemetry. It must not retain credentials or
include sensitive process output in diagnostics.

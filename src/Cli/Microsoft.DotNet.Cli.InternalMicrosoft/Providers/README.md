# Detection providers

This directory contains the concrete sources of Microsoft-internal identity evidence.
Each provider declares its supported platforms and execution stage.

| File | Purpose |
| --- | --- |
| `CopilotCliDetectionProvider.cs` | Uses an authenticated Copilot CLI session to check GitHub organization membership. |
| `EnvironmentGitHubTokenDetectionProvider.cs` | Checks eligible GitHub tokens from the environment outside CI. |
| `GitHubCliDetectionProvider.cs` | Uses an authenticated GitHub CLI session to check GitHub organization membership. |
| `MacPlatformSsoDetectionProvider.cs` | Validates Microsoft Platform SSO registration and identity data on macOS. |
| `UserDnsDomainDetectionProvider.cs` | Checks the Windows user DNS domain on Windows or through the Windows host from WSL. |
| `VisualStudioAccountDetectionProvider.cs` | Validates Microsoft tenant identities in the Visual Studio account store. |
| `WorkplaceJoinDetectionProvider.cs` | Validates Microsoft workplace-join data on Windows or through the Windows host from WSL. |

Providers return classification evidence to the detector. Providers must not emit
telemetry, retain credentials, or return sensitive process output in diagnostics.

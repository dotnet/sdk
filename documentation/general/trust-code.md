# Overview
The .NET SDK is intended to be a collection of tools for manipulating, building, and executing code. As a result, there is no way to ensure that it is 100% secure regardless of how it is used. This document attempts to make clear the line between what security threats are up to the user and what threats the SDK should protect against. It defines the trust boundary where the user can assume that as long as they are secure in the ways outlined in this document, they will not be compromised by an outside threat actor.

# Types of Threats

## Untrusted Source Code
You should never build or run code from an untrusted source without employing mitigating factors like those described [here](ExecutingCustomerCode.md). Additionally, some SDK commands will automatically find code or code-adjacent files in the current directory, project directory, executable directory, or any parent directory of any of those up to the file system root. Files that may affect a build or other command if found include but are not limited to a Directory.Build.props, global.json, NuGet.config, or dotnet-tools.json. You should also check your home folder as well as environment variables that may point to other locations the SDK will search to find code or configuration files.

## Untrusted Locations
Some folders often store untrusted files. (The Downloads folder is a fairly straightforward example of that.) Some commands look "next to" the project, solution, or other code file being worked with in the directory. Do not run dotnet CLI commands from within any directory that contains any files you do not trust.

## Untrusted Packages
Since many SDK commands utilize NuGet under the covers, we further assume that:
* NuGet packages in local caches are trustworthy.
* NuGet.Config, Packages.Config, .props, and similar files within our resolution scope (see previous comments on trusted and untrusted directories) point only to trusted package repositories and/or packages.

See: [Managing the global packages and cache folders](https://learn.microsoft.com/nuget/consume-packages/managing-the-global-packages-and-cache-folders), [Managing package trust boundaries](https://learn.microsoft.com/nuget/consume-packages/installing-signed-packages), and [Consuming packages from authenticated feeds](https://learn.microsoft.com/nuget/consume-packages/consuming-packages-authenticated-feeds) for more information.

Violating these assumptions may lead to unwanted code execution.

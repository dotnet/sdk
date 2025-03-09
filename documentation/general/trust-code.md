# Overview
The .NET SDK is intended to be a collection of tools for manipulating, building, and executing code. As a result, there is no way to ensure that it is 100% secure regardless of how it is used. Nevertheless, the SDK team tries to protect users from any unexpected security gaps. It strikes a balance between enabling the functionality that empowers our users to achieve more while protecting users from bad actors trying to take compromise their machines.

This document attempts to make clear the line between what security threats are up to the user and what threats the SDK should protect against. It defines the trust boundary where the user can assume that as long as they are secure in the ways outlined in this document, they will not be compromised by an outside threat actor.

# Types of Threats

## Untrusted Source Code
You should never build or run code from an untrusted source without employing mitigating factors like those described [here](https://github.com/dotnet/sdk/blob/main/documentation/general/ExecutingCustomerCode.md). Additionally, some SDK commands will automatically find code or code-adjacent files in the current directory, project directory, or executable directory, or any parent directory of any of those up to the file system root. Files that may affect a build or other command if found include but are not limited to a Directory.Build.props, global.json, NuGet.config, or dotnet-tools.json. You should also check your home folder as well as environment variables that may point to other locations the SDK will search to find code or configuration files.

## Untrusted Locations
Some folders often store untrusted files. (The Downloads folder is a fairly straightforward example of that.) Some commands look "next to" the project, solution, or other code file being worked with in the directory. It is generally recommended to avoid running dotnet commands from within any directory that contains any files you do not trust.

## Untrusted Packages
NuGet follows a preset order in looking for packages in [various caches](https://learn.microsoft.com/nuget/consume-packages/managing-the-global-packages-and-cache-folders). It first searches the (expanded) global-packages folder, followed by the (compressed and short-lived) http-cache before finally using the NuGet.config file to determine where to find any unfound packages. Of note, it does not always search the sources listed in the NuGet.config, as that is often slower than the alternatives. On the other hand, a malicious NuGet package can be found if one is cached or even if a downloaded package in a temp folder is modified before it can be expanded. Ensure that all NuGet packages you use are from trusted and verified sources.

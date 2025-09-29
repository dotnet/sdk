// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli;

internal static class CliUsage
{
    public static readonly string HelpText =
$@"{CliCommandStrings.Usage}: dotnet [runtime-options] [path-to-application] [arguments]

{CliCommandStrings.ExecutionUsageDescription}

runtime-options:
  --additionalprobingpath <path>   {CliCommandStrings.AdditionalProbingPathDefinition}
  --additional-deps <path>         {CliCommandStrings.AdditionalDeps}
  --depsfile                       {CliCommandStrings.DepsFileDefinition}
  --fx-version <version>           {CliCommandStrings.FxVersionDefinition}
  --roll-forward <setting>         {CliCommandStrings.RollForwardDefinition}
  --runtimeconfig                  {CliCommandStrings.RuntimeConfigDefinition}

path-to-application:
  {CliCommandStrings.PathToApplicationDefinition}

{CliCommandStrings.Usage}: dotnet [sdk-options] [command] [command-options] [arguments]

{CliCommandStrings.SDKCommandUsageDescription}

sdk-options:
  -d|--diagnostics  {CliStrings.SDKDiagnosticsCommandDefinition}
  -h|--help         {CliCommandStrings.SDKOptionsHelpDefinition}
  --info            {CliCommandStrings.SDKInfoCommandDefinition}
  --list-runtimes   {CliCommandStrings.SDKListRuntimesCommandDefinition}
  --list-sdks       {CliCommandStrings.SDKListSdksCommandDefinition}
  --version         {CliCommandStrings.SDKVersionCommandDefinition}

{CliCommandStrings.Commands}:
  build             {CliCommandStrings.BuildDefinition}
  build-server      {CliCommandStrings.BuildServerDefinition}
  clean             {CliCommandStrings.CleanDefinition}
  format            {CliCommandStrings.FormatDefinition}
  help              {CliCommandStrings.HelpDefinition}
  msbuild           {CliCommandStrings.MsBuildDefinition}
  new               {CliCommandStrings.NewDefinition}
  nuget             {CliCommandStrings.NugetDefinition}
  pack              {CliCommandStrings.PackDefinition}
  package           {CliCommandStrings.PackageDefinition}
  publish           {CliCommandStrings.PublishDefinition}
  reference         {CliCommandStrings.ReferenceDefinition}
  restore           {CliCommandStrings.RestoreDefinition}
  run               {CliCommandStrings.RunDefinition}
  sdk               {CliCommandStrings.SdkDefinition}
  solution          {CliCommandStrings.SlnDefinition}
  store             {CliCommandStrings.StoreDefinition}
  test              {CliCommandStrings.TestDefinition}
  tool              {CliCommandStrings.ToolDefinition}
  vstest            {CliCommandStrings.VsTestDefinition}
  workload          {CliCommandStrings.WorkloadDefinition}

{CliCommandStrings.AdditionalTools}
  dev-certs         {CliCommandStrings.DevCertsDefinition}
  fsi               {CliCommandStrings.FsiDefinition}
  user-jwts         {CliCommandStrings.UserJwtsDefinition}
  user-secrets      {CliCommandStrings.UserSecretsDefinition}
  watch             {CliCommandStrings.WatchDefinition}

{CliCommandStrings.RunDotnetCommandHelpForMore}";
}

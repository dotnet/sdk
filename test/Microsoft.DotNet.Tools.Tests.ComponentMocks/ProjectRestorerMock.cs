// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ProjectRestorerMock
    {
        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string DefaultToolCommandName = "SimulatorCommand";
        public const string DefaultPackageName = "global.tool.console.demo";
        public const string DefaultPackageVersion = "1.0.4";
        public const string FakeCommandSettingsFileName = "FakeDotnetToolSettings.json";
    }
}

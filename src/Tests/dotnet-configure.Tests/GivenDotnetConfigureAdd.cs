// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Tools.Configure;

namespace Microsoft.DotNet.Cli.Configure.Add.Tests
{
    public class GivenDotnetConfigureAdd : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Add new config or platform to a solution file.

Usage:
  dotnet configure <SLN_FILE> add [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  -c, --config <config>           New config to add to the solution.
  -p, --platform <platform>       New platform to add to the solution.
  -cf, --copyfrom <copyfrom>      Existing config or platform to copy project settings from.
  -up, --updateproj <updateproj>  Indicates whether to update project config or platform settings.
  -?, -h, --help                  Show command line help";

        public GivenDotnetConfigureAdd(ITestOutputHelper log) : base(log)
        {
        }

        private const string ExpectedSlnWhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_n = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		newconfig|Any CPU = newconfig|Any CPU
		newconfig|x64 = newconfig|x64
		newconfig|x86 = newconfig|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.Build.0 = Debug|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnWhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_y = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		newconfig|Any CPU = newconfig|Any CPU
		newconfig|x64 = newconfig|x64
		newconfig|x86 = newconfig|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.ActiveCfg = newconfig|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.Build.0 = newconfig|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.ActiveCfg = newconfig|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.Build.0 = newconfig|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.ActiveCfg = newconfig|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.Build.0 = newconfig|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnWhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_n = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.3.32929.385
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Lib"", ""Lib\Lib.csproj"", ""{D4ED07FD-E893-4D38-8E19-33C3027D687A}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""First"", ""Multiple\First.csproj"", ""{F1383793-E608-49EA-A9BE-B7C26E94850B}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Second"", ""Multiple\Second.csproj"", ""{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		newconfig|Any CPU = newconfig|Any CPU
		newconfig|x64 = newconfig|x64
		newconfig|x86 = newconfig|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.ActiveCfg = Debug|x86
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x64.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x64.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x86.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x86.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|Any CPU.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x64.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x64.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x86.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x86.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|Any CPU.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|Any CPU.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x64.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x64.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x86.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x86.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x64.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x64.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x86.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x86.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|Any CPU.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x64.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x64.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x86.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x86.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|Any CPU.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|Any CPU.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x64.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x64.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x86.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x86.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x64.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x64.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x86.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x86.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|Any CPU.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x64.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x64.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x86.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x86.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|Any CPU.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|Any CPU.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x64.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x64.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x86.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x86.Build.0 = Debug|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {69061246-1432-4C95-A7C9-B17C637C8639}
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnWhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_y = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.3.32929.385
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Lib"", ""Lib\Lib.csproj"", ""{D4ED07FD-E893-4D38-8E19-33C3027D687A}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""First"", ""Multiple\First.csproj"", ""{F1383793-E608-49EA-A9BE-B7C26E94850B}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""Second"", ""Multiple\Second.csproj"", ""{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		newconfig|Any CPU = newconfig|Any CPU
		newconfig|x64 = newconfig|x64
		newconfig|x86 = newconfig|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|Any CPU.ActiveCfg = newconfig|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x64.ActiveCfg = newconfig|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.newconfig|x86.ActiveCfg = newconfig|x86
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x64.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x64.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x86.ActiveCfg = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Debug|x86.Build.0 = Debug|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|Any CPU.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x64.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x64.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x86.ActiveCfg = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.Release|x86.Build.0 = Release|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|Any CPU.ActiveCfg = newconfig|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|Any CPU.Build.0 = newconfig|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x64.ActiveCfg = newconfig|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x64.Build.0 = newconfig|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x86.ActiveCfg = newconfig|Any CPU
		{D4ED07FD-E893-4D38-8E19-33C3027D687A}.newconfig|x86.Build.0 = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x64.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x64.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x86.ActiveCfg = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Debug|x86.Build.0 = Debug|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|Any CPU.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x64.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x64.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x86.ActiveCfg = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.Release|x86.Build.0 = Release|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|Any CPU.ActiveCfg = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|Any CPU.Build.0 = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x64.ActiveCfg = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x64.Build.0 = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x86.ActiveCfg = newconfig|Any CPU
		{F1383793-E608-49EA-A9BE-B7C26E94850B}.newconfig|x86.Build.0 = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x64.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x64.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x86.ActiveCfg = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Debug|x86.Build.0 = Debug|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|Any CPU.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x64.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x64.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x86.ActiveCfg = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.Release|x86.Build.0 = Release|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|Any CPU.ActiveCfg = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|Any CPU.Build.0 = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x64.ActiveCfg = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x64.Build.0 = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x86.ActiveCfg = newconfig|Any CPU
		{5F64398F-08F6-4FDA-87DE-6F138AF81DC8}.newconfig|x86.Build.0 = newconfig|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {69061246-1432-4C95-A7C9-B17C637C8639}
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnWhenNewConfigIsAddedSlnFileWithNoProjectReferencesSlnIsUpdated = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
		newconfig|Any CPU = newconfig|Any CPU
		newconfig|x64 = newconfig|x64
		newconfig|x86 = newconfig|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
"; 

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"configure", "add", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"configure {commandName}".Trim().Split());
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }
        
        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log)
                .Execute("configure", "one.sln", "two.sln", "three.sln", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
        }
        
        [Theory]
        [InlineData("idontexist.sln")]
        [InlineData("ihave?invalidcharacters")]
        [InlineData("ihaveinv@lidcharacters")]
        [InlineData("ihaveinvalid/characters")]
        [InlineData("ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"configure", solutionName, "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
        
        [Fact]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetConfigureAdd")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "InvalidSolution.sln", "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        
        [Fact]
        public void WhenInvalidSolutionIsFoundAddPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionPath, LocalizableStrings.FileHeaderMissingError));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
        
        [Fact]
        public void WhenNoConfigIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferenceToSingleCsproj", identifier: "GivenDotnetConfigureAdd")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"configure", "App.sln", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(LocalizableStrings.ConfigureAddNewConfigPlatformNameMissing);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoUnpdateProjIsPassedItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferenceToSingleCsproj", identifier: "GivenDotnetConfigureAdd")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"configure", "App.sln", "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(LocalizableStrings.ConfigureAddNewConfigOptionUpdateProjMissing);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Fact]
        public void WhenNoSolutionExistsInTheDirectoryAddPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferenceToSingleCsproj")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(@"configure", "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
        
        [Fact]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnAdd")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(@"configure", "add", "-c", "newconfig");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }
        
        [Fact]
        public void WhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_n()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferenceToSingleCsproj")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "App.sln", "add", "-c", "newconfig", "-cf", "Debug", "-up", "n");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnWhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_n);
        }

        [Fact]
        public void WhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_y()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferenceToSingleCsproj")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "App.sln", "add", "-c", "newconfig", "-cf", "Debug", "-up", "y");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnWhenNewConfigIsAddedSingleProjCopyfromUpdateProjSlnIsUpdated_y);

        }

        [Fact]
        public void WhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_n()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferencesToMultipleCsproj")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "App.sln", "add", "-c", "newconfig", "-cf", "Debug", "-up", "n");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnWhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_n);
        }

        [Fact]
        public void WhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_y()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndReferencesToMultipleCsproj")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "App.sln", "add", "-c", "newconfig", "-cf", "Debug", "-up", "y");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnWhenNewConfigIsAddedMultipleProjCopyfromUpdateProjSlnIsUpdated_y);

        }

        [Fact]
        public void WhenNewConfigIsAddedSlnFileWithNoProjectReferencesSlnIsUpdated()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndCSharpProject")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"configure", "App.sln", "add", "-c", "newconfig", "-cf", "Debug", "-up", "y");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnWhenNewConfigIsAddedSlnFileWithNoProjectReferencesSlnIsUpdated);
        }

        private string GetExpectedSlnContents(
            string slnPath,
            string slnTemplate,
            string expectedLibProjectGuid = null)
        {
            var slnFile = SlnFile.Read(slnPath);

            if (string.IsNullOrEmpty(expectedLibProjectGuid))
            {
                var matchingProjects = slnFile.Projects
                    .Where((p) => p.FilePath.EndsWith("Lib.csproj"))
                    .ToList();

                matchingProjects.Count.Should().Be(1);
                var slnProject = matchingProjects[0];
                expectedLibProjectGuid = slnProject.Id;
            }
            var slnContents = slnTemplate.Replace("__LIB_PROJECT_GUID__", expectedLibProjectGuid);

            var matchingSrcFolder = slnFile.Projects
                    .Where((p) => p.FilePath == "src")
                    .ToList();
            if (matchingSrcFolder.Count == 1)
            {
                slnContents = slnContents.Replace("__SRC_FOLDER_GUID__", matchingSrcFolder[0].Id);
            }

            var matchingSolutionFolder = slnFile.Projects
                    .Where((p) => p.FilePath == "TestFolder")
                    .ToList();
            if (matchingSolutionFolder.Count == 1)
            {
                slnContents = slnContents.Replace("__SOLUTION_FOLDER_GUID__", matchingSolutionFolder[0].Id);
            }

            return slnContents;
        }
    }
}

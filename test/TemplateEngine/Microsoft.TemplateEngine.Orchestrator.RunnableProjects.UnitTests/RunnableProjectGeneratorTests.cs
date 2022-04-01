// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using static Microsoft.TemplateEngine.Orchestrator.RunnableProjects.RunnableProjectGenerator;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class RunnableProjectGeneratorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public RunnableProjectGeneratorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async void CreateAsyncTest_GuidsMacroProcessingCaseSensitivity()
        {
            //
            // Template content preparation
            //

            Guid inputTestGuid = new Guid("12aa8f4e-a4aa-4ac1-927c-94cb99485ef1");
            string contentFileNamePrefix = "content - ";

            string guidsConfigFormat = @"
{{
  ""guids"": [
    ""{0}"" 
  ]
}}
";
            string guidsConfig = string.Format(guidsConfigFormat, inputTestGuid);

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, guidsConfig);

            //content
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                templateSourceFiles.Add(contentFileNamePrefix + guidFormat, inputTestGuid.ToString(guidFormat));
            }

            //
            // Dependencies preparation and mounting
            //

            IEngineEnvironmentSettings environment = _environmentSettingsHelper.CreateEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TemplateConfigTestHelpers.WriteTemplateSource(environment, sourceBasePath, templateSourceFiles);
            IMountPoint? sourceMountPoint = TemplateConfigTestHelpers.CreateMountPoint(environment, sourceBasePath);
            IRunnableProjectConfig runnableConfig = TemplateConfigTestHelpers.ConfigFromSource(environment, sourceMountPoint!);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            IParameterSet parameters = new ParameterSet(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/");

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await rpg.CreateAsync(environment, runnableConfig, sourceDir, parameters, targetDir, CancellationToken.None);

            //
            // Veryfying the outputs
            //

            Guid expectedResultGuid = Guid.Empty;
            foreach (string guidFormat in GuidMacroConfig.DefaultFormats.Select(c => c.ToString()))
            {
                string resultContent = environment.Host.FileSystem.ReadAllText(Path.Combine(targetDir, contentFileNamePrefix + guidFormat));
                Guid resultGuid;
                Assert.True(
                    Guid.TryParseExact(resultContent, guidFormat, out resultGuid),
                    $"Expected the result conent ({resultContent}) to be parseable by Guid format '{guidFormat}'");

                if (expectedResultGuid == Guid.Empty)
                {
                    expectedResultGuid = resultGuid;
                }
                else
                {
                    Assert.Equal(expectedResultGuid, resultGuid);
                }
            }
            Assert.NotEqual(inputTestGuid, expectedResultGuid);
        }
    }
}

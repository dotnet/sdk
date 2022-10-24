// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.IntegrationTests
{
    public class ExampleTemplateTest : TestBase
    {
        private readonly ILogger _log;

        public ExampleTemplateTest(ITestOutputHelper log)
        {
            _log = new XunitLoggerProvider(log).CreateLogger("TestRun");
        }

        [Fact]
        public async void VerificationEngineSampleDogfoodTest()
        {
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string templateShortName = "TestAssets.SampleTestTemplate";

            //get the template location
            string templateLocation = Path.Combine(TestTemplatesLocation, "TestTemplate");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplateSpecificArgs = new string[] { "--paramB", "true" },
                TemplatePath = templateLocation,
                SnapshotsDirectory = "Snapshots",
                OutputDirectory = workingDir,
                VerifyCommandOutput = true,
                DoNotPrependTemplateNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = true,
                UniqueFor = UniqueForOption.Architecture,
            }
                .WithCustomScrubbers(
                    ScrubbersDefinition.Empty
                        .AddScrubber(sb => sb.Replace("B is enabled", "*******"))
                        .AddScrubber((path, content) =>
                        {
                            if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                            {
                                content.Replace("SampleTestTemplate", "%TEMPLATE%");
                            }
                        }));

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options);
        }
    }
}

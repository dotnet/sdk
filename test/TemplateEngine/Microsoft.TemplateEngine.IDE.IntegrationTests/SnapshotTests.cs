// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateApiVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class SnapshotTests : TestBase
    {
        private readonly ILogger _log;

        public SnapshotTests(ITestOutputHelper log)
        {
            _log = new XunitLoggerProvider(log).CreateLogger("TestRun");
        }

        [Fact]
        public Task PreferDefaultNameTest()
        {
            string templateLocation = GetTestTemplateLocation("TemplateWithPreferDefaultName");

            TemplateVerifierOptions options =
                new TemplateVerifierOptions(templateName: "TestAssets.TemplateWithPreferDefaultName")
                {
                    TemplatePath = templateLocation,
                    SnapshotsDirectory = "Approvals"
                }
                    .WithInstantiationThroughTemplateCreatorApi(new Dictionary<string, string?>());

            VerificationEngine engine = new VerificationEngine(_log);
            return engine.Execute(options);
        }
    }
}

#endif

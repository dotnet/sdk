// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.IO.Compression;
using FakeItEasy;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    [ResourceLock(nameof(Reporter))]
    public class TemplateCommandTests : VerifyBase
    {
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task BuiltInPackageNoticeHonorsNoUpdateCheck(bool noUpdateCheck, bool dryRun)
        {
            const string PackageId = "Test.Templates";
            const string BuiltInVersion = "2.0.0";
            var managedFactory = A.Fake<ITemplatePackageProviderFactory>(options => options.Implements<IPrioritizedComponent>());
            var builtInFactory = A.Fake<ITemplatePackageProviderFactory>();
            var managedProvider = A.Fake<IManagedTemplatePackageProvider>();
            var builtInProvider = A.Fake<ITemplatePackageProvider>();
            A.CallTo(() => managedFactory.Id).Returns(Guid.NewGuid());
            A.CallTo(() => ((IPrioritizedComponent)managedFactory).Priority).Returns(100);
            A.CallTo(() => builtInFactory.Id).Returns(Guid.NewGuid());
            A.CallTo(() => builtInFactory.DisplayName).Returns("Optional workloads");
            A.CallTo(() => managedFactory.CreateProvider(A<IEngineEnvironmentSettings>._)).Returns(managedProvider);
            A.CallTo(() => builtInFactory.CreateProvider(A<IEngineEnvironmentSettings>._)).Returns(builtInProvider);
            A.CallTo(() => managedProvider.Factory).Returns(managedFactory);
            A.CallTo(() => builtInProvider.Factory).Returns(builtInFactory);

            using ICliTemplateEngineHost virtualHost = CliTestHostFactory.GetVirtualHost(additionalComponents:
            [
                (typeof(ITemplatePackageProviderFactory), managedFactory),
                (typeof(ITemplatePackageProviderFactory), builtInFactory)
            ]);
            ICliTemplateEngineHost host = A.Fake<ICliTemplateEngineHost>(options => options.Wrapping(virtualHost));
            using IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            string outputDirectory = settings.GetTempVirtualizedPath();
            A.CallTo(() => host.OutputPath).Returns(outputDirectory);
            A.CallTo(() => host.IsCustomOutputPath).Returns(true);
            string packageDirectory = settings.GetTempVirtualizedPath();
            string managedPath = Path.Combine(packageDirectory, "managed.nupkg");
            string builtInPath = Path.Combine(packageDirectory, "built-in.nupkg");
            CreatePackage(managedPath, "1.0.0");
            CreatePackage(builtInPath, BuiltInVersion);

            var managedPackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => managedPackage.Identifier).Returns(PackageId);
            A.CallTo(() => managedPackage.Version).Returns("1.0.0");
            A.CallTo(() => managedPackage.MountPointUri).Returns(managedPath);
            A.CallTo(() => managedPackage.Provider).Returns(managedProvider);
            A.CallTo(() => managedPackage.ManagedProvider).Returns(managedProvider);
            A.CallTo(() => managedProvider.GetAllTemplatePackagesAsync(A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<ITemplatePackage>>([managedPackage]));
            A.CallTo(() => builtInProvider.GetAllTemplatePackagesAsync(A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<ITemplatePackage>>([new TemplatePackage(builtInProvider, builtInPath, DateTime.UtcNow)]));
            A.CallTo(() => managedProvider.GetLatestVersionsAsync(A<IEnumerable<IManagedTemplatePackage>>._, A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<CheckUpdateResult>>([CheckUpdateResult.CreateSuccess(managedPackage, "1.0.0", isLatest: true)]));

            using TemplatePackageManager packageManager = new(settings);
            var templates = await packageManager.GetTemplatesAsync(TestContext.CancellationToken);
            Assert.AreEqual(managedPath, templates.Single().MountPointUri);
            TemplateGroup group = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(templates, new HostSpecificDataLoader(settings))).Single();
            var newCommand = CliTestHostFactory.CreateNewCommand(host);
            TemplateCommand command = new(newCommand, settings, packageManager, group, group.Templates.Single());
            newCommand.Add(command);
            var parseResult = newCommand.Parse($"new test --output \"{outputDirectory}\"{(noUpdateCheck ? " --no-update-check" : "")}{(dryRun ? " --dry-run" : "")}");
            Assert.IsEmpty(parseResult.Errors);

            IReporter originalOutput = Reporter.Output;
            IReporter originalError = Reporter.Error;
            BufferedReporter output = new();
            BufferedReporter error = new();
            try
            {
                Reporter.SetOutput(output);
                Reporter.SetError(error);

                Assert.AreEqual(NewCommandStatus.Success, await command.InvokeAsync(parseResult, TestContext.CancellationToken));

                Assert.IsEmpty(error.Lines);
                string notice = string.Format(
                    LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_BuiltInPackageAvailable,
                    $"{PackageId}@{BuiltInVersion}",
                    builtInFactory.DisplayName);
                Assert.AreEqual(!noUpdateCheck, output.Lines.Contains(notice));
                Assert.AreEqual(!noUpdateCheck, output.Lines.Contains(LocalizableStrings.TemplatePackageCoordinator_BuiltInCheck_Info_UninstallPackage));
                Assert.AreEqual(!noUpdateCheck, output.Lines.Any(line => line.Contains($"new uninstall {PackageId}", StringComparison.Ordinal)));
                Assert.AreEqual(!dryRun, host.FileSystem.FileExists(Path.Combine(outputDirectory, "content.txt")));

                A.CallTo(() => managedProvider.GetLatestVersionsAsync(A<IEnumerable<IManagedTemplatePackage>>._, A<CancellationToken>._))
                    .MustHaveHappened(noUpdateCheck ? 0 : 1, Times.Exactly);
            }
            finally
            {
                Reporter.SetOutput(originalOutput);
                Reporter.SetError(originalError);
            }

            void CreatePackage(string path, string version)
            {
                using Stream stream = host.FileSystem.CreateFile(path);
                using ZipArchive archive = new(stream, ZipArchiveMode.Create);
                WriteEntry($"{PackageId}.nuspec", $"""
                    <package>
                      <metadata>
                        <id>{PackageId}</id>
                        <version>{version}</version>
                        <authors>Test</authors>
                        <description>Test template package</description>
                      </metadata>
                    </package>
                    """);
                WriteEntry(".template.config/template.json", """
                    {
                      "identity": "Test.Template",
                      "name": "Test template",
                      "shortName": "test"
                    }
                    """);
                WriteEntry("content.txt", "Template content");

                void WriteEntry(string name, string content)
                {
                    using StreamWriter writer = new(archive.CreateEntry(name).Open());
                    writer.Write(content);
                }
            }
        }

        [TestMethod]
        public Task CannotCreateCommandForInvalidParameter()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithParameters("have:colon", "n1", "n2");

            var paramSymbolInfo = new Dictionary<string, string>()
            {
                { "longName", "name" },
                { "shortName", "n" }
            };
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                { "n1", paramSymbolInfo },
                { "n2", paramSymbolInfo }
            };

            IHostSpecificDataLoader hostDataLoader = A.Fake<IHostSpecificDataLoader>();
            A.CallTo(() => hostDataLoader.ReadHostSpecificTemplateData(template)).Returns(new HostSpecificTemplateData(symbolInfo));

            TemplateGroup templateGroup = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, hostDataLoader))
                .Single();

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            InvalidTemplateParametersException e = Assert.ThrowsExactly<InvalidTemplateParametersException>(
                () => _ = new TemplateCommand(myCommand, settings, packageManager, templateGroup, templateGroup.Templates.Single()));

            Assert.HasCount(2, e.ParameterErrors);
            Assert.AreEqual(templateGroup.Templates.Single(), e.Template);

            return Verify(e.Message);
        }

        [TestMethod]
        public async Task Constraints_WhenTheTemplateIsAllowed()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test", identity: "testId1").WithConstraints(new TemplateConstraintInfo("test", "yes"));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.IsEmpty((await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, TestContext.CancellationToken)));
        }

        [TestMethod]
        public async Task Constraints_WhenTheTemplateIsRestricted()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test").WithConstraints(new TemplateConstraintInfo("test", "no"));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.IsNotEmpty((await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, TestContext.CancellationToken)));
        }

        [TestMethod]
        public async Task Constraints_WhenTheConstraintCannotBeEvaluated()
        {
            MockTemplateInfo template = new MockTemplateInfo(shortName: "test").WithConstraints(new TemplateConstraintInfo("test", "bad-arg"));
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var templateConstraintManager = new TemplateConstraintManager(settings);

            Assert.IsNotEmpty((await TemplateCommand.ValidateConstraintsAsync(templateConstraintManager, template, TestContext.CancellationToken)));
        }

    }
}

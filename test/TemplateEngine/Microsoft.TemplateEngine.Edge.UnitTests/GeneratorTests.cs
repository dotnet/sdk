// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Microsoft.TemplateEngine.Utils;
using Xunit;
using ITemplateMatchInfo = Microsoft.TemplateEngine.Abstractions.TemplateFiltering.ITemplateMatchInfo;
using WellKnownSearchFilters = Microsoft.TemplateEngine.Utils.WellKnownSearchFilters;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class GeneratorTests : TestBase
    {
        [Fact]
        public async Task CanUseCustomGenerator()
        {
            var builtIns = new List<(Type, IIdentifiedComponent)>()
            {
                (typeof(IGenerator), new CustomGenerator())
            };
            builtIns.AddRange(BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation));
            builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Components.AllComponents);

            using ITemplateEngineHost testHost = TestHost.GetVirtualHost("generatorTest", additionalComponents: builtIns);
            using IEngineEnvironmentSettings engineEnvironmentSettings = new EngineEnvironmentSettings(testHost);
            TemplateCreator templateCreator = new(engineEnvironmentSettings);
            TemplatePackageManager templatePackagesManager = new(engineEnvironmentSettings);

            IReadOnlyList<ITemplateMatchInfo> foundTemplates = await templatePackagesManager.GetTemplatesAsync(
                matchFilter: WellKnownSearchFilters.MatchesAllCriteria,
                filters: new[] { WellKnownSearchFilters.NameFilter("test-template") },
                cancellationToken: default);
            ITemplateMatchInfo template = Assert.Single(foundTemplates);

            string output = TestUtils.CreateTemporaryFolder();
            ITemplateCreationResult dryRunResult = await templateCreator.InstantiateAsync(
                template.Info,
                "MyProject",
                fallbackName: null,
                outputPath: output,
                inputParameters: new Dictionary<string, string?>(),
                forceCreation: true,
                dryRun: true);

            Assert.Equal(CreationResultStatus.Success, dryRunResult.Status);
            Assert.Equal((ICreationEffects)CustomGenerator.CreationEffects.Instance, dryRunResult.CreationEffects);

            ITemplateCreationResult runResult = await templateCreator.InstantiateAsync(
                template.Info,
                "MyProject",
                fallbackName: null,
                outputPath: output,
                inputParameters: new Dictionary<string, string?>(),
                forceCreation: true,
                dryRun: false);

            Assert.Equal(CreationResultStatus.Success, runResult.Status);
            Assert.Equal(CustomGenerator.SimpleCreationResult.Instance, runResult.CreationResult);

            string targetFile = Path.Combine(output, "success.txt");
            Assert.True(File.Exists(targetFile));
        }

        private class CustomGenerator : IGenerator
        {
            public Guid Id { get; } = new Guid("{AB083D9D-857A-419E-8394-113F97FFBD6B}");

            public object? ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError) => throw new NotImplementedException();

            [Obsolete]
            public Task<ICreationResult> CreateAsync(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSet parameters, string targetDirectory, CancellationToken cancellationToken) => throw new NotImplementedException();

            public Task<ICreationResult> CreateAsync(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSetData parameters, string targetDirectory, CancellationToken cancellationToken)
            {
                if (!environmentSettings.Host.FileSystem.DirectoryExists(targetDirectory))
                {
                    environmentSettings.Host.FileSystem.CreateDirectory(targetDirectory);
                }
                environmentSettings.Host.FileSystem.WriteAllText(Path.Combine(targetDirectory, "success.txt"), string.Empty);

                return Task.FromResult(SimpleCreationResult.Instance);
            }

            [Obsolete]
            public Task<ICreationEffects> GetCreationEffectsAsync(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSet parameters, string targetDirectory, CancellationToken cancellationToken) => throw new NotImplementedException();

            public Task<ICreationEffects> GetCreationEffectsAsync(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSetData parameters, string targetDirectory, CancellationToken cancellationToken)
            {
                return Task.FromResult((ICreationEffects)CreationEffects.Instance);
            }

            [Obsolete]
            public IParameterSet GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template) => throw new NotImplementedException();

            public IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations)
            {
                localizations = new List<ILocalizationLocator>();
                return new List<ITemplate>() { new Template(this, source) };
            }

            public Task<IReadOnlyList<IScanTemplateInfo>> GetTemplatesFromMountPointAsync(IMountPoint source, CancellationToken cancellationToken)
            {
                return Task.FromResult((IReadOnlyList<IScanTemplateInfo>)new List<IScanTemplateInfo>() { new Template(this, source) });
            }

            public Task<ITemplate?> LoadTemplateAsync(IEngineEnvironmentSettings settings, ITemplateLocator config, string? baselineName = null, CancellationToken cancellationToken = default)
            {
                if (!settings.TryGetMountPoint(config.MountPointUri, out IMountPoint? mountPoint))
                {
                    return Task.FromResult((ITemplate?)null);
                }
                if (mountPoint == null)
                {
                    throw new InvalidOperationException($"{nameof(mountPoint)} is null after {nameof(EngineEnvironmentSettingsExtensions.TryGetMountPoint)} returned true.");
                }

                return Task.FromResult((ITemplate?)new Template(this, mountPoint));
            }

            public bool TryEvaluateFromString(ILogger logger, string text, IDictionary<string, object> variables, out bool result, out string evaluationError, HashSet<string>? referencedVariablesKeys = null) => throw new NotImplementedException();

            public bool TryGetTemplateFromConfigInfo(IFileSystemInfo config, out ITemplate? template, IFileSystemInfo? localeConfig, IFile? hostTemplateConfigFile, string? baselineName = null)
            {
                template = new Template(this, config.MountPoint);
                return true;
            }

            internal class Template : ITemplate, IScanTemplateInfo
            {
                private readonly IMountPoint _mountPoint;

                public Template(IGenerator generator, IMountPoint mountPoint)
                {
                    Generator = generator;
                    _mountPoint = mountPoint;
                }

                public IGenerator Generator { get; }

                public IFileSystemInfo Configuration => _mountPoint.Root;

                public IFileSystemInfo? LocaleConfiguration => null;

                public IDirectory TemplateSourceRoot => _mountPoint.Root;

                public bool IsNameAgreementWithFolderPreferred => false;

                public string? Author => "Microsoft";

                public string? Description => "This is the description";

                public IReadOnlyList<string> Classifications => Array.Empty<string>();

                public string? DefaultName => null;

                public bool PreferDefaultName => false;

                public string Identity => "Static.Test.Template";

                public Guid GeneratorId => Generator.Id;

                public string? GroupIdentity => null;

                public int Precedence => 0;

                public string Name => "Test template";

                public string ShortName => "test-template";

                [Obsolete]
                public IReadOnlyDictionary<string, ICacheTag> Tags => throw new NotImplementedException();

                public IReadOnlyDictionary<string, string> TagsCollection { get; } = new Dictionary<string, string>();

                [Obsolete]
                public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => throw new NotImplementedException();

                public IParameterDefinitionSet ParameterDefinitions => ParameterDefinitionSet.Empty;

                public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

                public string MountPointUri => _mountPoint.MountPointUri;

                public string ConfigPlace => ".";

                public string? LocaleConfigPlace => null;

                public string? HostConfigPlace => null;

                public string? ThirdPartyNotices => null;

                public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo { get; } = new Dictionary<string, IBaselineInfo>();

                public bool HasScriptRunningPostActions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

                public IReadOnlyList<string> ShortNameList => new[] { ShortName };

                public IReadOnlyList<Guid> PostActions => Array.Empty<Guid>();

                public IReadOnlyList<TemplateConstraintInfo> Constraints => Array.Empty<TemplateConstraintInfo>();

                public IFileSystemInfo? HostSpecificConfiguration => null;

                public bool IsValid => true;

                public IReadOnlyList<IValidationEntry> ValidationErrors => Array.Empty<IValidationEntry>();

                public IReadOnlyDictionary<string, ILocalizationLocator> Localizations { get; } = new Dictionary<string, ILocalizationLocator>();

                public IReadOnlyDictionary<string, string> HostConfigFiles { get; } = new Dictionary<string, string>();

                public ILocalizationLocator? Localization => null;

                public void Dispose()
                {
                    _mountPoint.Dispose();
                }
            }

            internal class CreationEffects : ICreationEffects2, ICreationEffects
            {
                private CreationEffects() { }

                public static ICreationEffects2 Instance { get; } = new CreationEffects();

                public IReadOnlyList<IFileChange2> FileChanges => new[] { FileChange.Instance };

                public ICreationResult CreationResult => SimpleCreationResult.Instance;

                IReadOnlyList<IFileChange> ICreationEffects.FileChanges => new[] { FileChange.Instance };

                private class FileChange : IFileChange2, IFileChange
                {
                    private FileChange() { }

                    public static FileChange Instance { get; } = new FileChange();

                    public string SourceRelativePath => "success.txt";

                    public string TargetRelativePath => "success.txt";

                    public ChangeKind ChangeKind => ChangeKind.Create;

                    public byte[] Contents => throw new NotImplementedException();
                }
            }

            internal class SimpleCreationResult : ICreationResult
            {
                private SimpleCreationResult() { }

                public static ICreationResult Instance { get; } = new SimpleCreationResult();

                public IReadOnlyList<IPostAction> PostActions { get; } = Array.Empty<IPostAction>();

                public IReadOnlyList<ICreationPath> PrimaryOutputs => new[] { CreationPath.Instance };

                private class CreationPath : ICreationPath
                {
                    private CreationPath() { }

                    public string Path => "success.txt";

                    public static CreationPath Instance { get; } = new CreationPath();
                }
            }
        }
    }
}

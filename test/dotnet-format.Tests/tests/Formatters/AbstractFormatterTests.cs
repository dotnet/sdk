// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Composition;

#nullable enable

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public abstract class AbstractFormatterTest
    {
        private static MetadataReference MicrosoftVisualBasicReference => MetadataReference.CreateFromFile(typeof(Microsoft.VisualBasic.Strings).Assembly.Location);

        private static Lazy<IExportProviderFactory> ExportProviderFactory { get; }

        static AbstractFormatterTest()
        {
            ExportProviderFactory = new Lazy<IExportProviderFactory>(
                () =>
                {
                    var discovery = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
                    var parts = Task.Run(() => discovery.CreatePartsAsync(MefHostServices.DefaultAssemblies)).GetAwaiter().GetResult();
                    var catalog = ComposableCatalog.Create(Resolver.DefaultInstance).AddParts(parts);

                    var configuration = CompositionConfiguration.Create(catalog);
                    var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);
                    return runtimeComposition.CreateExportProviderFactory();
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        protected virtual ReferenceAssemblies ReferenceAssemblies => ReferenceAssemblies.NetStandard.NetStandard20;

        protected virtual string DefaultFilePathPrefix => "Test";

        protected virtual string DefaultTestProjectName => "TestProject";

        // This folder path needs to appear rooted when adding the AnalyzerConfigDocument.
        // We achieve this by prepending a directory separator.
        protected virtual string DefaultFolderPath => Path.DirectorySeparatorChar + DefaultTestProjectName;

        protected virtual string DefaultTestProjectPath => Path.Combine(DefaultFolderPath, $"{DefaultTestProjectName}.{DefaultFileExt}proj");

        protected virtual string DefaultEditorConfigPath => Path.Combine(DefaultFolderPath, ".editorconfig");

        protected virtual string DefaultFilePath => Path.Combine(DefaultFolderPath, $"{DefaultFilePathPrefix}0.{DefaultFileExt}");

        protected abstract string DefaultFileExt { get; }

        private protected abstract ICodeFormatter Formatter { get; }

        protected ITestOutputHelper? TestOutputHelper { get; set; }

        protected AbstractFormatterTest()
        {
            TestState = new SolutionState("Test", Language, DefaultFilePathPrefix, DefaultFileExt);
        }

        /// <summary>
        /// Gets the language name used for the test.
        /// </summary>
        public abstract string Language { get; }

        public SolutionState TestState { get; }

        private protected string ToEditorConfig(IReadOnlyDictionary<string, string> editorConfig) => $@"root = true

[*.{DefaultFileExt}]
{string.Join(Environment.NewLine, editorConfig.Select(kvp => $"{kvp.Key} = {kvp.Value}"))}
";

        private protected Task<SourceText> AssertNoReportedFileChangesAsync(
            string code,
            IReadOnlyDictionary<string, string> editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            return AssertNoReportedFileChangesAsync(code, ToEditorConfig(editorConfig), encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);
        }

        private protected async Task<SourceText> AssertNoReportedFileChangesAsync(
            string code,
            string editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            var (formattedText, formattedFiles, logger) = await ApplyFormatterAsync(code, editorConfig, encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);

            try
            {
                // Ensure the code is unchanged
                Assert.Equal(code, formattedText.ToString());

                // Ensure no non-fixable diagnostics were reported
                Assert.Empty(formattedFiles);
            }
            catch
            {
                TestOutputHelper?.WriteLine(logger.GetLog());
                throw;
            }

            return formattedText;
        }

        private protected Task<SourceText> AssertCodeUnchangedAsync(
            string code,
            IReadOnlyDictionary<string, string> editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            return AssertCodeUnchangedAsync(code, ToEditorConfig(editorConfig), encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);
        }

        private protected async Task<SourceText> AssertCodeUnchangedAsync(
            string code,
            string editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            var (formattedText, _, logger) = await ApplyFormatterAsync(code, editorConfig, encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);

            try
            {
                // Ensure the code is unchanged
                Assert.Equal(code, formattedText.ToString());
            }
            catch
            {
                TestOutputHelper?.WriteLine(logger.GetLog());
                throw;
            }

            return formattedText;
        }

        private protected Task<SourceText> AssertCodeChangedAsync(
            string testCode,
            string expectedCode,
            IReadOnlyDictionary<string, string> editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            return AssertCodeChangedAsync(testCode, expectedCode, ToEditorConfig(editorConfig), encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);
        }

        private protected async Task<SourceText> AssertCodeChangedAsync(
            string testCode,
            string expectedCode,
            string editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            var (formattedText, _, logger) = await ApplyFormatterAsync(testCode, editorConfig, encoding, fixCategory, analyzerReferences, codeStyleSeverity, analyzerSeverity, diagnostics);

            try
            {
                Assert.Equal(expectedCode, formattedText.ToString());
            }
            catch
            {
                TestOutputHelper?.WriteLine(logger.GetLog());
                throw;
            }

            return formattedText;
        }

        private protected async Task<(SourceText FormattedText, List<FormattedFile> FormattedFiles, TestLogger Logger)> ApplyFormatterAsync(
            string code,
            string editorConfig,
            Encoding? encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            IEnumerable<AnalyzerReference>? analyzerReferences = null,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[]? diagnostics = null)
        {
            var text = SourceText.From(code, encoding ?? Encoding.UTF8);
            TestState.Sources.Add(text);

            var (workspace, solution) = await GetSolutionAsync(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray(), editorConfig, analyzerReferences);
            var project = solution.Projects.Single();
            var document = project.Documents.Single();

            var fileMatcher = SourceFileMatcher.CreateMatcher(new[] { document.FilePath! }, exclude: Array.Empty<string>());
            var formatOptions = new FormatOptions(
                WorkspaceFilePath: project.FilePath!,
                WorkspaceType: WorkspaceType.Solution,
                NoRestore: false,
                LogLevel: LogLevel.Trace,
                fixCategory,
                codeStyleSeverity,
                analyzerSeverity,
                (diagnostics ?? Array.Empty<string>()).ToImmutableHashSet(),
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: true,
                ChangesAreErrors: false,
                fileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);

            var pathsToFormat = GetOnlyFileToFormat(solution);

            var logger = new TestLogger();
            var formattedFiles = new List<FormattedFile>();

            var formattedSolution = await Formatter.FormatAsync(workspace, solution, pathsToFormat, formatOptions, logger, formattedFiles, default);
            var formattedDocument = GetOnlyDocument(formattedSolution);
            var formattedText = await formattedDocument.GetTextAsync();

            return (formattedText, formattedFiles, logger);
        }

        /// <summary>
        /// Gets the only <see cref="DocumentId"/>.
        /// </summary>
        /// <param name="solution">A Solution containing a single Project containing a single Document.</param>
        /// <returns>The only document id.</returns>
        internal ImmutableArray<DocumentId> GetOnlyFileToFormat(Solution solution) => ImmutableArray.Create(GetOnlyDocument(solution).Id);

        /// <summary>
        /// Gets the only <see cref="Document"/> contained within the only <see cref="Project"/> within the <see cref="Solution"/>.
        /// </summary>
        /// <param name="solution">A Solution containing a single Project containing a single Document.</param>
        /// <returns>The document contained within.</returns>
        public Document GetOnlyDocument(Solution solution) => solution.Projects.Single().Documents.Single();

        /// <summary>
        /// Gets the collection of inputs to provide to the XML documentation resolver.
        /// </summary>
        /// <remarks>
        /// <para>Files in this collection may be referenced via <c>&lt;include&gt;</c> elements in documentation
        /// comments.</para>
        /// </remarks>
        public Dictionary<string, string> XmlReferences { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="editorConfig">The .editorconfig to apply to this solution.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private protected async Task<(Workspace workspace, Solution solution)> GetSolutionAsync((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, IReadOnlyDictionary<string, string> editorConfig, IEnumerable<AnalyzerReference>? analyzerReferences = null)
        {
            return await GetSolutionAsync(sources, additionalFiles, additionalMetadataReferences, ToEditorConfig(editorConfig), analyzerReferences);
        }

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="editorConfig">The .editorconfig to apply to this solution.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private protected async Task<(Workspace workspace, Solution solution)> GetSolutionAsync((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string editorConfig, IEnumerable<AnalyzerReference>? analyzerReferences = null)
        {
            analyzerReferences ??= Enumerable.Empty<AnalyzerReference>();
            var (workspace, project) = await CreateProjectAsync(sources, additionalFiles, additionalMetadataReferences, analyzerReferences, Language, SourceText.From(editorConfig, Encoding.UTF8));
            return (workspace, project.Solution);
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <remarks>
        /// <para>This method first creates a <see cref="Project"/> by calling <see cref="CreateProjectImpl"/>, and then
        /// applies compilation options to the project by calling <see cref="ApplyCompilationOptions"/>.</para>
        /// </remarks>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this project.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected async Task<(Workspace workspace, Project project)> CreateProjectAsync((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, IEnumerable<AnalyzerReference> analyzerReferences, string language, SourceText editorConfigText)
        {
            language ??= Language;
            return await CreateProjectImplAsync(sources, additionalFiles, additionalMetadataReferences, analyzerReferences, language, editorConfigText);
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this project.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected virtual async Task<(Workspace workspace, Project project)> CreateProjectImplAsync((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, IEnumerable<AnalyzerReference> analyzerReferences, string language, SourceText editorConfigText)
        {
            var projectId = ProjectId.CreateNewId(debugName: DefaultTestProjectName);
            var (workspace, solution) = await CreateSolutionAsync(projectId, language, editorConfigText);
            solution = solution
                .AddAnalyzerReferences(projectId, analyzerReferences)
                .AddMetadataReferences(projectId, additionalMetadataReferences);

            for (var i = 0; i < sources.Length; i++)
            {
                (var newFileName, var source) = sources[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, source, filePath: Path.Combine(DefaultFolderPath, newFileName));
            }

            for (var i = 0; i < additionalFiles.Length; i++)
            {
                (var newFileName, var source) = additionalFiles[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddAdditionalDocument(documentId, newFileName, source);
            }

            return (workspace, solution.GetProject(projectId)!);
        }

        /// <summary>
        /// Creates a solution that will be used as parent for the sources that need to be checked.
        /// </summary>
        /// <param name="projectId">The project identifier to use.</param>
        /// <param name="language">The language for which the solution is being created.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this solution.</param>
        /// <returns>The created solution.</returns>
        protected virtual async Task<(Workspace workspace, Solution solution)> CreateSolutionAsync(ProjectId projectId, string language, SourceText editorConfigText)
        {
            var xmlReferenceResolver = new TestXmlReferenceResolver();
            foreach (var xmlReference in XmlReferences)
            {
                xmlReferenceResolver.XmlReferences.Add(xmlReference.Key, xmlReference.Value);
            }

            var compilationOptions = CreateCompilationOptions()
                .WithXmlReferenceResolver(xmlReferenceResolver)
                .WithAssemblyIdentityComparer(ReferenceAssemblies.AssemblyIdentityComparer);

            var parseOptions = CreateParseOptions();
            var referenceAssemblies = await ReferenceAssemblies.ResolveAsync(language, CancellationToken.None);

            var editorConfigDocument = DocumentInfo.Create(
                DocumentId.CreateNewId(projectId, DefaultEditorConfigPath),
                name: DefaultEditorConfigPath,
                loader: TextLoader.From(TextAndVersion.Create(editorConfigText, VersionStamp.Create())),
                filePath: DefaultEditorConfigPath);

            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                name: DefaultTestProjectName,
                assemblyName: DefaultTestProjectName,
                language,
                filePath: DefaultTestProjectPath,
                outputFilePath: Path.ChangeExtension(DefaultTestProjectPath, "dll"),
                compilationOptions: compilationOptions,
                parseOptions: parseOptions,
                metadataReferences: referenceAssemblies,
                isSubmission: false)
                .WithDefaultNamespace(DefaultTestProjectName)
                .WithAnalyzerConfigDocuments(ImmutableArray.Create(editorConfigDocument));

            var workspace = CreateWorkspace();
            var solution = workspace
                .CurrentSolution
                .AddProject(projectInfo);

            if (language == LanguageNames.VisualBasic)
            {
                solution = solution.AddMetadataReference(projectId, MicrosoftVisualBasicReference);
            }

            return (workspace, solution);
        }

        public virtual AdhocWorkspace CreateWorkspace()
        {
            var exportProvider = ExportProviderFactory.Value.CreateExportProvider();
            var host = MefHostServices.Create(exportProvider.AsCompositionContext());
            return new AdhocWorkspace(host);
        }

        protected abstract CompilationOptions CreateCompilationOptions();

        protected abstract ParseOptions CreateParseOptions();
    }
}

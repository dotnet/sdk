// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.NET.Sdk.Razor.Tool.CommandLineUtils;
using Microsoft.NET.Sdk.Razor.Tool.Json;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal class DiscoverCommand : CommandBase
    {
        public DiscoverCommand(Application parent)
            : base(parent, "discover")
        {
            Assemblies = Argument("assemblies", "assemblies to search for tag helpers", multipleValues: true);
            TagHelperManifest = Option("-o", "output file", CommandOptionType.SingleValue);
            ProjectDirectory = Option("-p", "project root directory", CommandOptionType.SingleValue);
            Version = Option("-v|--version", "Razor language version", CommandOptionType.SingleValue);
            Configuration = Option("-c", "Razor configuration name", CommandOptionType.SingleValue);
            ExtensionNames = Option("-n", "extension name", CommandOptionType.MultipleValue);
            ExtensionFilePaths = Option("-e", "extension file path", CommandOptionType.MultipleValue);
            UseSourceGenerator = Option("--use-source-generator", "host the Razor source generator instead of the engine", CommandOptionType.NoValue);
        }

        public CommandArgument Assemblies { get; }

        public CommandOption TagHelperManifest { get; }

        public CommandOption ProjectDirectory { get; }

        public CommandOption Version { get; }

        public CommandOption Configuration { get; }

        public CommandOption ExtensionNames { get; }

        public CommandOption ExtensionFilePaths { get; }

        public CommandOption UseSourceGenerator { get; }

        protected override bool ValidateArguments()
        {
            if (string.IsNullOrEmpty(TagHelperManifest.Value()))
            {
                Error.WriteLine($"{TagHelperManifest.Description} must be specified.");
                return false;
            }

            if (Assemblies.Values.Count == 0)
            {
                Error.WriteLine($"{Assemblies.Name} must have at least one value.");
                return false;
            }

            if (string.IsNullOrEmpty(ProjectDirectory.Value()))
            {
                ProjectDirectory.Values.Add(Environment.CurrentDirectory);
            }

            if (string.IsNullOrEmpty(Version.Value()))
            {
                Error.WriteLine($"{Version.Description} must be specified.");
                return false;
            }
            else if (!RazorLanguageVersion.TryParse(Version.Value(), out _))
            {
                Error.WriteLine($"Invalid option {Version.Value()} for Razor language version --version; must be Latest or a valid version in range {RazorLanguageVersion.Version_1_0} to {RazorLanguageVersion.Latest}.");
                return false;
            }

            if (string.IsNullOrEmpty(Configuration.Value()))
            {
                Error.WriteLine($"{Configuration.Description} must be specified.");
                return false;
            }

            if (ExtensionNames.Values.Count != ExtensionFilePaths.Values.Count)
            {
                Error.WriteLine($"{ExtensionNames.Description} and {ExtensionFilePaths.Description} should have the same number of values.");
            }

            foreach (var filePath in ExtensionFilePaths.Values)
            {
                if (!Path.IsPathRooted(filePath))
                {
                    Error.WriteLine($"Extension file paths must be fully-qualified, absolute paths.");
                    return false;
                }
            }

            PatchExtensions(ExtensionNames, ExtensionFilePaths, Error);

            return true;
        }

        private const string RazorCompilerFileName = "Microsoft.CodeAnalysis.Razor.Compiler.dll";

        /// <summary>
        /// Replaces the assembly for MVC extension with the one shipped alongside SDK (as opposed to the one from NuGet).
        /// </summary>
        /// <remarks>
        /// Needed so the Razor compiler can change its APIs without breaking legacy MVC scenarios.
        /// </remarks>
        internal static void PatchExtensions(CommandOption extensionNames, CommandOption extensionFilePaths, TextWriter error)
        {
            string currentDirectory = null;

            for (int i = 0; i < extensionNames.Values.Count; i++)
            {
                var extensionName = extensionNames.Values[i];

                string expectedOriginalPath = extensionName switch
                {
                    "MVC-1.0" or "MVC-1.1" or "MVC-2.0" or "MVC-2.1" => "Microsoft.AspNetCore.Mvc.Razor.Extensions",
                    "MVC-3.0" => "Microsoft.CodeAnalysis.Razor.Compiler",
                    _ => null,
                };

                if (expectedOriginalPath is not null)
                {
                    var extensionFilePath = extensionFilePaths.Values[i];
                    if (!string.Equals(expectedOriginalPath, Path.GetFileNameWithoutExtension(extensionFilePath), StringComparison.OrdinalIgnoreCase))
                    {
                        error.WriteLine($"Extension '{extensionName}' has unexpected path '{extensionFilePath}'.");
                    }
                    else
                    {
                        currentDirectory ??= Path.GetDirectoryName(typeof(Application).Assembly.Location);
                        extensionFilePaths.Values[i] = Path.Combine(currentDirectory, RazorCompilerFileName);
                    }
                }
            }
        }

        protected override Task<int> ExecuteCoreAsync()
        {
            if (!Parent.Checker.Check(ExtensionFilePaths.Values))
            {
                Error.WriteLine($"Extensions could not be loaded. See output for details.");
                return Task.FromResult(ExitCodeFailure);
            }

            if (UseSourceGenerator.HasValue())
            {
                return Task.FromResult(ExecuteWithSourceGenerator(
                    projectDirectory: ProjectDirectory.Value(),
                    outputFilePath: TagHelperManifest.Value(),
                    assemblies: Assemblies.Values.ToArray()));
            }

            var version = RazorLanguageVersion.Parse(Version.Value());
            var configuration = new RazorConfiguration(version, Configuration.Value(), Extensions: [], UseConsolidatedMvcViews: false);

            var result = ExecuteCore(
                configuration: configuration,
                projectDirectory: ProjectDirectory.Value(),
                outputFilePath: TagHelperManifest.Value(),
                assemblies: Assemblies.Values.ToArray());

            return Task.FromResult(result);
        }

        private int ExecuteCore(RazorConfiguration configuration, string projectDirectory, string outputFilePath, string[] assemblies)
        {
            outputFilePath = Path.Combine(projectDirectory, outputFilePath);

            var metadataReferences = new MetadataReference[assemblies.Length];
            for (var i = 0; i < assemblies.Length; i++)
            {
                metadataReferences[i] = Parent.AssemblyReferenceProvider(assemblies[i], default(MetadataReferenceProperties));
            }

            var engine = RazorProjectEngine.Create(configuration, RazorProjectFileSystem.Empty, b =>
            {
                b.RegisterExtensions();

                b.Features.Add(new DefaultMetadataReferenceFeature() { References = metadataReferences });
                b.Features.Add(new CompilationTagHelperFeature());

                b.RegisterDefaultTagHelperProducer();

                CompilerFeatures.Register(b);
            });

            var feature = engine.Engine.Features.OfType<ITagHelperFeature>().Single();
            var tagHelpers = feature.GetTagHelpers();

            WriteTagHelperManifest(outputFilePath, tagHelpers);

            return ExitCodeSuccess;
        }

        private int ExecuteWithSourceGenerator(string projectDirectory, string outputFilePath, string[] assemblies)
        {
            outputFilePath = Path.Combine(projectDirectory, outputFilePath);

            var parseOptions = RazorSourceGeneratorHost.CreateParseOptions(LanguageVersion.Default);
            var compilation = RazorSourceGeneratorHost.CreateCompilation(assemblies, Parent.AssemblyReferenceProvider);

            // The generator only discovers tag helpers from references when the project has at least one
            // Razor file. A synthetic empty view satisfies that without contributing a tag helper itself:
            // only components (.razor files) feed the compilation-based half of discovery.
            var syntheticPath = Path.Combine(projectDirectory, "__rzc_discover__.cshtml");
            var inputFiles = new List<RazorInputFile>
            {
                new RazorInputFile(syntheticPath, "__rzc_discover__.cshtml", text: SourceText.From(string.Empty, Encoding.UTF8)),
            };

            var optionsProvider = RazorSourceGeneratorHost.CreateOptionsProvider(
                razorConfiguration: Configuration.Value(),
                razorLanguageVersion: Version.Value(),
                rootNamespace: "ASP",
                supportLocalizedComponentNames: false,
                generateMetadataSourceChecksumAttributes: false,
                projectDirectory: projectDirectory,
                files: inputFiles);
            var additionalTexts = RazorSourceGeneratorHost.CreateAdditionalTexts(inputFiles);

            var runResult = RazorSourceGeneratorHost.CreateDriver(parseOptions)
                .AddAdditionalTexts(additionalTexts)
                .WithUpdatedAnalyzerConfigOptions(optionsProvider)
                .RunGeneratorsAndUpdateCompilation(compilation, out _, out _)
                .GetRunResult().Results.Single();

            if (!RazorSourceGeneratorHostOutput.TryGet(runResult, out var razorResult))
            {
                Error.WriteLine("The Razor source generator did not produce the expected host output.");
                return ExitCodeFailure;
            }

            WriteTagHelperManifest(outputFilePath, razorResult.TagHelpers);

            return ExitCodeSuccess;
        }

        private void WriteTagHelperManifest(string outputFilePath, IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            using (var stream = new MemoryStream())
            {
                Serialize(stream, tagHelpers);

                stream.Position = 0;

                var newHash = Hash(stream);
                var existingHash = Hash(outputFilePath);

                if (!HashesEqual(newHash, existingHash))
                {
                    stream.Position = 0;
                    using (var output = File.Open(outputFilePath, FileMode.Create))
                    {
                        stream.CopyTo(output);
                    }
                }
            }
        }

        private static byte[] Hash(string path)
        {
            if (!File.Exists(path))
            {
                return Array.Empty<byte>();
            }

            using (var stream = File.OpenRead(path))
            {
                return Hash(stream);
            }
        }

        private static byte[] Hash(Stream stream)
        {
            using (var sha = SHA256.Create())
            {
                sha.ComputeHash(stream);
                return sha.Hash;
            }
        }

        private bool HashesEqual(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void Serialize(Stream stream, IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            JsonSerializer.Serialize(stream, tagHelpers, TagHelperDescriptorJsonConverter.SerializerOptions);
        }
    }
}

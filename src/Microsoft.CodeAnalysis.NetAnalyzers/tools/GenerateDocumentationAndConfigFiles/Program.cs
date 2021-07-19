// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Analyzer.Utilities.PooledObjects.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ReleaseTracking;
using Microsoft.CodeAnalysis.Text;
using static GenerateDocumentationAndConfigFiles.CommonPropertyNames;

namespace GenerateDocumentationAndConfigFiles
{
    public static class Program
    {
        private static readonly HttpClient httpClient = new();

        public static async Task<int> Main(string[] args)
        {
            const int expectedArguments = 22;
            const string validateOnlyPrefix = "-validateOnly:";

            if (args.Length != expectedArguments)
            {
                Console.Error.WriteLine($"Excepted {expectedArguments} arguments, found {args.Length}: {string.Join(';', args)}");
                return 1;
            }

            if (!args[0].StartsWith("-validateOnly:", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Excepted the first argument to start with `{validateOnlyPrefix}`. found `{args[0]}`.");
                return 1;
            }

            if (!bool.TryParse(args[0][validateOnlyPrefix.Length..], out var validateOnly))
            {
                validateOnly = false;
            }

            var fileNamesWithValidationFailures = new List<string>();

            string analyzerRulesetsDir = args[1];
            string analyzerEditorconfigsDir = args[2];
            string analyzerGlobalconfigsDir = args[3];
            string binDirectory = args[4];
            string configuration = args[5];
            string tfm = args[6];
            var assemblyList = args[7].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string propsFileDir = args[8];
            string propsFileName = args[9];
            string targetsFileDir = args[10];
            string targetsFileName = args[11];
            string propsFileToDisableNetAnalyzersInNuGetPackageName = args[12];
            string analyzerDocumentationFileDir = args[13];
            string analyzerDocumentationFileName = args[14];
            string analyzerSarifFileDir = args[15];
            string analyzerSarifFileName = args[16];
            var analyzerVersion = args[17];
            var analyzerPackageName = args[18];
            if (!bool.TryParse(args[19], out var containsPortedFxCopRules))
            {
                containsPortedFxCopRules = false;
            }

            if (!bool.TryParse(args[20], out var generateAnalyzerRulesMissingDocumentationFile))
            {
                generateAnalyzerRulesMissingDocumentationFile = false;
            }

            var releaseTrackingOptOutString = args[21];
            if (!bool.TryParse(releaseTrackingOptOutString, out bool releaseTrackingOptOut))
            {
                releaseTrackingOptOut = false;
            }

            var allRulesById = new SortedList<string, DiagnosticDescriptor>();
            var fixableDiagnosticIds = new HashSet<string>();
            var categories = new HashSet<string>();
            var rulesMetadata = new SortedList<string, (string path, SortedList<string, (DiagnosticDescriptor rule, string typeName, string[]? languages)> rules)>();
            foreach (string assembly in assemblyList)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assembly);
                string path = Path.Combine(binDirectory, assemblyName, configuration, tfm, assembly);
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"'{path}' does not exist");
                    return 1;
                }

                var analyzerFileReference = new AnalyzerFileReference(path, AnalyzerAssemblyLoader.Instance);
                analyzerFileReference.AnalyzerLoadFailed += AnalyzerFileReference_AnalyzerLoadFailed;
                var analyzers = analyzerFileReference.GetAnalyzersForAllLanguages();

                var assemblyRulesMetadata = (path, rules: new SortedList<string, (DiagnosticDescriptor rule, string typeName, string[]? languages)>());

                foreach (var analyzer in analyzers)
                {
                    var analyzerType = analyzer.GetType();

                    foreach (var rule in analyzer.SupportedDiagnostics)
                    {
                        allRulesById[rule.Id] = rule;
                        categories.Add(rule.Category);
                        assemblyRulesMetadata.rules[rule.Id] = (rule, analyzerType.Name, analyzerType.GetCustomAttribute<DiagnosticAnalyzerAttribute>(true)?.Languages);
                    }
                }

                rulesMetadata.Add(assemblyName, assemblyRulesMetadata);

                foreach (var id in analyzerFileReference.GetFixers().SelectMany(fixer => fixer.FixableDiagnosticIds))
                {
                    fixableDiagnosticIds.Add(id);
                }
            }

            createRulesetAndEditorconfig(
                "AllRulesDefault",
                "All Rules with default severity",
                @"All Rules with default severity. Rules with IsEnabledByDefault = false are disabled.",
                RulesetKind.AllDefault);

            createRulesetAndEditorconfig(
                "AllRulesEnabled",
                "All Rules Enabled as build warnings",
                "All Rules are enabled as build warnings. Rules with IsEnabledByDefault = false are force enabled as build warnings.",
                RulesetKind.AllEnabled);

            createRulesetAndEditorconfig(
                "AllRulesDisabled",
                "All Rules Disabled",
                @"All Rules are disabled.",
                RulesetKind.AllDisabled);

            foreach (var category in categories)
            {
                createRulesetAndEditorconfig(
                    $"{category}RulesDefault",
                    $"{category} Rules with default severity",
                    $@"All {category} Rules with default severity. Rules with IsEnabledByDefault = false or from a different category are disabled.",
                    RulesetKind.CategoryDefault,
                    category: category);

                createRulesetAndEditorconfig(
                    $"{category}RulesEnabled",
                    $"{category} Rules Enabled as build warnings",
                    $@"All {category} Rules are enabled as build warnings. {category} Rules with IsEnabledByDefault = false are force enabled as build warnings. Rules from a different category are disabled.",
                    RulesetKind.CategoryEnabled,
                    category: category);
            }

            // We generate custom tag based rulesets only for select custom tags.
            var customTagsToGenerateRulesets = ImmutableArray.Create(
                WellKnownDiagnosticTagsExtensions.Dataflow,
                FxCopWellKnownDiagnosticTags.PortedFromFxCop);

            foreach (var customTag in customTagsToGenerateRulesets)
            {
                createRulesetAndEditorconfig(
                    $"{customTag}RulesDefault",
                    $"{customTag} Rules with default severity",
                    $@"All {customTag} Rules with default severity. Rules with IsEnabledByDefault = false and non-{customTag} rules are disabled.",
                    RulesetKind.CustomTagDefault,
                    customTag: customTag);

                createRulesetAndEditorconfig(
                    $"{customTag}RulesEnabled",
                    $"{customTag} Rules Enabled as build warnings",
                    $@"All {customTag} Rules are enabled as build warnings. {customTag} Rules with IsEnabledByDefault = false are force enabled as build warning. Non-{customTag} Rules are disabled.",
                    RulesetKind.CustomTagEnabled,
                    customTag: customTag);
            }

            createPropsFiles();

            createAnalyzerDocumentationFile();

            createAnalyzerSarifFile();

            if (generateAnalyzerRulesMissingDocumentationFile)
            {
                await createAnalyzerRulesMissingDocumentationFileAsync().ConfigureAwait(false);
            }

            if (fileNamesWithValidationFailures.Count > 0)
            {
                Console.Error.WriteLine("One or more auto-generated documentation files were either edited manually, or not updated. Please revert changes made to the following files (if manually edited) and run `msbuild /t:pack` at the root of the repo to automatically update them:");
                fileNamesWithValidationFailures.ForEach(fileName => Console.Error.WriteLine($"    {fileName}"));
                return 1;
            }

            if (!createGlobalConfigFiles())
            {
                return 2;
            }

            CreateTargetsFile(targetsFileDir, targetsFileName, analyzerPackageName, categories.OrderBy(c => c));

            return 0;

            // Local functions.
            static void AnalyzerFileReference_AnalyzerLoadFailed(object? sender, AnalyzerLoadFailureEventArgs e)
                => throw e.Exception ?? new NotSupportedException(e.Message);

            void createRulesetAndEditorconfig(
                string fileName,
                string title,
                string description,
                RulesetKind rulesetKind,
                string? category = null,
                string? customTag = null)
            {
                CreateRuleset(analyzerRulesetsDir, fileName + ".ruleset", title, description, rulesetKind, category, customTag, allRulesById, analyzerPackageName);
                CreateEditorconfig(analyzerEditorconfigsDir, fileName, title, description, rulesetKind, category, customTag, allRulesById);
                return;
            }

            void createPropsFiles()
            {
                if (string.IsNullOrEmpty(propsFileDir) || string.IsNullOrEmpty(propsFileName))
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    Debug.Assert(string.IsNullOrEmpty(propsFileToDisableNetAnalyzersInNuGetPackageName));
                    return;
                }

                var disableNetAnalyzersImport = getDisableNetAnalyzersImport();

                var fileContents =
$@"<Project>
  {disableNetAnalyzersImport}{getCodeAnalysisTreatWarningsNotAsErrors()}{getCompilerVisibleProperties()}
</Project>";
                var directory = Directory.CreateDirectory(propsFileDir);
                var fileWithPath = Path.Combine(directory.FullName, propsFileName);

                // This doesn't need validation as the generated file is part of artifacts.
                File.WriteAllText(fileWithPath, fileContents);

                if (!string.IsNullOrEmpty(disableNetAnalyzersImport))
                {
                    Debug.Assert(Version.TryParse(analyzerVersion, out _));

                    fileWithPath = Path.Combine(directory.FullName, propsFileToDisableNetAnalyzersInNuGetPackageName);
                    fileContents =
$@"<Project>
  <!-- 
    PropertyGroup to disable built-in analyzers from .NET SDK that have the identical CA rules to those implemented in this package.
    This props file should only be present in the analyzer NuGet package, it should **not** be inserted into the .NET SDK.
  -->
  <PropertyGroup>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <{NetAnalyzersNugetAssemblyVersionPropertyName}>{analyzerVersion}</{NetAnalyzersNugetAssemblyVersionPropertyName}>
  </PropertyGroup>
</Project>";
                    // This doesn't need validation as the generated file is part of artifacts.
                    File.WriteAllText(fileWithPath, fileContents);
                }

                return;

                string getDisableNetAnalyzersImport()
                {
                    if (!string.IsNullOrEmpty(propsFileToDisableNetAnalyzersInNuGetPackageName))
                    {
                        Debug.Assert(analyzerPackageName is NetAnalyzersPackageName or TextAnalyzersPackageName);

                        return $@"
  <!-- 
    This import includes an additional props file that disables built-in analyzers from .NET SDK that have the identical CA rules to those implemented in this package.
    This additional props file should only be present in the analyzer NuGet package, it should **not** be inserted into the .NET SDK.
  -->
  <Import Project=""{propsFileToDisableNetAnalyzersInNuGetPackageName}"" Condition=""Exists('{propsFileToDisableNetAnalyzersInNuGetPackageName}')"" />

  <!--
    PropertyGroup to set the NetAnalyzers version installed in the SDK.
    We rely on the additional props file '{propsFileToDisableNetAnalyzersInNuGetPackageName}' not being present in the SDK.
  -->
  <PropertyGroup Condition=""!Exists('{propsFileToDisableNetAnalyzersInNuGetPackageName}')"">
    <{NetAnalyzersSDKAssemblyVersionPropertyName}>{analyzerVersion}</{NetAnalyzersSDKAssemblyVersionPropertyName}>
  </PropertyGroup>
";
                    }

                    Debug.Assert(!containsPortedFxCopRules);
                    return string.Empty;
                }
            }

            string getCodeAnalysisTreatWarningsNotAsErrors()
            {
                var allRuleIds = string.Join(';', allRulesById.Keys);
                return $@"
  <!-- 
    This property group prevents the rule ids implemented in this package to be bumped to errors when
    the 'CodeAnalysisTreatWarningsAsErrors' = 'false'.
  -->
  <PropertyGroup>
    <CodeAnalysisRuleIds>{allRuleIds}</CodeAnalysisRuleIds>
    <WarningsNotAsErrors Condition=""'$(CodeAnalysisTreatWarningsAsErrors)' == 'false'"">$(WarningsNotAsErrors);$(CodeAnalysisRuleIds)</WarningsNotAsErrors>
  </PropertyGroup>";
            }

            string getCompilerVisibleProperties()
            {
                return analyzerPackageName switch
                {
                    ResxSourceGeneratorPackageName => @"
  <ItemGroup>
    <CompilerVisibleProperty Include=""RootNamespace"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""GenerateSource"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""RelativeDir"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""OmitGetResourceString"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""AsConstants"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""IncludeDefaultValues"" />
    <CompilerVisibleItemMetadata Include=""AdditionalFiles"" MetadataName=""EmitFormatMethods"" />
  </ItemGroup>
",
                    _ => "",
                };
            }

            void createAnalyzerDocumentationFile()
            {
                if (string.IsNullOrEmpty(analyzerDocumentationFileDir) || string.IsNullOrEmpty(analyzerDocumentationFileName) || allRulesById.Count == 0)
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    return;
                }

                var directory = Directory.CreateDirectory(analyzerDocumentationFileDir);
                var fileWithPath = Path.Combine(directory.FullName, analyzerDocumentationFileName);

                var builder = new StringBuilder();

                var fileTitle = Path.GetFileNameWithoutExtension(analyzerDocumentationFileName);
                builder.AppendLine($"# {fileTitle}");
                builder.AppendLine();

                var isFirstEntry = true;
                foreach (var ruleById in allRulesById)
                {
                    string ruleId = ruleById.Key;
                    DiagnosticDescriptor descriptor = ruleById.Value;

                    var ruleIdWithHyperLink = descriptor.Id;
                    if (!string.IsNullOrWhiteSpace(descriptor.HelpLinkUri))
                    {
                        ruleIdWithHyperLink = $"[{ruleIdWithHyperLink}]({descriptor.HelpLinkUri})";
                    }

                    var title = descriptor.Title.ToString(CultureInfo.InvariantCulture).Trim();
                    // Escape generic arguments to ensure they are not considered as HTML elements
                    title = Regex.Replace(title, "(<.+?>)", "\\$1");

                    if (!isFirstEntry)
                    {
                        // Add separation line only when reaching next entry to avoid useless empty line at the end
                        builder.AppendLine();
                    }

                    isFirstEntry = false;
                    builder.AppendLine($"## {ruleIdWithHyperLink}: {title}");
                    builder.AppendLine();

                    var description = descriptor.Description.ToString(CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        description = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
                    }

                    // Double the line breaks to ensure they are rendered properly in markdown
                    description = Regex.Replace(description, "(\r?\n)", "$1$1");
                    // Escape generic arguments to ensure they are not considered as HTML elements
                    description = Regex.Replace(description, "(<.+?>)", "\\$1");
                    // Add angle brackets around links to prevent violating MD034:
                    // https://github.com/DavidAnson/markdownlint/blob/82cf68023f7dbd2948a65c53fc30482432195de4/doc/Rules.md#md034---bare-url-used
                    // Regex taken from: https://github.com/DavidAnson/markdownlint/blob/59eaa869fc749e381fe9d53d04812dfc759595c6/helpers/helpers.js#L24
                    description = Regex.Replace(description, @"(?:http|ftp)s?:\/\/[^\s\]""']*(?:\/|[^\s\]""'\W])", "<$0>");
                    description = description.Trim();

                    builder.AppendLine(description);
                    builder.AppendLine();

                    builder.AppendLine("|Item|Value|");
                    builder.AppendLine("|-|-|");
                    builder.AppendLine($"|Category|{descriptor.Category}|");
                    builder.AppendLine($"|Enabled|{descriptor.IsEnabledByDefault}|");
                    builder.AppendLine($"|Severity|{descriptor.DefaultSeverity}|");
                    var hasCodeFix = fixableDiagnosticIds.Contains(descriptor.Id);
                    builder.AppendLine($"|CodeFix|{hasCodeFix}|");
                    builder.AppendLine("---");
                }

                if (validateOnly)
                {
                    Validate(fileWithPath, builder.ToString(), fileNamesWithValidationFailures);
                }
                else
                {
                    File.WriteAllText(fileWithPath, builder.ToString());
                }
            }

            // based on https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/CommandLine/ErrorLogger.cs
            void createAnalyzerSarifFile()
            {
                if (string.IsNullOrEmpty(analyzerSarifFileDir) || string.IsNullOrEmpty(analyzerSarifFileName) || allRulesById.Count == 0)
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    return;
                }

                var culture = new CultureInfo("en-us");
                string tempAnalyzerSarifFileName = analyzerSarifFileName;
                if (validateOnly)
                {
                    // In validate only mode, we write the sarif file in a temp file and compare it with
                    // the existing content in `analyzerSarifFileName`.
                    tempAnalyzerSarifFileName = $"temp-{analyzerSarifFileName}";
                }

                var directory = Directory.CreateDirectory(analyzerSarifFileDir);
                var fileWithPath = Path.Combine(directory.FullName, tempAnalyzerSarifFileName);
                using var textWriter = new StreamWriter(fileWithPath, false, Encoding.UTF8);
                using var writer = new Roslyn.Utilities.JsonWriter(textWriter);
                writer.WriteObjectStart(); // root
                writer.Write("$schema", "http://json.schemastore.org/sarif-1.0.0");
                writer.Write("version", "1.0.0");
                writer.WriteArrayStart("runs");

                foreach (var assemblymetadata in rulesMetadata)
                {
                    writer.WriteObjectStart(); // run

                    writer.WriteObjectStart("tool");
                    writer.Write("name", assemblymetadata.Key);

                    if (!string.IsNullOrWhiteSpace(analyzerVersion))
                    {
                        writer.Write("version", analyzerVersion);
                    }

                    writer.Write("language", culture.Name);
                    writer.WriteObjectEnd(); // tool

                    writer.WriteObjectStart("rules"); // rules

                    foreach (var rule in assemblymetadata.Value.rules)
                    {
                        var ruleId = rule.Key;
                        var descriptor = rule.Value.rule;

                        writer.WriteObjectStart(descriptor.Id); // rule
                        writer.Write("id", descriptor.Id);

                        writer.Write("shortDescription", descriptor.Title.ToString(CultureInfo.InvariantCulture));

                        string fullDescription = descriptor.Description.ToString(CultureInfo.InvariantCulture);
                        writer.Write("fullDescription", !string.IsNullOrEmpty(fullDescription) ? fullDescription.Replace("\r\n", "\n") : descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture));

                        writer.Write("defaultLevel", getLevel(descriptor.DefaultSeverity));

                        if (!string.IsNullOrEmpty(descriptor.HelpLinkUri))
                        {
                            writer.Write("helpUri", descriptor.HelpLinkUri);
                        }

                        writer.WriteObjectStart("properties");

                        writer.Write("category", descriptor.Category);

                        writer.Write("isEnabledByDefault", descriptor.IsEnabledByDefault);

                        writer.Write("typeName", rule.Value.typeName);

                        if (rule.Value.languages?.Length > 0)
                        {
                            writer.WriteArrayStart("languages");

                            foreach (var language in rule.Value.languages.OrderBy(l => l, StringComparer.InvariantCultureIgnoreCase))
                            {
                                writer.Write(language);
                            }

                            writer.WriteArrayEnd(); // languages
                        }

                        if (descriptor.CustomTags.Any())
                        {
                            writer.WriteArrayStart("tags");

                            foreach (string tag in descriptor.CustomTags)
                            {
                                writer.Write(tag);
                            }

                            writer.WriteArrayEnd(); // tags
                        }

                        writer.WriteObjectEnd(); // properties
                        writer.WriteObjectEnd(); // rule
                    }

                    writer.WriteObjectEnd(); // rules
                    writer.WriteObjectEnd(); // run
                }

                writer.WriteArrayEnd(); // runs
                writer.WriteObjectEnd(); // root

                if (validateOnly)
                {
                    // Close is needed to be able to read the file. Dispose() should do the same job.
                    // Note: Although a using statement exists for the textWriter, its scope is the whole method.
                    // So Dispose isn't called before the whole method returns.
                    textWriter.Close();
                    Validate(Path.Combine(directory.FullName, analyzerSarifFileName), File.ReadAllText(fileWithPath), fileNamesWithValidationFailures);
                }

                return;
                static string getLevel(DiagnosticSeverity severity)
                {
                    switch (severity)
                    {
                        case DiagnosticSeverity.Info:
                            return "note";

                        case DiagnosticSeverity.Error:
                            return "error";

                        case DiagnosticSeverity.Warning:
                            return "warning";

                        case DiagnosticSeverity.Hidden:
                            return "hidden";

                        default:
                            Debug.Assert(false);
                            goto case DiagnosticSeverity.Warning;
                    }
                }
            }

            async ValueTask createAnalyzerRulesMissingDocumentationFileAsync()
            {
                if (string.IsNullOrEmpty(analyzerDocumentationFileDir) || allRulesById.Count == 0)
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    return;
                }

                var directory = Directory.CreateDirectory(analyzerDocumentationFileDir);
                var fileWithPath = Path.Combine(directory.FullName, "RulesMissingDocumentation.md");

                var builder = new StringBuilder();
                builder.Append(@"# Rules without documentation

Rule ID | Missing Help Link | Title |
--------|-------------------|-------|
");
                var actualContent = Array.Empty<string>();
                if (validateOnly)
                {
                    actualContent = File.ReadAllLines(fileWithPath);
                }

                foreach (var ruleById in allRulesById)
                {
                    string ruleId = ruleById.Key;
                    DiagnosticDescriptor descriptor = ruleById.Value;

                    var helpLinkUri = descriptor.HelpLinkUri;
                    if (!string.IsNullOrWhiteSpace(helpLinkUri) &&
                        await checkHelpLinkAsync(helpLinkUri).ConfigureAwait(false))
                    {
                        // Rule with valid documentation link
                        continue;
                    }

                    // The angle brackets around helpLinkUri are added to follow MD034 rule:
                    // https://github.com/DavidAnson/markdownlint/blob/82cf68023f7dbd2948a65c53fc30482432195de4/doc/Rules.md#md034---bare-url-used
                    if (!string.IsNullOrWhiteSpace(helpLinkUri))
                    {
                        helpLinkUri = $"<{helpLinkUri}>";
                    }

                    var escapedTitle = descriptor.Title.ToString(CultureInfo.InvariantCulture).Replace("<", "\\<");
                    var line = $"{ruleId} | {helpLinkUri} | {escapedTitle} |";
                    if (validateOnly)
                    {
                        // The validation for RulesMissingDocumentation.md is different than others.
                        // We consider having "extra" entries as valid. This is to prevent CI failures due to rules being documented.
                        // However, we consider "missing" entries as invalid. This is to force updating the file when new rules are added.
                        if (!actualContent.Contains(line))
                        {
                            Console.Error.WriteLine($"Missing entry in {fileWithPath}");
                            Console.Error.WriteLine(line);
                            // The file is missing an entry. Mark it as invalid and break the loop as there is no need to continue validating.
                            fileNamesWithValidationFailures.Add(fileWithPath);
                            break;
                        }
                    }
                    else
                    {
                        builder.AppendLine(line);
                    }
                }

                if (!validateOnly)
                {
                    File.WriteAllText(fileWithPath, builder.ToString());
                }
                return;

                static async Task<bool> checkHelpLinkAsync(string helpLink)
                {
                    try
                    {
                        if (!Uri.TryCreate(helpLink, UriKind.Absolute, out var uri))
                        {
                            return false;
                        }

                        var request = new HttpRequestMessage(HttpMethod.Head, uri);
                        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                        return response?.StatusCode == HttpStatusCode.OK;
                    }
                    catch (WebException)
                    {
                        return false;
                    }
                }
            }

            bool createGlobalConfigFiles()
            {
                using var shippedFilesDataBuilder = ArrayBuilder<ReleaseTrackingData>.GetInstance();
                using var versionsBuilder = PooledHashSet<Version>.GetInstance();

                // Validate all assemblies exist on disk and can be loaded.
                foreach (string assembly in assemblyList)
                {
                    var assemblyPath = GetAssemblyPath(assembly);
                    if (!File.Exists(assemblyPath))
                    {
                        Console.Error.WriteLine($"'{assemblyPath}' does not exist");
                        return false;
                    }

                    try
                    {
                        _ = Assembly.LoadFrom(assemblyPath);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        Console.Error.WriteLine(ex.Message);
                        return false;
                    }
                }

                // Compute descriptors by rule ID and shipped analyzer release versions and shipped data.
                var sawShippedFile = false;
                foreach (string assembly in assemblyList)
                {
                    var assemblyPath = GetAssemblyPath(assembly);
                    var assemblyDir = Path.GetDirectoryName(assemblyPath);
                    if (assemblyDir is null)
                    {
                        continue;
                    }

                    var assemblyName = Path.GetFileNameWithoutExtension(assembly);
                    var shippedFile = Path.Combine(assemblyDir, "AnalyzerReleases", assemblyName, ReleaseTrackingHelper.ShippedFileName);
                    if (File.Exists(shippedFile))
                    {
                        sawShippedFile = true;

                        if (releaseTrackingOptOut)
                        {
                            Console.Error.WriteLine($"'{shippedFile}' exists but was not expected");
                            return false;
                        }

                        try
                        {
                            using var fileStream = File.OpenRead(shippedFile);
                            var sourceText = SourceText.From(fileStream);
                            var releaseTrackingData = ReleaseTrackingHelper.ReadReleaseTrackingData(shippedFile, sourceText,
                                onDuplicateEntryInRelease: (_1, _2, _3, _4, line) => throw new Exception($"Duplicate entry in {shippedFile} at {line.LineNumber}: '{line}'"),
                                onInvalidEntry: (line, _2, _3, _4) => throw new Exception($"Invalid entry in {shippedFile} at {line.LineNumber}: '{line}'"),
                                isShippedFile: true);
                            shippedFilesDataBuilder.Add(releaseTrackingData);
                            versionsBuilder.AddRange(releaseTrackingData.Versions);
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            Console.Error.WriteLine(ex.Message);
                            return false;
                        }
                    }
                }

                if (!releaseTrackingOptOut && !sawShippedFile)
                {
                    Console.Error.WriteLine($"Could not find any 'AnalyzerReleases.Shipped.md' file");
                    return false;
                }

                if (versionsBuilder.Count > 0)
                {
                    var shippedFilesData = shippedFilesDataBuilder.ToImmutable();

                    // Generate global analyzer config files for each shipped version, if required.
                    foreach (var version in versionsBuilder)
                    {
                        var analysisLevelVersionString = GetNormalizedVersionStringForEditorconfigFileNameSuffix(version);

                        foreach (var analysisMode in Enum.GetValues(typeof(AnalysisMode)))
                        {
                            CreateGlobalConfig(version, analysisLevelVersionString, (AnalysisMode)analysisMode!, shippedFilesData, category: null);
                            foreach (var category in categories)
                            {
                                CreateGlobalConfig(version, analysisLevelVersionString, (AnalysisMode)analysisMode!, shippedFilesData, category);
                            }
                        }
                    }
                }

                return true;

                // Local functions.

                void CreateGlobalConfig(
                    Version version,
                    string analysisLevelVersionString,
                    AnalysisMode analysisMode,
                    ImmutableArray<ReleaseTrackingData> shippedFilesData,
                    string? category)
                {
                    var analysisLevelPropName = "AnalysisLevel";
                    var title = $"Rules from '{version}' release with '{analysisMode}' analysis mode";
                    var description = $"Rules with enabled-by-default state from '{version}' release with '{analysisMode}' analysis mode. Rules that are first released in a version later than '{version}' are disabled.";
                    if (category != null)
                    {
                        analysisLevelPropName += category;
                        title = $"'{category}' {title}";
                        description = $"'{category}' {description}";
                    }

                    CreateGlobalconfig(
                        analyzerGlobalconfigsDir,
#pragma warning disable CA1308 // Normalize strings to uppercase
                                $"{analysisLevelPropName}_{analysisLevelVersionString}_{analysisMode!.ToString()!.ToLowerInvariant()}.editorconfig",
#pragma warning restore CA1308 // Normalize strings to uppercase
                                    title,
                        description,
                        analysisMode,
                        category,
                        allRulesById,
                        (shippedFilesData, version));
                }

                static string GetNormalizedVersionStringForEditorconfigFileNameSuffix(Version version)
                {
                    var fieldCount = GetVersionFieldCount(version);
                    return version.ToString(fieldCount).Replace(".", "_", StringComparison.Ordinal);

                    static int GetVersionFieldCount(Version version)
                    {
                        if (version.Revision > 0)
                        {
                            return 4;
                        }

                        if (version.Build > 0)
                        {
                            return 3;
                        }

                        if (version.Minor > 0)
                        {
                            return 2;
                        }

                        return 1;
                    }
                }

                string GetAssemblyPath(string assembly)
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assembly);
                    var assemblyDir = Path.Combine(binDirectory, assemblyName, configuration, tfm);
                    return Path.Combine(assemblyDir, assembly);
                }
            }
        }

        private static void CreateRuleset(
            string analyzerRulesetsDir,
            string rulesetFileName,
            string rulesetTitle,
            string rulesetDescription,
            RulesetKind rulesetKind,
            string? category,
            string? customTag,
            SortedList<string, DiagnosticDescriptor> sortedRulesById,
            string analyzerPackageName)
        {
            var text = GetRulesetOrEditorconfigText(
                rulesetKind,
                startRuleset,
                endRuleset,
                startRulesSection,
                endRulesSection,
                addRuleEntry,
                getSeverityString,
                commentStart: "   <!-- ",
                commentEnd: " -->",
                category,
                customTag,
                sortedRulesById);

            var directory = Directory.CreateDirectory(analyzerRulesetsDir);
            var rulesetFilePath = Path.Combine(directory.FullName, rulesetFileName);

            // This doesn't need validation as the generated file is part of artifacts.
            File.WriteAllText(rulesetFilePath, text);
            return;

            // Local functions
            void startRuleset(StringBuilder result)
            {
                result.AppendLine(@"<?xml version=""1.0""?>");
                result.AppendLine($@"<RuleSet Name=""{rulesetTitle}"" Description=""{rulesetDescription}"" ToolsVersion=""15.0"">");
            }

            static void endRuleset(StringBuilder result)
            {
                result.AppendLine("</RuleSet>");
            }

            void startRulesSection(StringBuilder result)
            {
                result.AppendLine($@"   <Rules AnalyzerId=""{analyzerPackageName}"" RuleNamespace=""{analyzerPackageName}"">");
            }

            static void endRulesSection(StringBuilder result)
            {
                result.AppendLine("   </Rules>");
            }

            static void addRuleEntry(StringBuilder result, DiagnosticDescriptor rule, string severity)
            {
                var spacing = new string(' ', count: 15 - severity.Length);
                result.AppendLine($@"      <Rule Id=""{rule.Id}"" Action=""{severity}"" /> {spacing} <!-- {rule.Title} -->");
            }

            static string getSeverityString(DiagnosticSeverity? severity)
            {
                return severity.HasValue ? severity.ToString() ?? "None" : "None";
            }
        }

        private static void CreateEditorconfig(
            string analyzerEditorconfigsDir,
            string editorconfigFolder,
            string editorconfigTitle,
            string editorconfigDescription,
            RulesetKind rulesetKind,
            string? category,
            string? customTag,
            SortedList<string, DiagnosticDescriptor> sortedRulesById)
        {
            var text = GetRulesetOrEditorconfigText(
                rulesetKind,
                startEditorconfig,
                endEditorconfig,
                startRulesSection,
                endRulesSection,
                addRuleEntry,
                getSeverityString,
                commentStart: "# ",
                commentEnd: string.Empty,
                category,
                customTag,
                sortedRulesById);

            var directory = Directory.CreateDirectory(Path.Combine(analyzerEditorconfigsDir, editorconfigFolder));
            var editorconfigFilePath = Path.Combine(directory.FullName, ".editorconfig");

            // This doesn't need validation as the generated file is part of artifacts.
            File.WriteAllText(editorconfigFilePath, text);
            return;

            // Local functions
            void startEditorconfig(StringBuilder result)
            {
                result.AppendLine(@"# NOTE: Requires **VS2019 16.3** or later");
                result.AppendLine();
                result.AppendLine($@"# {editorconfigTitle}");
                result.AppendLine($@"# Description: {editorconfigDescription}");
                result.AppendLine();
                result.AppendLine(@"# Code files");
                result.AppendLine(@"[*.{cs,vb}]");
                result.AppendLine();
            }

            static void endEditorconfig(StringBuilder _)
            {
            }

            static void startRulesSection(StringBuilder _)
            {
            }

            static void endRulesSection(StringBuilder _)
            {
            }

            static void addRuleEntry(StringBuilder result, DiagnosticDescriptor rule, string severity)
            {
                result.AppendLine();
                result.AppendLine($"# {rule.Id}: {rule.Title}");
                result.AppendLine($@"dotnet_diagnostic.{rule.Id}.severity = {severity}");
            }

            static string getSeverityString(DiagnosticSeverity? severity)
            {
                if (!severity.HasValue)
                {
                    return "none";
                }

                return severity.Value switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    DiagnosticSeverity.Info => "suggestion",
                    DiagnosticSeverity.Hidden => "silent",
                    _ => throw new NotImplementedException(severity.Value.ToString()),
                };
            }
        }

        private static string GetRulesetOrEditorconfigText(
            RulesetKind rulesetKind,
            Action<StringBuilder> startRulesetOrEditorconfig,
            Action<StringBuilder> endRulesetOrEditorconfig,
            Action<StringBuilder> startRulesSection,
            Action<StringBuilder> endRulesSection,
            Action<StringBuilder, DiagnosticDescriptor, string> addRuleEntry,
            Func<DiagnosticSeverity?, string> getSeverityString,
            string commentStart,
            string commentEnd,
            string? category,
            string? customTag,
            SortedList<string, DiagnosticDescriptor> sortedRulesById)
        {
            Debug.Assert(category == null || customTag == null);
            Debug.Assert(category != null == (rulesetKind == RulesetKind.CategoryDefault || rulesetKind == RulesetKind.CategoryEnabled));
            Debug.Assert(customTag != null == (rulesetKind == RulesetKind.CustomTagDefault || rulesetKind == RulesetKind.CustomTagEnabled));

            var result = new StringBuilder();
            startRulesetOrEditorconfig(result);
            if (category == null && customTag == null)
            {
                addRules(categoryPass: false, customTagPass: false);
            }
            else
            {
                result.AppendLine($@"{commentStart}{category ?? customTag} Rules{commentEnd}");
                addRules(categoryPass: category != null, customTagPass: customTag != null);
                result.AppendLine();
                result.AppendLine();
                result.AppendLine();
                result.AppendLine($@"{commentStart}Other Rules{commentEnd}");
                addRules(categoryPass: false, customTagPass: false);
            }

            endRulesetOrEditorconfig(result);
            return result.ToString();

            void addRules(bool categoryPass, bool customTagPass)
            {
                if (!sortedRulesById.Any(r => !shouldSkipRule(r.Value)))
                {
                    // Bail out if we don't have any rule to be added for this assembly.
                    return;
                }

                startRulesSection(result);

                foreach (var rule in sortedRulesById)
                {
                    addRule(rule.Value);
                }

                endRulesSection(result);

                return;

                void addRule(DiagnosticDescriptor rule)
                {
                    if (shouldSkipRule(rule))
                    {
                        return;
                    }

                    string severity = getRuleAction(rule);
                    addRuleEntry(result, rule, severity);
                }

                bool shouldSkipRule(DiagnosticDescriptor rule)
                {
                    switch (rulesetKind)
                    {
                        case RulesetKind.CategoryDefault:
                        case RulesetKind.CategoryEnabled:
                            if (categoryPass)
                            {
                                return rule.Category != category;
                            }
                            else
                            {
                                return rule.Category == category;
                            }

                        case RulesetKind.CustomTagDefault:
                        case RulesetKind.CustomTagEnabled:
                            if (customTagPass)
                            {
                                return !rule.CustomTags.Contains(customTag);
                            }
                            else
                            {
                                return rule.CustomTags.Contains(customTag);
                            }

                        default:
                            return false;
                    }
                }

                string getRuleAction(DiagnosticDescriptor rule)
                {
                    return rulesetKind switch
                    {
                        RulesetKind.CategoryDefault => getRuleActionCore(enable: categoryPass && rule.IsEnabledByDefault),

                        RulesetKind.CategoryEnabled => getRuleActionCore(enable: categoryPass, enableAsWarning: categoryPass),

                        RulesetKind.CustomTagDefault => getRuleActionCore(enable: customTagPass && rule.IsEnabledByDefault),

                        RulesetKind.CustomTagEnabled => getRuleActionCore(enable: customTagPass, enableAsWarning: customTagPass),

                        RulesetKind.AllDefault => getRuleActionCore(enable: rule.IsEnabledByDefault),

                        RulesetKind.AllEnabled => getRuleActionCore(enable: true, enableAsWarning: true),

                        RulesetKind.AllDisabled => getRuleActionCore(enable: false),

                        _ => throw new InvalidProgramException(),
                    };

                    string getRuleActionCore(bool enable, bool enableAsWarning = false)
                    {
                        if (!enable && enableAsWarning)
                        {
                            throw new ArgumentException($"Unexpected arguments. '{nameof(enable)}' can't be false while '{nameof(enableAsWarning)}' is true.");
                        }
                        else if (enable)
                        {
                            return getSeverityString(enableAsWarning ? DiagnosticSeverity.Warning : rule.DefaultSeverity);
                        }
                        else
                        {
                            return getSeverityString(null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates whether <paramref name="fileContents"/> matches the contents of <paramref name="fileWithPath"/>.
        /// If they don't match, <paramref name="fileWithPath"/> is added to <paramref name="fileNamesWithValidationFailures"/>.
        /// The validation process is run within CI, so that the CI build fails when the auto-generated files are out of date.
        /// </summary>
        /// <remarks>
        /// Don't call this method with auto-generated files that are part of the artifacts because it's expected that they don't initially exist.
        /// </remarks>
        private static void Validate(string fileWithPath, string fileContents, List<string> fileNamesWithValidationFailures)
        {
            string actual = File.ReadAllText(fileWithPath);
            if (actual != fileContents)
            {
                fileNamesWithValidationFailures.Add(fileWithPath);
            }
        }

        private static void CreateGlobalconfig(
            string folder,
            string editorconfigFileName,
            string editorconfigTitle,
            string editorconfigDescription,
            AnalysisMode analysisMode,
            string? category,
            SortedList<string, DiagnosticDescriptor> sortedRulesById,
            (ImmutableArray<ReleaseTrackingData> shippedFiles, Version version) shippedReleaseData)
        {
            Debug.Assert(editorconfigFileName.EndsWith(".editorconfig", StringComparison.Ordinal));

            var text = GetGlobalconfigText(
                editorconfigTitle,
                editorconfigDescription,
                analysisMode,
                category,
                sortedRulesById,
                shippedReleaseData);
            var directory = Directory.CreateDirectory(folder);
            var editorconfigFilePath = Path.Combine(directory.FullName, editorconfigFileName);
            File.WriteAllText(editorconfigFilePath, text);
            return;

            // Local functions
            static string GetGlobalconfigText(
                string editorconfigTitle,
                string editorconfigDescription,
                AnalysisMode analysisMode,
                string? category,
                SortedList<string, DiagnosticDescriptor> sortedRulesById,
                (ImmutableArray<ReleaseTrackingData> shippedFiles, Version version)? shippedReleaseData)
            {
                var result = new StringBuilder();
                StartGlobalconfig();
                AddRules(analysisMode, category);
                return result.ToString();

                void StartGlobalconfig()
                {
                    result.AppendLine(@"# NOTE: Requires **VS2019 16.7** or later");
                    result.AppendLine();
                    result.AppendLine($@"# {editorconfigTitle}");
                    result.AppendLine($@"# Description: {editorconfigDescription}");
                    result.AppendLine();
                    result.AppendLine($@"is_global = true");
                    result.AppendLine();

                    // Append 'global_level' to ensure conflicts are properly resolved between different global configs:
                    //   1. Lowest precedence (-2): Category-agnostic config generated by us.
                    //   2. Higher precedence (-1): Category-specific config generated by us.
                    //   3. Highest predence (non-negative integer): User provided config.
                    // See https://github.com/dotnet/roslyn/issues/48634 for further details.
                    var globalLevel = category != null ? -1 : -2;
                    result.AppendLine($@"global_level = {globalLevel}");
                    result.AppendLine();
                }

                bool AddRules(AnalysisMode analysisMode, string? category)
                {
                    Debug.Assert(sortedRulesById.Count > 0);

                    var addedRule = false;
                    foreach (var rule in sortedRulesById)
                    {
                        if (AddRule(rule.Value, category))
                        {
                            addedRule = true;
                        }
                    }

                    return addedRule;

                    bool AddRule(DiagnosticDescriptor rule, string? category)
                    {
                        if (category != null &&
                            !string.Equals(rule.Category, category, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        var (isEnabledByDefault, severity) = GetEnabledByDefaultAndSeverity(rule, analysisMode);
                        if (rule.IsEnabledByDefault == isEnabledByDefault &&
                            severity == rule.DefaultSeverity)
                        {
                            // Rule had the same default severity and enabled state in the release.
                            // We do not need to generate any entry.
                            return false;
                        }

                        string severityString = GetRuleSeverity(isEnabledByDefault, severity);

                        result.AppendLine();
                        result.AppendLine($"# {rule.Id}: {rule.Title}");
                        result.AppendLine($@"dotnet_diagnostic.{rule.Id}.severity = {severityString}");
                        return true;
                    }

                    (bool isEnabledByDefault, DiagnosticSeverity effectiveSeverity) GetEnabledByDefaultAndSeverity(DiagnosticDescriptor rule, AnalysisMode analysisMode)
                    {
                        var isEnabledByDefault = rule.IsEnabledByDefault;
                        var effectiveSeverity = rule.DefaultSeverity;

                        bool isEnabledRuleForNonDefaultAnalysisMode;
                        switch (analysisMode)
                        {
                            case AnalysisMode.None:
                                // Disable all rules by default.
                                return (isEnabledByDefault: false, DiagnosticSeverity.Warning);

                            case AnalysisMode.All:
                                // Escalate all rules with a special custom tag to be build warnings.
                                isEnabledRuleForNonDefaultAnalysisMode = rule.CustomTags.Contains(WellKnownDiagnosticTagsExtensions.EnabledRuleInAggressiveMode);
                                break;

                            case AnalysisMode.Minimum:
                                // Escalate all enabled, non-hidden rules to be build warnings.
                                isEnabledRuleForNonDefaultAnalysisMode = isEnabledByDefault && effectiveSeverity != DiagnosticSeverity.Hidden;
                                break;

                            case AnalysisMode.Recommended:
                                // Escalate all enabled rules to be build warnings.
                                isEnabledRuleForNonDefaultAnalysisMode = isEnabledByDefault;
                                break;

                            case AnalysisMode.Default:
                                // Retain the default severity and enabled by default values.
                                isEnabledRuleForNonDefaultAnalysisMode = false;
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        if (isEnabledRuleForNonDefaultAnalysisMode)
                        {
                            isEnabledByDefault = true;
                            effectiveSeverity = DiagnosticSeverity.Warning;
                        }

                        if (shippedReleaseData != null)
                        {
                            isEnabledByDefault = isEnabledRuleForNonDefaultAnalysisMode;
                            var maxVersion = shippedReleaseData.Value.version;
                            foreach (var shippedFile in shippedReleaseData.Value.shippedFiles)
                            {
                                if (shippedFile.TryGetLatestReleaseTrackingLine(rule.Id, maxVersion, out _, out var releaseTrackingLine) &&
                                    releaseTrackingLine.EnabledByDefault.HasValue &&
                                    releaseTrackingLine.DefaultSeverity.HasValue)
                                {
                                    isEnabledByDefault = releaseTrackingLine.EnabledByDefault.Value && !releaseTrackingLine.IsRemovedRule;
                                    effectiveSeverity = releaseTrackingLine.DefaultSeverity.Value;

                                    if (isEnabledRuleForNonDefaultAnalysisMode && !releaseTrackingLine.IsRemovedRule)
                                    {
                                        isEnabledByDefault = true;
                                        effectiveSeverity = DiagnosticSeverity.Warning;
                                    }

                                    break;
                                }
                            }
                        }

                        return (isEnabledByDefault, effectiveSeverity);
                    }

                    static string GetRuleSeverity(bool isEnabledByDefault, DiagnosticSeverity defaultSeverity)
                    {
                        if (isEnabledByDefault)
                        {
                            return GetSeverityString(defaultSeverity);
                        }
                        else
                        {
                            return GetSeverityString(null);
                        }

                        static string GetSeverityString(DiagnosticSeverity? severity)
                        {
                            if (!severity.HasValue)
                            {
                                return "none";
                            }

                            return severity.Value switch
                            {
                                DiagnosticSeverity.Error => "error",
                                DiagnosticSeverity.Warning => "warning",
                                DiagnosticSeverity.Info => "suggestion",
                                DiagnosticSeverity.Hidden => "silent",
                                _ => throw new NotImplementedException(severity.Value.ToString()),
                            };
                        }
                    }
                }
            }
        }

        private static void CreateTargetsFile(string targetsFileDir, string targetsFileName, string packageName, IOrderedEnumerable<string> categories)
        {
            if (string.IsNullOrEmpty(targetsFileDir) || string.IsNullOrEmpty(targetsFileName))
            {
                return;
            }

            var fileContents =
$@"<Project>{GetCommonContents(packageName, categories)}{GetPackageSpecificContents(packageName)}
</Project>";
            var directory = Directory.CreateDirectory(targetsFileDir);
            var fileWithPath = Path.Combine(directory.FullName, targetsFileName);
            File.WriteAllText(fileWithPath, fileContents);

            static string GetCommonContents(string packageName, IOrderedEnumerable<string> categories)
            {
                var stringBuilder = new StringBuilder();

                stringBuilder.Append(GetGlobalAnalyzerConfigTargetContents(packageName, category: null));
                foreach (var category in categories)
                {
                    stringBuilder.Append(GetGlobalAnalyzerConfigTargetContents(packageName, category));
                }

                stringBuilder.Append(GetMSBuildContentForPropertyAndItemOptions());
                stringBuilder.Append(GetCodeAnalysisTreatWarningsAsErrorsTargetContents());
                return stringBuilder.ToString();
            }

            static string GetGlobalAnalyzerConfigTargetContents(string packageName, string? category)
            {
                var analysisLevelPropName = "AnalysisLevel";
                var analysisLevelPrefixPropName = "AnalysisLevelPrefix";
                var analysisLevelSuffixPropName = "AnalysisLevelSuffix";
                var analysisModePropName = nameof(AnalysisMode);
                var effectiveAnalysisLevelPropName = "EffectiveAnalysisLevel";
                var targetCondition = "'$(SkipGlobalAnalyzerConfigForPackage)' != 'true'";
                var afterTargets = string.Empty;
                var trimmedPackageName = packageName.Replace(".", string.Empty, StringComparison.Ordinal);

                if (!string.IsNullOrEmpty(category))
                {
                    analysisLevelPropName += category;
                    analysisLevelPrefixPropName += category;
                    analysisLevelSuffixPropName += category;
                    analysisModePropName += category;
                    effectiveAnalysisLevelPropName += category;

                    // For category-specific target, we also check if end-user has overriden category-specific AnalysisLevel or AnalysisMode.
                    targetCondition += $" and ('$({analysisLevelPropName})' != '' or '$({analysisModePropName})' != '')";

                    // Ensure that category-specific target executes after category-agnostic target
                    afterTargets += $@"AfterTargets=""AddGlobalAnalyzerConfigForPackage_{trimmedPackageName}"" ";

                    trimmedPackageName += category;
                }

                var packageVersionPropName = trimmedPackageName + "RulesVersion";
                var propertyStringForSettingDefaultPropertyValues = GetPropertyStringForSettingDefaultPropertyValues(
                    packageName, packageVersionPropName, category, analysisLevelPropName,
                    analysisLevelPrefixPropName, analysisLevelSuffixPropName, effectiveAnalysisLevelPropName);

                return $@"
  <Target Name=""AddGlobalAnalyzerConfigForPackage_{trimmedPackageName}"" BeforeTargets=""CoreCompile"" {afterTargets}Condition=""{targetCondition}"">
    <!-- PropertyGroup to compute global analyzer config file to be used -->
    <PropertyGroup>{propertyStringForSettingDefaultPropertyValues}
      <!-- Set the default analysis mode, if not set by the user -->
      <_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>$({analysisLevelSuffixPropName})</_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>
      <_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName})' == ''"">$({analysisModePropName})</_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>
      <_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName})' == 'AllEnabledByDefault'"">{nameof(AnalysisMode.All)}</_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>
      <_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName})' == 'AllDisabledByDefault'"">{nameof(AnalysisMode.None)}</_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>
      <_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName})' == ''"">{nameof(AnalysisMode.Default)}</_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}>

      <!-- GlobalAnalyzerConfig file name based on user specified package version '{packageVersionPropName}', if any. We replace '.' with '_' to map the version string to file name suffix. -->
      <_GlobalAnalyzerConfigFileName_{trimmedPackageName} Condition=""'$({packageVersionPropName})' != ''"">{analysisLevelPropName}_$({packageVersionPropName}.Replace(""."",""_""))_$(_GlobalAnalyzerConfigAnalysisMode_{trimmedPackageName}).editorconfig</_GlobalAnalyzerConfigFileName_{trimmedPackageName}>

      <_GlobalAnalyzerConfigDir_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigDir_{trimmedPackageName})' == ''"">$(MSBuildThisFileDirectory)config</_GlobalAnalyzerConfigDir_{trimmedPackageName}>
      <_GlobalAnalyzerConfigFile_{trimmedPackageName} Condition=""'$(_GlobalAnalyzerConfigFileName_{trimmedPackageName})' != ''"">$(_GlobalAnalyzerConfigDir_{trimmedPackageName})\$(_GlobalAnalyzerConfigFileName_{trimmedPackageName})</_GlobalAnalyzerConfigFile_{trimmedPackageName}>
    </PropertyGroup>

    <ItemGroup Condition=""Exists('$(_GlobalAnalyzerConfigFile_{trimmedPackageName})')"">
      <EditorConfigFiles Include=""$(_GlobalAnalyzerConfigFile_{trimmedPackageName})"" />
    </ItemGroup>
  </Target>
";

                static string GetPropertyStringForSettingDefaultPropertyValues(
                    string packageName,
                    string packageVersionPropName,
                    string? category,
                    string analysisLevelPropName,
                    string analysisLevelPrefixPropName,
                    string analysisLevelSuffixPropName,
                    string effectiveAnalysisLevelPropName)
                {
                    if (packageName == NetAnalyzersPackageName)
                    {
                        var propertyStr = string.Empty;

                        if (!string.IsNullOrEmpty(category))
                        {
                            // For category-specific logic, we need to duplicate logic from SDK targets to set
                            // category-specific AnalysisLevel property values. In future, we should consider removing similar logic from
                            // SDK targets for core AnalysisLevel and instead generalize this logic.

                            propertyStr += $@"
      <!-- Default '{analysisLevelPropName}' to the core 'AnalysisLevel' and compute '{analysisLevelPrefixPropName}', '{analysisLevelSuffixPropName}' and '{effectiveAnalysisLevelPropName}' -->
      <{analysisLevelPropName} Condition=""'$({analysisLevelPropName})' == ''"">$(AnalysisLevel)</{analysisLevelPropName}>

      <!-- {analysisLevelPropName} can also contain compound values with a prefix and suffix separated by a '-' character.
           The prefix indicates the core AnalysisLevel for '{category}' rules and the suffix indicates the bucket of
           rules to enable for '{category}' rules by default. For example, some valid compound values for {analysisLevelPropName} are:
             1. '5-all' - Indicates core {analysisLevelPropName} = '5' with 'all' the '{category}' rules enabled by default.
             2. 'latest-none' - Indicates core {analysisLevelPropName} = 'latest' with 'none' of the '{category}' rules enabled by default.
           {analysisLevelPrefixPropName} is used to set the {effectiveAnalysisLevelPropName} below.
           {analysisLevelSuffixPropName} is used to map to the correct global config.
      -->
      <{analysisLevelPrefixPropName} Condition=""$({analysisLevelPropName}.Contains('-'))"">$([System.Text.RegularExpressions.Regex]::Replace($({analysisLevelPropName}), '-(.)*', ''))</{analysisLevelPrefixPropName}>
      <{analysisLevelSuffixPropName} Condition=""'$({analysisLevelPrefixPropName})' != ''"">$([System.Text.RegularExpressions.Regex]::Replace($({analysisLevelPropName}), '$({analysisLevelPrefixPropName})-', ''))</{analysisLevelSuffixPropName}>

      <!-- {effectiveAnalysisLevelPropName} is used to differentiate from user specified strings (such as 'none')
           and an implied numerical option (such as '4') -->
      <!-- TODO: Remove hard-coded constants such as 4.0, 5.0 and 6.0 used below once these are exposed as properties from the SDK -->
      <{effectiveAnalysisLevelPropName} Condition=""'$({analysisLevelPropName})' == 'none' or '$({analysisLevelPrefixPropName})' == 'none'"">4.0</{effectiveAnalysisLevelPropName}>
      <{effectiveAnalysisLevelPropName} Condition=""'$({analysisLevelPropName})' == 'latest' or '$({analysisLevelPrefixPropName})' == 'latest'"">5.0</{effectiveAnalysisLevelPropName}>
      <{effectiveAnalysisLevelPropName} Condition=""'$({analysisLevelPropName})' == 'preview' or '$({analysisLevelPrefixPropName})' == 'preview'"">6.0</{effectiveAnalysisLevelPropName}>

      <!-- Set {effectiveAnalysisLevelPropName} to the value of {analysisLevelPropName} if it is a version number -->
      <{effectiveAnalysisLevelPropName} Condition=""'$({effectiveAnalysisLevelPropName})' == '' And
                                         '$({analysisLevelPrefixPropName})' != ''"">$({analysisLevelPrefixPropName})</{effectiveAnalysisLevelPropName}>
      <{effectiveAnalysisLevelPropName} Condition=""'$({effectiveAnalysisLevelPropName})' == '' And
                                         '$({analysisLevelPropName})' != ''"">$({analysisLevelPropName})</{effectiveAnalysisLevelPropName}>
";
                        }

                        propertyStr += $@"
      <!-- Default '{packageVersionPropName}' to '{effectiveAnalysisLevelPropName}' with trimmed trailing '.0' -->
      <{packageVersionPropName} Condition=""'$({packageVersionPropName})' == '' and $({effectiveAnalysisLevelPropName}) != ''"">$([System.Text.RegularExpressions.Regex]::Replace($({effectiveAnalysisLevelPropName}), '(.0)*$', ''))</{packageVersionPropName}>
";
                        return propertyStr;
                    }

                    return string.Empty;
                }
            }

            static string GetMSBuildContentForPropertyAndItemOptions()
            {
                var builder = new StringBuilder();

                AddMSBuildContentForPropertyOptions(builder);
                AddMSBuildContentForItemOptions(builder);

                return builder.ToString();

                static void AddMSBuildContentForPropertyOptions(StringBuilder builder)
                {
                    var compilerVisibleProperties = new List<string>();
                    foreach (var field in typeof(MSBuildPropertyOptionNames).GetFields())
                    {
                        compilerVisibleProperties.Add(field.Name);
                    }

                    // Add ItemGroup for MSBuild property names that are required to be threaded as analyzer config options.
                    AddItemGroupForCompilerVisibleProperties(compilerVisibleProperties, builder);
                }

                static void AddItemGroupForCompilerVisibleProperties(List<string> compilerVisibleProperties, StringBuilder builder)
                {
                    builder.AppendLine($@"
  <!-- MSBuild properties to thread to the analyzers as options --> 
  <ItemGroup>");
                    foreach (var compilerVisibleProperty in compilerVisibleProperties)
                    {
                        builder.AppendLine($@"    <CompilerVisibleProperty Include=""{compilerVisibleProperty}"" />");
                    }

                    builder.AppendLine($@"  </ItemGroup>");
                }

                static void AddMSBuildContentForItemOptions(StringBuilder builder)
                {
                    // Add ItemGroup and PropertyGroup for MSBuild item names that are required to be treated as analyzer config options.
                    // The analyzer config option will have the following key/value:
                    // - Key: Item name prefixed with an '_' and suffixed with a 'List' to reduce chances of conflicts with any existing project property.
                    // - Value: Concatenated item metadata values, separated by a ',' character. See https://github.com/dotnet/sdk/issues/12706#issuecomment-668219422 for details.

                    builder.Append($@"
  <!-- MSBuild item metadata to thread to the analyzers as options -->
  <PropertyGroup>
");
                    var compilerVisibleProperties = new List<string>();
                    foreach (var field in typeof(MSBuildItemOptionNames).GetFields())
                    {
                        // Item option name: "SupportedPlatform"
                        // Generated MSBuild property: "<_SupportedPlatformList>@(SupportedPlatform, '<separator>')</_SupportedPlatformList>"

                        var itemOptionName = field.Name;
                        var propertyName = MSBuildItemOptionNamesHelpers.GetPropertyNameForItemOptionName(itemOptionName);
                        compilerVisibleProperties.Add(propertyName);
                        builder.AppendLine($@"    <{propertyName}>@({itemOptionName}, '{MSBuildItemOptionNamesHelpers.ValuesSeparator}')</{propertyName}>");
                    }

                    builder.AppendLine($@"  </PropertyGroup>");

                    AddItemGroupForCompilerVisibleProperties(compilerVisibleProperties, builder);
                }
            }

            static string GetCodeAnalysisTreatWarningsAsErrorsTargetContents()
            {
                return $@"
  <!--
    Design-time target to prevent the rule ids implemented in this package to be bumped to errors in the IDE
    when 'CodeAnalysisTreatWarningsAsErrors' = 'false'. Note that a similar 'WarningsNotAsErrors'
    property group is present in the generated props file to ensure this functionality on command line builds.
  -->
  <Target Name=""_CodeAnalysisTreatWarningsNotAsErrors"" BeforeTargets=""CoreCompile"" Condition=""'$(CodeAnalysisTreatWarningsAsErrors)' == 'false' AND ('$(DesignTimeBuild)' == 'true' OR '$(BuildingProject)' != 'true')"">
    <PropertyGroup>
      <WarningsNotAsErrors>$(WarningsNotAsErrors);$(CodeAnalysisRuleIds)</WarningsNotAsErrors>
    </PropertyGroup>
  </Target>
";
            }

            static string GetPackageSpecificContents(string packageName)
                => packageName switch
                {
                    CodeAnalysisAnalyzersPackageName => @"
  <!-- Target to add all 'EmbeddedResource' files with '.resx' extension as analyzer additional files -->
  <Target Name=""AddAllResxFilesAsAdditionalFiles"" BeforeTargets=""CoreCompile"" Condition=""'@(EmbeddedResource)' != '' AND '$(SkipAddAllResxFilesAsAdditionalFiles)' != 'true'"">
    <ItemGroup>
      <EmbeddedResourceWithResxExtension Include=""@(EmbeddedResource)"" Condition=""'%(Extension)' == '.resx'"" />
      <AdditionalFiles Include=""%(EmbeddedResourceWithResxExtension.Identity)"" />
    </ItemGroup>
  </Target>

  <!-- Workaround for https://github.com/dotnet/roslyn/issues/4655 -->
  <ItemGroup Condition=""Exists('$(MSBuildProjectDirectory)\AnalyzerReleases.Shipped.md')"" >
	<AdditionalFiles Include=""AnalyzerReleases.Shipped.md"" />
  </ItemGroup>
  <ItemGroup Condition=""Exists('$(MSBuildProjectDirectory)\AnalyzerReleases.Unshipped.md')"" >
	<AdditionalFiles Include=""AnalyzerReleases.Unshipped.md"" />
  </ItemGroup>",
                    PublicApiAnalyzersPackageName => @"

  <!-- Workaround for https://github.com/dotnet/roslyn/issues/4655 -->
  <ItemGroup Condition=""Exists('$(MSBuildProjectDirectory)\PublicAPI.Shipped.txt')"" >
	<AdditionalFiles Include=""PublicAPI.Shipped.txt"" />
  </ItemGroup>
  <ItemGroup Condition=""Exists('$(MSBuildProjectDirectory)\PublicAPI.Unshipped.txt')"" >
	<AdditionalFiles Include=""PublicAPI.Unshipped.txt"" />
  </ItemGroup>",
                    PerformanceSensitiveAnalyzersPackageName => @"
  <PropertyGroup>
    <GeneratePerformanceSensitiveAttribute Condition=""'$(GeneratePerformanceSensitiveAttribute)' == ''"">true</GeneratePerformanceSensitiveAttribute>
    <PerformanceSensitiveAttributePath Condition=""'$(PerformanceSensitiveAttributePath)' == ''"">$(MSBuildThisFileDirectory)PerformanceSensitiveAttribute$(DefaultLanguageSourceExtension)</PerformanceSensitiveAttributePath>
  </PropertyGroup>

  <ItemGroup Condition=""'$(GeneratePerformanceSensitiveAttribute)' == 'true' and Exists($(PerformanceSensitiveAttributePath))"">
    <Compile Include=""$(PerformanceSensitiveAttributePath)"" Visible=""false"" />

    <!-- Make sure the source file is embedded in PDB to support Source Link -->
    <EmbeddedFiles Condition=""'$(DebugType)' != 'none'"" Include=""$(PerformanceSensitiveAttributePath)"" />
  </ItemGroup>",
                    NetAnalyzersPackageName => $@"
  <!-- Target to report a warning when SDK NetAnalyzers version is higher than the referenced NuGet NetAnalyzers version -->
  <Target Name=""_ReportUpgradeNetAnalyzersNuGetWarning"" BeforeTargets=""CoreCompile"" Condition=""'$(_SkipUpgradeNetAnalyzersNuGetWarning)' != 'true' "">
    <Warning Text =""The .NET SDK has newer analyzers with version '$({NetAnalyzersSDKAssemblyVersionPropertyName})' than what version '$({NetAnalyzersNugetAssemblyVersionPropertyName})' of '{NetAnalyzersPackageName}' package provides. Update or remove this package reference.""
             Condition=""'$({NetAnalyzersNugetAssemblyVersionPropertyName})' != '' AND
                         '$({NetAnalyzersSDKAssemblyVersionPropertyName})' != '' AND
                          $({NetAnalyzersNugetAssemblyVersionPropertyName}) &lt; $({NetAnalyzersSDKAssemblyVersionPropertyName})""/>
  </Target>",
                    _ => string.Empty,
                };
        }

        private enum RulesetKind
        {
            AllDefault,
            CategoryDefault,
            CustomTagDefault,
            AllEnabled,
            CategoryEnabled,
            CustomTagEnabled,
            AllDisabled,
        }

        // NOTE: **Do not** change the names of the fields for this enum - that would be a breaking change for user visible property setting for `AnalysisMode` property in MSBuild project file.
        private enum AnalysisMode
        {
            Default,
            None,
            Minimum,
            Recommended,
            All
        }

        private sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            public static IAnalyzerAssemblyLoader Instance = new AnalyzerAssemblyLoader();

            private AnalyzerAssemblyLoader() { }
            public void AddDependencyLocation(string fullPath)
            {
            }

            public Assembly LoadFromPath(string fullPath)
            {
                return Assembly.LoadFrom(fullPath);
            }
        }
    }
}

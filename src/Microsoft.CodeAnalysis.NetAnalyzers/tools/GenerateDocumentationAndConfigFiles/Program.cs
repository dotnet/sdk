// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static GenerateDocumentationAndConfigFiles.CommonPropertyNames;

namespace GenerateDocumentationAndConfigFiles
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            const int expectedArguments = 17;

            if (args.Length != expectedArguments)
            {
                Console.Error.WriteLine($"Excepted {expectedArguments} arguments, found {args.Length}: {string.Join(';', args)}");
                return 1;
            }

            string analyzerRulesetsDir = args[0];
            string analyzerEditorconfigsDir = args[1];
            string binDirectory = args[2];
            string configuration = args[3];
            string tfm = args[4];
            var assemblyList = args[5].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string propsFileDir = args[6];
            string propsFileName = args[7];
            string propsFileToDisableNetAnalyzersInNuGetPackageName = args[8];
            string analyzerDocumentationFileDir = args[9];
            string analyzerDocumentationFileName = args[10];
            string analyzerSarifFileDir = args[11];
            string analyzerSarifFileName = args[12];
            var analyzerVersion = args[13];
            var analyzerPackageName = args[14];
            if (!bool.TryParse(args[15], out var containsPortedFxCopRules))
            {
                containsPortedFxCopRules = false;
            }

            if (!bool.TryParse(args[16], out var generateAnalyzerRulesMissingDocumentationFile))
            {
                generateAnalyzerRulesMissingDocumentationFile = false;
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
                "All Rules Enabled with default severity",
                "All Rules are enabled with default severity. Rules with IsEnabledByDefault = false are force enabled with default severity.",
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
                    $"{category} Rules Enabled with default severity",
                    $@"All {category} Rules are enabled with default severity. {category} Rules with IsEnabledByDefault = false are force enabled with default severity. Rules from a different category are disabled.",
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
                    $"{customTag} Rules Enabled with default severity",
                    $@"All {customTag} Rules are enabled with default severity. {customTag} Rules with IsEnabledByDefault = false are force enabled with default severity. Non-{customTag} Rules are disabled.",
                    RulesetKind.CustomTagEnabled,
                    customTag: customTag);
            }

            createPropsFiles();

            createAnalyzerDocumentationFile();

            createAnalyzerSarifFile();

            if (generateAnalyzerRulesMissingDocumentationFile)
            {
                createAnalyzerRulesMissingDocumentationFile();
            }

            return 0;

            // Local functions.
            static void AnalyzerFileReference_AnalyzerLoadFailed(object? sender, AnalyzerLoadFailureEventArgs e)
                => throw e.Exception;

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
  {disableNetAnalyzersImport}{getCodeAnalysisTreatWarningsNotAsErrors()}
</Project>";
                var directory = Directory.CreateDirectory(propsFileDir);
                var fileWithPath = Path.Combine(directory.FullName, propsFileName);
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
                    File.WriteAllText(fileWithPath, fileContents);
                }

                return;

                string getDisableNetAnalyzersImport()
                {
                    if (!string.IsNullOrEmpty(propsFileToDisableNetAnalyzersInNuGetPackageName))
                    {
                        Debug.Assert(analyzerPackageName is NetAnalyzersPackageName or
                            FxCopAnalyzersPackageName or
                            NetCoreAnalyzersPackageName or
                            NetFrameworkAnalyzersPackageName or
                            CodeQualityAnalyzersPackageName);

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
  <PropertyGroup Condition=""'$(CodeAnalysisTreatWarningsAsErrors)' == 'false'"">
    <WarningsNotAsErrors>$(WarningsNotAsErrors);{allRuleIds}</WarningsNotAsErrors>
  </PropertyGroup>";
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

                var title = Path.GetFileNameWithoutExtension(analyzerDocumentationFileName);
                builder.AppendLine($"# {title}");
                builder.AppendLine();

                foreach (var ruleById in allRulesById)
                {
                    string ruleId = ruleById.Key;
                    DiagnosticDescriptor descriptor = ruleById.Value;

                    var ruleIdWithHyperLink = descriptor.Id;
                    if (!string.IsNullOrWhiteSpace(descriptor.HelpLinkUri))
                    {
                        ruleIdWithHyperLink = $"[{ruleIdWithHyperLink}]({descriptor.HelpLinkUri})";
                    }

                    builder.AppendLine($"## {ruleIdWithHyperLink}: {descriptor.Title}");
                    builder.AppendLine();

                    builder.AppendLine("|Item|Value|");
                    builder.AppendLine("|-|-|");
                    builder.AppendLine($"|Category|{descriptor.Category}|");
                    builder.AppendLine($"|Enabled|{descriptor.IsEnabledByDefault}|");
                    builder.AppendLine($"|Severity|{descriptor.DefaultSeverity}|");
                    var hasCodeFix = fixableDiagnosticIds.Contains(descriptor.Id);
                    builder.AppendLine($"|CodeFix|{hasCodeFix}|");
                    builder.AppendLine();

                    builder.AppendLine($"### Rule description");
                    builder.AppendLine();

                    var description = descriptor.Description.ToString(CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        description = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
                    }

                    // Replace line breaks with HTML breaks so that new
                    // lines don't break the markdown table formatting.
                    description = System.Text.RegularExpressions.Regex.Replace(description, "\r?\n", "<br>");
                    builder.AppendLine(description);
                    builder.AppendLine();
                }

                File.WriteAllText(fileWithPath, builder.ToString());
            }

            // based on https://github.com/dotnet/roslyn/blob/master/src/Compilers/Core/Portable/CommandLine/ErrorLogger.cs
            void createAnalyzerSarifFile()
            {
                if (string.IsNullOrEmpty(analyzerSarifFileDir) || string.IsNullOrEmpty(analyzerSarifFileName) || allRulesById.Count == 0)
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    return;
                }

                var culture = new CultureInfo("en-us");

                var directory = Directory.CreateDirectory(analyzerSarifFileDir);
                var fileWithPath = Path.Combine(directory.FullName, analyzerSarifFileName);

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

                        writer.Write("shortDescription", descriptor.Title.ToString(culture));

                        string fullDescription = descriptor.Description.ToString(culture);
                        writer.Write("fullDescription", !string.IsNullOrEmpty(fullDescription) ? fullDescription : descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture));

                        writer.Write("defaultLevel", getLevel(descriptor.DefaultSeverity));

                        if (!string.IsNullOrEmpty(descriptor.HelpLinkUri))
                        {
                            writer.Write("helpUri", descriptor.HelpLinkUri);
                        }

                        writer.WriteObjectStart("properties");

                        writer.Write("category", descriptor.Category);

                        writer.Write("isEnabledByDefault", descriptor.IsEnabledByDefault);

                        writer.Write("typeName", rule.Value.typeName);

                        if ((rule.Value.languages?.Length ?? 0) > 0)
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

            void createAnalyzerRulesMissingDocumentationFile()
            {
                if (string.IsNullOrEmpty(analyzerDocumentationFileDir) || allRulesById.Count == 0)
                {
                    Debug.Assert(!containsPortedFxCopRules);
                    return;
                }

                var directory = Directory.CreateDirectory(analyzerDocumentationFileDir);
                var fileWithPath = Path.Combine(directory.FullName, "RulesMissingDocumentation.md");

                var builder = new StringBuilder();
                builder.Append(@"## Rules without documentation

Rule ID | Missing Help Link | Title |
--------|-------------------|-------|
");

                foreach (var ruleById in allRulesById)
                {
                    string ruleId = ruleById.Key;
                    DiagnosticDescriptor descriptor = ruleById.Value;

                    var helpLinkUri = descriptor.HelpLinkUri;
                    if (!string.IsNullOrWhiteSpace(helpLinkUri) &&
                        checkHelpLink(helpLinkUri))
                    {
                        // Rule with valid documentation link
                        continue;
                    }

                    builder.AppendLine($"{ruleId} | {helpLinkUri} | {descriptor.Title} |");
                }

                File.WriteAllText(fileWithPath, builder.ToString());
                return;

                static bool checkHelpLink(string helpLink)
                {
                    try
                    {
                        if (!Uri.TryCreate(helpLink, UriKind.Absolute, out var uri))
                        {
                            return false;
                        }

                        var request = (HttpWebRequest)WebRequest.Create(uri);
                        request.Method = "HEAD";
                        using var response = request.GetResponse() as HttpWebResponse;
                        return response?.StatusCode == HttpStatusCode.OK;
                    }
                    catch (WebException)
                    {
                        return false;
                    }
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

                        RulesetKind.CategoryEnabled => getRuleActionCore(enable: categoryPass),

                        RulesetKind.CustomTagDefault => getRuleActionCore(enable: customTagPass && rule.IsEnabledByDefault),

                        RulesetKind.CustomTagEnabled => getRuleActionCore(enable: customTagPass),

                        RulesetKind.AllDefault => getRuleActionCore(enable: rule.IsEnabledByDefault),

                        RulesetKind.AllEnabled => getRuleActionCore(enable: true),

                        RulesetKind.AllDisabled => getRuleActionCore(enable: false),

                        _ => throw new InvalidProgramException(),
                    };

                    string getRuleActionCore(bool enable)
                    {
                        if (enable)
                        {
                            return getSeverityString(rule.DefaultSeverity);
                        }
                        else
                        {
                            return getSeverityString(null);
                        }
                    }
                }
            }
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

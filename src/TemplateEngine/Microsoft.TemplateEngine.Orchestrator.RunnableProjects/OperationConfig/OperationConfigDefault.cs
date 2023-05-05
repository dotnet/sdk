// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig
{
    internal class OperationConfigDefault
    {
        internal OperationConfigDefault(string glob, string flagPrefix, EvaluatorType evaluator, ConditionalType type)
        {
            Evaluator = evaluator;
            Glob = glob;
            FlagPrefix = flagPrefix;
            ConditionalStyle = type;
        }

        /// <summary>
        /// Gets default operation config.
        /// </summary>
        internal static OperationConfigDefault Default { get; } = new(glob: string.Empty, flagPrefix: string.Empty, evaluator: EvaluatorType.CPP, type: ConditionalType.None);

        /// <summary>
        /// Gets default global operation config.
        /// </summary>
        internal static OperationConfigDefault DefaultGlobalConfig { get; } = new(glob: string.Empty, flagPrefix: "//", evaluator: EvaluatorType.CPP, type: ConditionalType.CLineComments);

        /// <summary>
        /// Gets default special operation config.
        /// </summary>
        internal static IReadOnlyList<OperationConfigDefault> DefaultSpecialConfig { get; } = new[]
                    {
                        new OperationConfigDefault("**/*.js", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.es", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.es6", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.ts", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.json", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.jsonld", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.hjson", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.json5", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.geojson", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.topojson", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.bowerrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.npmrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.job", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.postcssrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.babelrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.csslintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.eslintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.jade-lintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.pug-lintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.jshintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.stylelintrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.yarnrc", "//", EvaluatorType.CPP, ConditionalType.CLineComments),
                        new OperationConfigDefault("**/*.css.min", "/*", EvaluatorType.CPP, ConditionalType.CBlockComments),
                        new OperationConfigDefault("**/*.css", "/*", EvaluatorType.CPP, ConditionalType.CBlockComments),
                        new OperationConfigDefault("**/*.cshtml", "@*", EvaluatorType.CPP, ConditionalType.Razor),
                        new OperationConfigDefault("**/*.razor", "@*", EvaluatorType.CPP, ConditionalType.Razor),
                        new OperationConfigDefault("**/*.vbhtml", "@*", EvaluatorType.VB, ConditionalType.Razor),
                        new OperationConfigDefault("**/*.cs", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.fs", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.c", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.cpp", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.cxx", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.h", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.hpp", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.hxx", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.cake", "//", EvaluatorType.CPP, ConditionalType.CNoComments),
                        new OperationConfigDefault("**/*.*proj", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.*proj.user", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.pubxml", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.pubxml.user", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.msbuild", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.targets", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.props", "<!--/", EvaluatorType.MSBuild, ConditionalType.MSBuild),
                        new OperationConfigDefault("**/*.svg", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.*htm", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.*html", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.md", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.jsp", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.asp", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.aspx", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/app.config", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/web.config", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/web.*.config", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/packages.config", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/nuget.config", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.nuspec", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.xslt", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.xsd", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.vsixmanifest", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.vsct", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.storyboard", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.axml", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.plist", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.xib", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.strings", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.bat", "rem --:", EvaluatorType.CPP, ConditionalType.RemLineComment),
                        new OperationConfigDefault("**/*.cmd", "rem --:", EvaluatorType.CPP, ConditionalType.RemLineComment),
                        new OperationConfigDefault("**/nginx.conf", "#--", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/robots.txt", "#--", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/*.sh", "#--", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/*.ps1", "#--", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/*.haml", "-#", EvaluatorType.CPP, ConditionalType.HamlLineComment),
                        new OperationConfigDefault("**/*.jsx", "{/*", EvaluatorType.CPP, ConditionalType.JsxBlockComment),
                        new OperationConfigDefault("**/*.tsx", "{/*", EvaluatorType.CPP, ConditionalType.JsxBlockComment),
                        new OperationConfigDefault("**/*.xml", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.appxmanifest", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.resx", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.bas", "'", EvaluatorType.VB, ConditionalType.VB),
                        new OperationConfigDefault("**/*.vb", "'", EvaluatorType.VB, ConditionalType.VB),
                        new OperationConfigDefault("**/*.xaml", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.axaml", "<!--", EvaluatorType.CPP, ConditionalType.Xml),
                        new OperationConfigDefault("**/*.sln", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/*.yaml", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/*.yml", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/Dockerfile", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/.editorconfig", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/.gitattributes", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/.gitignore", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment),
                        new OperationConfigDefault("**/.dockerignore", "#-", EvaluatorType.CPP, ConditionalType.HashSignLineComment)
                    };

        /// <summary>
        /// Gets the files the config applies to.
        /// Empty string means that the config is applied to all files.
        /// </summary>
        internal string Glob { get; }

        /// <summary>
        /// Gets the evaluator to use for the applicable files.
        /// </summary>
        internal EvaluatorType Evaluator { get; }

        /// <summary>
        /// Gets the prefix for flags to use for the applicable files.
        /// </summary>
        internal string FlagPrefix { get; }

        /// <summary>
        /// Gets conditional type to use for the applicable files.
        /// </summary>
        internal ConditionalType ConditionalStyle { get; }
    }
}

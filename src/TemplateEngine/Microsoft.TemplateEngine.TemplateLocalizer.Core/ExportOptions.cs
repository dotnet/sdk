// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    public struct ExportOptions : IEquatable<ExportOptions>
    {
        /// <summary>
        /// Creates an instance of <see cref="ExportOptions"/>.
        /// </summary>
        /// <param name="dryRun">Specifies whether export operation skip flushing files to disk.</param>
        /// <param name="targetDirectory">Path to the directory where the generated files will be saved into.</param>
        /// <param name="languages">A set of languages for which the localizable files will be exported.</param>
        public ExportOptions(bool dryRun, string? targetDirectory = default, IEnumerable<string>? languages = default)
        {
            DryRun = dryRun;
            TargetDirectory = targetDirectory;
            Languages = languages;
        }

        /// <summary>
        /// Gets the default list of languages for which the localizable files will be exported.
        /// </summary>
        public static IReadOnlyList<string> DefaultLanguages { get; } = new[]
        {
            "cs",
            "de",
            "en",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "pl",
            "pt-BR",
            "ru",
            "tr",
            "zh-Hans",
            "zh-Hant",
        };

        /// <summary>
        /// Gets the languages for which localizable files will be exported.
        /// </summary>
        public IEnumerable<string>? Languages { get; }

        /// <summary>
        /// Gets the path to the directory to export into. If null, files will be exported into
        /// a "localize" folder next to the template.json file.
        /// </summary>
        public string? TargetDirectory { get; }

        /// <summary>
        /// Gets a value indicating whether the export process should skip
        /// flushing the file changes to file system.
        /// </summary>
        public bool DryRun { get; }

        public static bool operator ==(ExportOptions x, ExportOptions y) => x.Equals(y);

        public static bool operator !=(ExportOptions x, ExportOptions y) => !(x == y);

        public bool Equals(ExportOptions other)
        {
            return DryRun == other.DryRun
                && Languages == other.Languages
                && TargetDirectory == other.TargetDirectory;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ExportOptions other))
            {
                return false;
            }

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked(((17 * 23 + DryRun.GetHashCode()) * 23
                + (TargetDirectory?.GetHashCode() ?? 0)) * 23
                + (Languages?.GetHashCode() ?? 0));
        }

        public override string ToString()
        {
            return $"({DryRun}, {TargetDirectory}, {string.Join(", ", Languages ?? Enumerable.Empty<string>())})";
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Suppression engine that contains a collection of <see cref="Suppression"/> items. It provides API to add a suppression, check if a passed-in suppression is already suppressed
    /// and serialize all suppressions down into a file.
    /// </summary>
    public class SuppressionEngine : ISuppressionEngine
    {
        protected const string DiagnosticIdDocumentationComment = " https://learn.microsoft.com/en-us/dotnet/fundamentals/package-validation/diagnostic-ids ";
        private readonly HashSet<Suppression> _baselineSuppressions;
        private readonly HashSet<Suppression> _suppressions = new();
        private readonly HashSet<string> _noWarn;

        /// <inheritdoc/>
        public bool BaselineAllErrors { get; }

        /// <inheritdoc/>
        public IReadOnlyCollection<Suppression> BaselineSuppressions => _baselineSuppressions;

        /// <inheritdoc/>
        public IReadOnlyCollection<Suppression> Suppressions => _suppressions;

        /// <inheritdoc/>
        public SuppressionEngine(string[]? suppressionFiles = null,
            string? noWarn = null,
            bool baselineAllErrors = false)
        {
            BaselineAllErrors = baselineAllErrors;
            _noWarn = string.IsNullOrEmpty(noWarn) ? new HashSet<string>() : new HashSet<string>(noWarn!.Split(';'));
            _baselineSuppressions = ParseSuppressionFiles(suppressionFiles);
        }

        /// <inheritdoc/>
        public bool IsErrorSuppressed(Suppression error)
        {
            if (_noWarn.Contains(error.DiagnosticId))
            {
                return true;
            }

            if (_suppressions.Contains(error))
            {
                return true;
            }

            if (_baselineSuppressions.Contains(error))
            {
                _suppressions.Add(error);
                return true;
            }

            // Only CP errors can have "global suppressions". Global suppressions are ones that could apply to more than just one compatibility difference.
            if (error.DiagnosticId.StartsWith("cp", StringComparison.InvariantCultureIgnoreCase))
            {
                // - DiagnosticId, Target, IsBaselineSuppression
                Suppression globalTargetSuppression = new(error.DiagnosticId, error.Target, isBaselineSuppression: error.IsBaselineSuppression);

                // - DiagnosticId, Left, Right, IsBaselineSuppression
                Suppression globalLeftRightSuppression = new(error.DiagnosticId, left: error.Left, right: error.Right, isBaselineSuppression: error.IsBaselineSuppression);

                if (_suppressions.Contains(globalTargetSuppression) ||
                    _suppressions.Contains(globalLeftRightSuppression))
                {
                    return true;
                }

                if (_baselineSuppressions.TryGetValue(globalTargetSuppression, out Suppression? globalSuppression) ||
                    _baselineSuppressions.TryGetValue(globalLeftRightSuppression, out globalSuppression))
                {
                    _suppressions.Add(globalSuppression);
                    return true;
                }
            }

            if (BaselineAllErrors)
            {
                AddSuppression(error);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void AddSuppression(Suppression suppression) => _suppressions.Add(suppression);

        /// <inheritdoc/>
        public bool WriteSuppressionsToFile(string suppressionOutputFile, bool preserveUnnecessarySuppressions)
        {
            // If unnecessary suppressions should be preserved in the suppression file, union the
            // baseline suppressions with the set of actual suppressions. Duplicates are ignored.
            HashSet<Suppression> suppressionsToSerialize = _suppressions;
            if (preserveUnnecessarySuppressions)
            {
                suppressionsToSerialize.UnionWith(_baselineSuppressions);
            }

            if (suppressionsToSerialize.Count == 0)
                return false;

            Suppression[] orderedSuppressions = suppressionsToSerialize
                .OrderBy(suppression => suppression.DiagnosticId)
                .ThenBy(suppression => suppression.Left)
                .ThenBy(suppression => suppression.Right)
                .ThenBy(suppression => suppression.Target)
                .ToArray();

            using Stream stream = GetWritableStream(suppressionOutputFile);
            XmlWriter xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings()
            {
                Encoding = Encoding.UTF8,
                ConformanceLevel = ConformanceLevel.Document,
                Indent = true
            });

            xmlWriter.WriteComment(DiagnosticIdDocumentationComment);
            CreateXmlSerializer().Serialize(xmlWriter, orderedSuppressions);
            AfterWritingSuppressionsCallback(stream);

            return true;
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<Suppression> GetUnnecessarySuppressions() => _baselineSuppressions.Except(_suppressions).ToArray();

        protected virtual void AfterWritingSuppressionsCallback(Stream stream) { /* Do nothing. Used for tests. */ }

        // FileAccess.Read and FileShare.Read are specified to allow multiple processes to concurrently read from the suppression file.
        protected virtual Stream GetReadableStream(string suppressionFile) => new FileStream(suppressionFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        protected virtual Stream GetWritableStream(string suppressionFile) => new FileStream(suppressionFile, FileMode.Create);

        private HashSet<Suppression> ParseSuppressionFiles(string[]? suppressionFiles)
        {
            HashSet<Suppression> suppressions = new();

            if (suppressionFiles != null)
            {
                XmlSerializer serializer = CreateXmlSerializer();
                foreach (string suppressionFile in suppressionFiles)
                {
                    try
                    {
                        using Stream reader = GetReadableStream(suppressionFile);
                        if (serializer.Deserialize(reader) is Suppression[] deserializedSuppressions)
                        {
                            suppressions.UnionWith(deserializedSuppressions);
                        }
                    }
                    catch (FileNotFoundException) when (BaselineAllErrors)
                    {
                        // Throw if the passed in suppression file doesn't exist and errors aren't baselined.
                    }
                }
            }

            return suppressions;
        }

        private static XmlSerializer CreateXmlSerializer() => new(typeof(Suppression[]), new XmlRootAttribute("Suppressions"));
    }
}

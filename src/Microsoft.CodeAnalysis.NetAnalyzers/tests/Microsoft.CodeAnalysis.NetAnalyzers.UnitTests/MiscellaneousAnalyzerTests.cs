// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    public class MiscellaneousAnalyzerTests
    {
        private sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            public static IAnalyzerAssemblyLoader Instance = new AnalyzerAssemblyLoader();

            private AnalyzerAssemblyLoader() { }

            public void AddDependencyLocation(string fullPath) { }

            public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
        }

        [Fact]
        public void TestGlobalizationAnalyzersSubclassAbstractGlobalizationDiagnosticAnalyzer()
        {
            // <repo_root>\artifacts\bin\Microsoft.CodeAnalysis.NetAnalyzers.UnitTests\Debug\netcoreapp3.1\Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.dll
            var testsAssemblyPath = typeof(MiscellaneousAnalyzerTests).Assembly.Location;

            var directory = Path.GetDirectoryName(testsAssemblyPath);

            foreach (var assembly in new[] { "Microsoft.CodeAnalysis.NetAnalyzers.dll", "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll", "Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers.dll" })
            {
                var path = Path.Combine(directory, assembly);
                Assert.True(File.Exists(path), $"File {path} doesn't exist.");
                var analyzerFileReference = new AnalyzerFileReference(path, AnalyzerAssemblyLoader.Instance);
                analyzerFileReference.AnalyzerLoadFailed += AnalyzerFileReference_AnalyzerLoadFailed;
                var analyzers = analyzerFileReference.GetAnalyzersForAllLanguages();
                foreach (var analyzer in analyzers)
                {
                    if (analyzer.SupportedDiagnostics.Length == 0)
                    {
                        continue;
                    }

                    var analyzerType = analyzer.GetType();
                    var isAbstractGlobalizationDiagnosticAnalyzer = IsSubClassOfGlobalizationAnalyzer(analyzerType);
                    if (analyzer.SupportedDiagnostics.All(d => d.Category == "Globalization"))
                    {
                        Debug.Assert(isAbstractGlobalizationDiagnosticAnalyzer, $"Analyzer {analyzerType.Name} was expected to inherit AbstractGlobalizationDiagnosticAnalyzer.");
                    }
                    else
                    {
                        // Note: If an analyzer have one Globalization rule and other non-Globalization rules, it shouldn't inherit AbstractGlobalizationDiagnosticAnalyzer.
                        // Instead, it should check for InvariantCulture MSBuild property for the Globalization rules only.
                        Debug.Assert(!isAbstractGlobalizationDiagnosticAnalyzer, $"Analyzer {analyzerType.Name} wasn't expected to inherit AbstractGlobalizationDiagnosticAnalyzer.");
                    }
                }
            }

            static void AnalyzerFileReference_AnalyzerLoadFailed(object sender, AnalyzerLoadFailureEventArgs e)
            => throw e.Exception ?? new NotSupportedException(e.Message);
        }

        private static bool IsSubClassOfGlobalizationAnalyzer(Type analyzerType)
        {
            var baseType = analyzerType.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "AbstractGlobalizationDiagnosticAnalyzer")
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}

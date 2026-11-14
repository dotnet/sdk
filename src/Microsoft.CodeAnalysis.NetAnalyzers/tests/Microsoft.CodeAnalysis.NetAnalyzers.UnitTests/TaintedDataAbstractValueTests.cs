// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class TaintedDataAbstractValueTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void BinaryMergeReusesUnchangedOrigins()
        {
            var values = CreateValues();
            Assert.AreSame(values[0], TaintedDataAbstractValue.MergeTainted(values[0], values[0]));
            var merged = TaintedDataAbstractValue.MergeTainted(values[0], values[1]);
            Assert.HasCount(2, merged.SourceOrigins);
            Assert.AreSame(merged, TaintedDataAbstractValue.MergeTainted(merged, values[0]));
            Assert.AreSame(merged, TaintedDataAbstractValue.MergeTainted(merged, values[1]));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(8)]
        public void BulkMergeReusesRepeatedValue(int count)
        {
            var value = CreateValues()[0];
            Assert.AreSame(value, TaintedDataAbstractValue.MergeTainted(Enumerable.Repeat(value, count)));
        }

        [TestMethod]
        public void BulkMergePreservesAllOrigins()
        {
            var values = CreateValues();
            var merged = TaintedDataAbstractValue.MergeTainted(values);
            Assert.HasCount(3, merged.SourceOrigins);
            Assert.IsTrue(merged.SourceOrigins.SetEquals(values.SelectMany(value => value.SourceOrigins)));
            Assert.AreSame(merged, TaintedDataAbstractValue.MergeTainted(new[] { merged, values[0], values[1] }));
        }

        [TestMethod]
        public void EmptyBulkMergePreservesTaintedEmptyValue()
        {
            var merged = TaintedDataAbstractValue.MergeTainted(Enumerable.Empty<TaintedDataAbstractValue>());
            Assert.AreEqual(TaintedDataAbstractValueKind.Tainted, merged.Kind);
            Assert.IsEmpty(merged.SourceOrigins);
        }

        [TestMethod]
        public void BulkMergeEnumeratesInputsOnce()
        {
            var values = CreateValues();
            var count = 0;
            IEnumerable<TaintedDataAbstractValue> Enumerate()
            {
                foreach (var value in values)
                {
                    count++;
                    yield return value;
                }
            }

            var merged = TaintedDataAbstractValue.MergeTainted(Enumerate());
            Assert.AreEqual(3, count);
            Assert.HasCount(3, merged.SourceOrigins);
        }

        private TaintedDataAbstractValue[] CreateValues()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class C
                {
                    void M(int value)
                    {
                        var first = value;
                        var second = value;
                        var third = value;
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("TaintValueTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var root = tree.GetRoot(TestContext.CancellationToken);
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var symbol = (IMethodSymbol)compilation.GetSemanticModel(tree).GetDeclaredSymbol(method, TestContext.CancellationToken);
            return root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(name => name.Identifier.ValueText == "value")
                .Select(syntax => TaintedDataAbstractValue.CreateTainted(symbol.Parameters[0], syntax, symbol)).ToArray();
        }
    }
}

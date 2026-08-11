// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    [TestClass]
    public class ExperimentalApiTests
    {
        private const string ExperimentalAttribute = "[System.Diagnostics.CodeAnalysis.Experimental(\"TEST001\")]";

        [TestMethod]
        public void RemovedExperimentalTypeAndMemberAreInformational()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class RemovedType { }
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Removed() { }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.IsNotEmpty(differences);
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Informational));
        }

        [TestMethod]
        public void NewExperimentalTypeAndMemberAreInformationalInStrictMode()
        {
            string leftSyntax = "namespace CompatTests; public class Api { }";
            string rightSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class AddedType { }
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Added() { }
                }
                """;

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax, strictMode: true);

            Assert.IsNotEmpty(differences);
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Informational));
        }

        [TestMethod]
        public void NewStableTypeAndMemberRemainErrorsInStrictMode()
        {
            string leftSyntax = "namespace CompatTests; public class Api { }";
            string rightSyntax = "namespace CompatTests; public class AddedType { } public class Api { public void Added() { } }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax, strictMode: true);

            Assert.IsNotEmpty(differences);
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Error));
        }

        [TestMethod]
        public void BreakingChangesRemainInformationalWhileApiRemainsExperimental()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class Api { public string Changed() => string.Empty; }
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class Api { public int Changed() => 0; }
                """;

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.IsNotEmpty(differences);
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Informational));
        }

        [TestMethod]
        public void ExperimentalTypeAndMemberPromotionIsReportedAsError()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class ExperimentalType { }
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void ExperimentalMember() { }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class ExperimentalType { } public class Api { public void ExperimentalMember() { } }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.HasCount(1, differences.Where(difference => difference.DiagnosticId == DiagnosticIds.ExperimentalApiBecomesStable && difference.ReferenceId == "T:CompatTests.ExperimentalType"));
            Assert.HasCount(1, differences.Where(difference => difference.DiagnosticId == DiagnosticIds.ExperimentalApiBecomesStable && difference.ReferenceId == "M:CompatTests.Api.ExperimentalMember"));
            Assert.IsTrue(differences.Where(difference => difference.DiagnosticId == DiagnosticIds.ExperimentalApiBecomesStable)
                .All(difference => difference.Severity == DifferenceSeverity.Error));
        }

        [TestMethod]
        public void MemberInExperimentalContainingTypeUsesContainingTypeStability()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class Api { public void Changed() { } }
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class Api { }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Changed"));
            Assert.AreEqual(DifferenceSeverity.Informational, difference.Severity);
        }

        [TestMethod]
        [DataRow("assembly")]
        [DataRow("module")]
        public void MemberInExperimentalCompilationScopeIsInformational(string attributeTarget)
        {
            string leftSyntax = $$"""
                [{{attributeTarget}}: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                namespace CompatTests;
                public class Api { public void Changed() { } }
                """;
            string rightSyntax = $$"""
                [{{attributeTarget}}: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                namespace CompatTests;
                public class Api { }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Changed"));
            Assert.AreEqual(DifferenceSeverity.Informational, difference.Severity);
        }

        [TestMethod]
        [DataRow("assembly")]
        [DataRow("module")]
        public void RemovingExperimentalCompilationScopeReportsPromotion(string attributeTarget)
        {
            string leftSyntax = $$"""
                [{{attributeTarget}}: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                namespace CompatTests;
                public class Api { public void Changed() { } }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Changed"));
            Assert.AreEqual(DiagnosticIds.ExperimentalApiBecomesStable, difference.DiagnosticId);
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void StableToExperimentalRemainsGenericAttributeError()
        {
            string leftSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";
            string rightSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Changed() { }
                }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax, strictMode: true, includeAttributesRule: true)
                .Where(difference => difference.DiagnosticId == DiagnosticIds.CannotAddAttribute));
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void PromotionDoesNotDuplicateGenericAttributeDiagnostic()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Changed() { }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax, includeAttributesRule: true);

            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.Changed"));
            Assert.IsTrue(differences.All(difference => difference.DiagnosticId == DiagnosticIds.ExperimentalApiBecomesStable));
        }

        [TestMethod]
        public void AttributesRuleAloneReportsPromotion()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Changed() { }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";
            TestRuleFactory ruleFactory = new((settings, context) => new AttributesMustMatch(settings, context));
            ApiComparer comparer = new(ruleFactory);

            CompatDifference difference = Assert.ContainsSingle(comparer.GetDifferences(
                SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                SymbolFactory.GetAssemblyFromSyntax(rightSyntax)));

            Assert.AreEqual(DiagnosticIds.ExperimentalApiBecomesStable, difference.DiagnosticId);
            Assert.AreEqual("M:CompatTests.Api.Changed", difference.ReferenceId);
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void ExperimentalAssemblyIdentityDifferenceIsInformational()
        {
            const string leftSyntax = """
                [assembly: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                [assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
                """;
            const string rightSyntax = """
                [assembly: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
                """;

            CompatDifference difference = Assert.ContainsSingle(GetAssemblyIdentityDifferences(leftSyntax, rightSyntax));

            Assert.AreEqual(DifferenceSeverity.Informational, difference.Severity);
        }

        [TestMethod]
        public void StableAssemblyIdentityDifferenceRemainsError()
        {
            const string leftSyntax = "[assembly: System.Reflection.AssemblyVersion(\"2.0.0.0\")]";
            const string rightSyntax = "[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")]";

            CompatDifference difference = Assert.ContainsSingle(GetAssemblyIdentityDifferences(leftSyntax, rightSyntax));

            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void RemovedExperimentalAssemblyIsInformational()
        {
            const string leftSyntax = "[assembly: System.Diagnostics.CodeAnalysis.Experimental(\"TEST001\")]";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            TestRuleFactory ruleFactory = new((settings, context) => new AssemblyIdentityMustMatch(new SuppressibleTestLog(), settings, context));
            ApiComparer comparer = new(ruleFactory);

            CompatDifference difference = Assert.ContainsSingle(comparer.GetDifferences([left], Array.Empty<IAssemblySymbol>()));

            Assert.AreEqual(DifferenceSeverity.Informational, difference.Severity);
        }

        private static CompatDifference[] GetAssemblyIdentityDifferences(string leftSyntax, string rightSyntax)
        {
            TestRuleFactory ruleFactory = new((settings, context) => new AssemblyIdentityMustMatch(new SuppressibleTestLog(), settings, context));
            ApiComparer comparer = new(ruleFactory);

            return comparer.GetDifferences(
                SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                SymbolFactory.GetAssemblyFromSyntax(rightSyntax)).ToArray();
        }

        private static CompatDifference[] GetDifferences(string leftSyntax, string rightSyntax, bool strictMode = false, bool includeAttributesRule = false)
        {
            TestRuleFactory ruleFactory = new(
                (settings, context) => new MembersMustExist(settings, context),
                (settings, context) => new ExperimentalApiBecomesStable(context));

            if (includeAttributesRule)
            {
                ruleFactory = ruleFactory.WithRule((settings, context) => new AttributesMustMatch(settings, context));
            }

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer comparer = new(ruleFactory, new ApiComparerSettings(strictMode: strictMode));

            return comparer.GetDifferences(left, right).ToArray();
        }
    }
}

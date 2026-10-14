// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
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
        public void RemovedExperimentalPropertyIsInformational()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public int Removed { get; set; }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.HasCount(2, differences);
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.get_Removed"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.set_Removed(System.Int32)"));
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Informational));
        }

        [TestMethod]
        public void MutatedExperimentalPropertyIsInformational()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public int Changed { get; set; }
                }
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public int Changed { get; }
                }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax));

            Assert.AreEqual("M:CompatTests.Api.set_Changed(System.Int32)", difference.ReferenceId);
            Assert.AreEqual(DifferenceSeverity.Informational, difference.Severity);
        }

        [TestMethod]
        public void RemovedExperimentalEventIsInformational()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public delegate void Handler();
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public event Handler Removed;
                }
                """;
            string rightSyntax = "namespace CompatTests; public delegate void Handler(); public class Api { }";

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.HasCount(2, differences);
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.add_Removed(CompatTests.Handler)"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.remove_Removed(CompatTests.Handler)"));
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Informational));
        }

        [TestMethod]
        public void MutatedExperimentalEventIsInformational()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public delegate void Handler();
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public virtual event Handler Changed;
                }
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                public delegate void Handler();
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public event Handler Changed;
                }
                """;

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax, includeVirtualRule: true);

            Assert.HasCount(3, differences);
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.add_Changed(CompatTests.Handler)"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.remove_Changed(CompatTests.Handler)"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "E:CompatTests.Api.Changed"));
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
        public void ExcludedExperimentalAttributeDoesNotChangeDifferenceSeverity()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Removed() { }
                }
                """;
            string rightSyntax = "namespace CompatTests; public class Api { }";

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(
                leftSyntax,
                rightSyntax,
                attributeDataSymbolFilter: CreateExperimentalAttributeExclusionFilter()));

            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ExcludedExperimentalAttributeDoesNotReportStabilityTransition(bool experimentalOnLeft)
        {
            string experimentalSyntax = $$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Changed() { }
                }
                """;
            const string stableSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";

            CompatDifference[] differences = GetDifferences(
                experimentalOnLeft ? experimentalSyntax : stableSyntax,
                experimentalOnLeft ? stableSyntax : experimentalSyntax,
                includeAttributesRule: true,
                attributeDataSymbolFilter: CreateExperimentalAttributeExclusionFilter());

            Assert.IsEmpty(differences);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void UserDefinedExperimentalAttributeRequiresStringConstructor(bool hasStringConstructor)
        {
            string constructorParameterType = hasStringConstructor ? "string" : "int";
            string constructorArgument = hasStringConstructor ? "\"TEST001\"" : "1";
            string attributeDefinition = $$"""
                #pragma warning disable CS0436
                namespace System.Diagnostics.CodeAnalysis
                {
                    [global::System.AttributeUsage(global::System.AttributeTargets.All)]
                    public sealed class ExperimentalAttribute : global::System.Attribute
                    {
                        public ExperimentalAttribute({{constructorParameterType}} diagnosticId) { }
                    }
                }
                """;
            string leftSyntax = $$"""
                {{attributeDefinition}}
                namespace CompatTests
                {
                    public class Api
                    {
                        [System.Diagnostics.CodeAnalysis.Experimental({{constructorArgument}})]
                        public void Removed() { }
                    }
                }
                """;
            string rightSyntax = $$"""
                {{attributeDefinition}}
                namespace CompatTests
                {
                    public class Api { }
                }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Removed"));

            Assert.AreEqual(
                hasStringConstructor ? DifferenceSeverity.Informational : DifferenceSeverity.Error,
                difference.Severity);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void FrameworkExperimentalAttributeWithoutAssemblyReferencesHonorsFilter(bool excludeExperimentalAttribute)
        {
            string leftAssemblyPath = SymbolFactory.EmitAssemblyFromSyntax($$"""
                namespace CompatTests;
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public void Experimental() { }
                }
                """, assemblyName: "ExperimentalAttributeMetadataLeft");
            string rightAssemblyPath = SymbolFactory.EmitAssemblyFromSyntax(
                "namespace CompatTests; public class Api { }",
                assemblyName: "ExperimentalAttributeMetadataRight");

            try
            {
                IAssemblySymbol left = new AssemblySymbolLoader(new SuppressibleTestLog()).LoadAssembly(leftAssemblyPath)
                    ?? throw new InvalidOperationException($"Failed to load '{leftAssemblyPath}'.");
                IAssemblySymbol right = new AssemblySymbolLoader(new SuppressibleTestLog()).LoadAssembly(rightAssemblyPath)
                    ?? throw new InvalidOperationException($"Failed to load '{rightAssemblyPath}'.");
                TestRuleFactory ruleFactory = new((settings, context) => new MembersMustExist(settings, context));
                ApiComparer comparer = new(ruleFactory);
                ISymbolFilter filter = SymbolFilterFactory.GetFilterFromList(
                    excludeExperimentalAttribute ? ["T:System.Diagnostics.CodeAnalysis.ExperimentalAttribute"] : null);
                comparer.Settings.AttributeDataSymbolFilter = filter;

                CompatDifference difference = Assert.ContainsSingle(comparer.GetDifferences(left, right)
                    .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Experimental"));

                Assert.AreEqual(
                    excludeExperimentalAttribute ? DifferenceSeverity.Error : DifferenceSeverity.Informational,
                    difference.Severity);
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(leftAssemblyPath)!, recursive: true);
                Directory.Delete(Path.GetDirectoryName(rightAssemblyPath)!, recursive: true);
            }
        }

        [TestMethod]
        public void BreakingChangesToMemberInExperimentalTypeRemainErrors()
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
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Error));
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
        public void ExperimentalContainingTypeDoesNotMakeMemberExperimental()
        {
            string leftSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}} public class ExperimentalBase { public void Changed() { } }
                #pragma warning disable TEST001
                public class StableDerived : ExperimentalBase { }
                #pragma warning restore TEST001
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}} public class ExperimentalBase { }
                #pragma warning disable TEST001
                public class StableDerived : ExperimentalBase { }
                #pragma warning restore TEST001
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.ExperimentalBase.Changed"));
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
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
        [DataRow(false)]
        [DataRow(true)]
        public void StableToExperimentalIsReportedByDefault(bool includeAttributesRule)
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

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax, includeAttributesRule: includeAttributesRule));

            Assert.AreEqual(DiagnosticIds.StableApiBecomesExperimental, difference.DiagnosticId);
            Assert.AreEqual("M:CompatTests.Api.Changed", difference.ReferenceId);
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void StableTypeToExperimentalIsReportedByDefault()
        {
            string leftSyntax = "namespace CompatTests; public class Api { }";
            string rightSyntax = $$"""
                namespace CompatTests;
                {{ExperimentalAttribute}}
                public class Api { }
                """;

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.HasCount(1, differences);
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "T:CompatTests.Api"));
            Assert.IsTrue(differences.All(difference => difference.DiagnosticId == DiagnosticIds.StableApiBecomesExperimental));
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Error));
        }

        [TestMethod]
        public void StablePropertyAndEventToExperimentalReportAccessorsByDefault()
        {
            const string leftSyntax = """
                namespace CompatTests;
                public delegate void Handler();
                public class Api
                {
                    public int Property { get; set; }
                    public event Handler Event;
                }
                """;
            string rightSyntax = $$"""
                namespace CompatTests;
                public delegate void Handler();
                public class Api
                {
                    {{ExperimentalAttribute}}
                    public int Property { get; set; }
                    {{ExperimentalAttribute}}
                    public event Handler Event;
                }
                """;

            CompatDifference[] differences = GetDifferences(leftSyntax, rightSyntax);

            Assert.HasCount(6, differences);
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "P:CompatTests.Api.Property"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.get_Property"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.set_Property(System.Int32)"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "E:CompatTests.Api.Event"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.add_Event(CompatTests.Handler)"));
            Assert.ContainsSingle(differences.Where(difference => difference.ReferenceId == "M:CompatTests.Api.remove_Event(CompatTests.Handler)"));
            Assert.IsTrue(differences.All(difference => difference.DiagnosticId == DiagnosticIds.StableApiBecomesExperimental));
            Assert.IsTrue(differences.All(difference => difference.Severity == DifferenceSeverity.Error));
        }

        [TestMethod]
        [DataRow("assembly")]
        [DataRow("module")]
        public void AddingExperimentalCompilationScopeIsReportedByDefault(string attributeTarget)
        {
            string leftSyntax = "namespace CompatTests; public class Api { public void Changed() { } }";
            string rightSyntax = $$"""
                [{{attributeTarget}}: System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
                namespace CompatTests;
                public class Api { public void Changed() { } }
                """;

            CompatDifference difference = Assert.ContainsSingle(GetDifferences(leftSyntax, rightSyntax)
                .Where(difference => difference.ReferenceId == "M:CompatTests.Api.Changed"));

            Assert.AreEqual(DiagnosticIds.StableApiBecomesExperimental, difference.DiagnosticId);
            Assert.AreEqual(DifferenceSeverity.Error, difference.Severity);
        }

        [TestMethod]
        public void AttributesRuleAloneReportsStableToExperimental()
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
            TestRuleFactory ruleFactory = new((settings, context) => new AttributesMustMatch(settings, context));
            ApiComparer comparer = new(ruleFactory);

            CompatDifference difference = Assert.ContainsSingle(comparer.GetDifferences(
                SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                SymbolFactory.GetAssemblyFromSyntax(rightSyntax)));

            Assert.AreEqual(DiagnosticIds.StableApiBecomesExperimental, difference.DiagnosticId);
            Assert.AreEqual("M:CompatTests.Api.Changed", difference.ReferenceId);
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

        private static CompatDifference[] GetDifferences(
            string leftSyntax,
            string rightSyntax,
            bool strictMode = false,
            bool includeAttributesRule = false,
            bool includeVirtualRule = false,
            ISymbolFilter? attributeDataSymbolFilter = null)
        {
            TestRuleFactory ruleFactory = new(
                (settings, context) => new MembersMustExist(settings, context),
                (settings, context) => new ExperimentalApiBecomesStable(settings, context));

            if (includeAttributesRule)
            {
                ruleFactory = ruleFactory.WithRule((settings, context) => new AttributesMustMatch(settings, context));
            }

            if (includeVirtualRule)
            {
                ruleFactory = ruleFactory.WithRule((settings, context) => new CannotAddOrRemoveVirtualKeyword(settings, context));
            }

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer comparer = new(ruleFactory, new ApiComparerSettings(strictMode: strictMode));
            if (attributeDataSymbolFilter is not null)
            {
                comparer.Settings.AttributeDataSymbolFilter = attributeDataSymbolFilter;
            }

            return comparer.GetDifferences(left, right).ToArray();
        }

        private static ISymbolFilter CreateExperimentalAttributeExclusionFilter() =>
            DocIdSymbolFilter.CreateFromLists(["T:System.Diagnostics.CodeAnalysis.ExperimentalAttribute"]);
    }
}

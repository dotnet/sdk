// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotChangeGenericConstraintsTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotChangeGenericConstraints(settings, context));

        private const string ClassesWithConstraints = """
            namespace CompatTests
            {
              public class First<T> where T : new() {}
              public class Second<T> where T : notnull {}
              public class Third<T> where T : class {}
              public class Fourth<T> where T : struct {}
              public class Fifth<T> where T : unmanaged {}
              public class Sixth<T> where T : struct, System.Enum {}
              public class Seventh<T> where T : System.IO.Stream, System.IDisposable, System.Runtime.Serialization.ISerializable {}
            }
            """;
        private static readonly string SealedClassesWithConstraints = ClassesWithConstraints.Replace("public class", "public sealed class");
        private const string ClassesWithoutConstraints = """
            namespace CompatTests
            {
              public class First<T> {}
              public class Second<T> {}
              public class Third<T> {}
              public class Fourth<T> {}
              public class Fifth<T> {}
              public class Sixth<T> where T : struct {}
              public class Seventh<T> where T : System.IO.Stream, System.IDisposable {}
            }
            """;
        public static readonly string SealedClassesWithoutConstraints = ClassesWithoutConstraints.Replace("public class", "public sealed class");

        private static readonly CompatDifference[] RemovedClassConstraintDifferences =
        {
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.First`1``0:new()"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Second`1``0:notnull"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Third`1``0:class"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Fourth`1``0:struct"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Fifth`1``0:unmanaged"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Sixth`1``0:T:System.Enum"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "T:CompatTests.Seventh`1``0:T:System.Runtime.Serialization.ISerializable")
        };
        private static readonly CompatDifference[] AddedClassConstraintDifferences = RemovedClassConstraintDifferences
            .Select(d => CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Added, d.ReferenceId)).ToArray();

        private const string MethodsWithConstraints = """
            namespace CompatTests
            {
              public class C
              {
                public void First<T>() where T : new() {}
                public void Second<T>() where T : notnull {}
                public void Third<T>() where T : class {}
                public void Fourth<T>() where T : struct {}
                public void Fifth<T>() where T : unmanaged {}
                public void Sixth<T>() where T : struct, System.Enum {}
                public void Seventh<T>() where T : System.IO.Stream, System.IDisposable, System.Runtime.Serialization.ISerializable {}
              }
            }
            """;
        private static readonly string VirtualMethodsWithConstraints = MethodsWithConstraints.Replace("void", "virtual void");
        private const string MethodsWithoutConstraints = """
            namespace CompatTests
            {
              public class C
              {
                public void First<T>() {}
                public void Second<T>() {}
                public void Third<T>() {}
                public void Fourth<T>() {}
                public void Fifth<T>() {}
                public void Sixth<T>() where T : struct {}
                public void Seventh<T>() where T : System.IO.Stream, System.IDisposable {}
              }
            }
            """;
        private static readonly string VirtualMethodsWithoutConstraints = MethodsWithoutConstraints.Replace("void", "virtual void");
        private static readonly CompatDifference[] RemovedMethodConstraintDifferences =
        {
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.First``1``0:new()"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Second``1``0:notnull"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Third``1``0:class"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Fourth``1``0:struct"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Fifth``1``0:unmanaged"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Sixth``1``0:T:System.Enum"),
            CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Removed, "M:CompatTests.C.Seventh``1``0:T:System.Runtime.Serialization.ISerializable")
        };
        private static readonly CompatDifference[] AddedMethodConstraintDifferences = RemovedMethodConstraintDifferences
            .Select(d => CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeGenericConstraint, string.Empty, DifferenceType.Added, d.ReferenceId)).ToArray();


        public static TheoryData<string, string, CompatDifference[]> TestCases => new()
        {
            // removing constraints from class
            {
                ClassesWithConstraints,
                ClassesWithoutConstraints,
                RemovedClassConstraintDifferences
            },

            // removing constraints virtual method
            {
                VirtualMethodsWithConstraints,
                VirtualMethodsWithoutConstraints,
                RemovedMethodConstraintDifferences
            },
            
            // removing constraints sealed class
            {
                SealedClassesWithConstraints,
                SealedClassesWithoutConstraints,
                Array.Empty<CompatDifference>()
            },

            // removing constraints non-virtual method
            {
                MethodsWithConstraints,
                MethodsWithoutConstraints,
                Array.Empty<CompatDifference>()
            },

            // adding constraints to class          
            {
                ClassesWithoutConstraints,
                ClassesWithConstraints,
                AddedClassConstraintDifferences
            },

            // adding constraint virtual method
            {
                VirtualMethodsWithoutConstraints,
                VirtualMethodsWithConstraints,
                AddedMethodConstraintDifferences
            },

            // adding constraint to sealed class           
            {
                SealedClassesWithoutConstraints,
                SealedClassesWithConstraints,
                AddedClassConstraintDifferences
            },

            // adding constraint non-virtual method
            {
                MethodsWithoutConstraints,
                MethodsWithConstraints,
                AddedMethodConstraintDifferences
            },

            // reordering constraints
            {
                """
                namespace CompatTests
                {
                  public class C
                  {
                    public void First<T>() where T : System.IO.Stream, System.Runtime.Serialization.ISerializable, System.IDisposable {}
                  }
                }
                """,
                """
                namespace CompatTests
                {
                  public class C
                  {
                    public void First<T>() where T : System.IO.Stream, System.IDisposable, System.Runtime.Serialization.ISerializable {}
                  }
                }
                """,
                Array.Empty<CompatDifference>()
            },

            // renaming parameters
            {
                MethodsWithConstraints,
                MethodsWithConstraints.Replace("where T", "where TVal").Replace("<T>", "<TVal>"),
                Array.Empty<CompatDifference>()
            },
            {
                ClassesWithConstraints,
                ClassesWithConstraints.Replace("where T", "where TVal").Replace("<T>", "<TVal>"),
                Array.Empty<CompatDifference>()
            }
        };

        public static TheoryData<string, string, CompatDifference[]> StrictMode => new()
        {
            // in strict mode we don't allow removals even on sealed classes / virtual methods
            
            // removing constraints sealed class
            {
                SealedClassesWithConstraints,
                SealedClassesWithoutConstraints,
                RemovedClassConstraintDifferences
            },

            // removing constraints non-virtual method
            {
                MethodsWithConstraints,
                MethodsWithoutConstraints,
                RemovedMethodConstraintDifferences
            },
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: true));

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(StrictMode))]
        public void EnsureDiagnosticIsReportedInStrictMode(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(
                includeInternalSymbols: true,
                strictMode: true));

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }
    }
}

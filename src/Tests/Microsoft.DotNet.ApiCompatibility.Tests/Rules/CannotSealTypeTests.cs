// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CannotSealTypeTests
    {
        [Theory]
        [MemberData(nameof(SealNonInheritableTypeNotReportedData))]
        public void SealNonInheritableTypeNotReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Theory]
        [MemberData(nameof(SealInheritableTypeReportedData))]
        public void SealInheritableTypeReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.UnsealedClass")
            };

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SealInheritableTypeInternalsVisibleReported(bool includeInternals)
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class UnsealedClass
    {
        internal UnsealedClass() { }
    }
}
";
            string rightSyntax = @"
namespace CompatTests
{
    public sealed class UnsealedClass
    {
      public UnsealedClass() { }
    }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (!includeInternals)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected = new[]
                {
                     new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.UnsealedClass")
                };

                Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
            }

        }

        [Fact]
        public void MultipleRightsAreReportedCorrectly()
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class UnsealedClass
    {
    }
}
";

            string[] rightSyntaxes = new[]
            {
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
    }
}",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        protected UnsealedClass() { }
    }
}",
                @"
namespace CompatTests
{
    public sealed class UnsealedClass
    {
    }
}",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        private UnsealedClass() { }
    }
}",
            };

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            MetadataInformation leftMetadata = new("left", "net6.0", "ref/a.dll");
            ElementContainer<IAssemblySymbol> leftContainer = new(left, leftMetadata);

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> result =
                differ.GetDifferences(leftContainer, right);

            CompatDifference[][] expectedDiffs =
            {
                Array.Empty<CompatDifference>(),
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.UnsealedClass"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.UnsealedClass"),
                },
            };

            AssertExtensions.MultiRightResult(leftMetadata, expectedDiffs, result);
        }

        [Fact]
        public void MultipleRightsNoDifferences()
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class UnsealedClass
    {
    }
}
";

            string[] rightSyntaxes = new[]
            {
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
    }
}",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        protected UnsealedClass() { }
    }
}",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        internal UnsealedClass() { }
    }
}"
            };

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            MetadataInformation leftMetadata = new("left", "net6.0", "ref/a.dll");
            ElementContainer<IAssemblySymbol> leftContainer = new(left, leftMetadata);

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = true;
            IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> result =
                differ.GetDifferences(leftContainer, right);

            AssertExtensions.MultiRightEmptyDifferences(leftMetadata, 3, result);
        }

        [Theory]
        [MemberData(nameof(StrictModeSealedLeftIsReportedData))]
        public void StrictModeSealedLeftIsReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.StrictMode = true;
            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.UnsealedClass")
            };

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
            Assert.True(differences[0].Message.IndexOf("left") < differences[0].Message.IndexOf("right"));
        }

        public static IEnumerable<object[]> StrictModeSealedLeftIsReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public sealed class UnsealedClass
    {
        public UnsealedClass() { }
    }
}
",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
      public UnsealedClass() { }
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        private UnsealedClass() { }
    }
}
",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        public UnsealedClass() { }
    }
}
"
            };
        }

        public static IEnumerable<object[]> SealInheritableTypeReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
    }
}
",
                @"
namespace CompatTests
{
    public sealed class UnsealedClass
    {
      public UnsealedClass() { }
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        public UnsealedClass() { }
    }
}
",
                @"
namespace CompatTests
{
    public class UnsealedClass
    {
        private UnsealedClass() { }
    }
}
"
            };
        }

        public static IEnumerable<object[]> SealNonInheritableTypeNotReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public class SealedClass
    {
      private SealedClass() { } 
    }
}
",
                @"
namespace CompatTests
{
    public sealed class SealedClass
    {
      public SealedClass() { } 
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public sealed class SealedClass
    {
    }
}
",
                @"
namespace CompatTests
{
    public class SealedClass
    {
      private SealedClass() { } 
    }
}
"
            };
        }
    }
}

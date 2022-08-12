﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CustomSideNameTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

        [Fact]
        public void CustomSideNameAreNotSpecified()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            string expectedLeftName = "left";
            string expectedRightName = "right";
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), expectedLeftName, expectedRightName);
        }

        [Fact]
        public void CustomSideNamesAreUsed()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First
  {
    public string Method1() => string.Empty;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            bool enableNullable = false;
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable),
                new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable),
                new MetadataInformation("a.dll", "lib/net6.0/a.dll"));
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString);

            // Use the single assembly override
            differences = differ.GetDifferences(left, right);
            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString);
        }

        [Fact]
        public void CustomSideNamesAreUsedStrictMode()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Method1() => string.Empty;
  }
}
";

            bool enableNullable = false;
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable),
                new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable),
                new MetadataInformation("a.dll", "lib/net6.0/a.dll"));
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString, leftFirst: false);
        }

        [Fact]
        public void MultipleRightsMetadataInformationIsUsedAsName()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            foreach ((MetadataInformation leftMetadata, MetadataInformation rightMetadata, IEnumerable<CompatDifference> differences) in differ.GetDifferences(left, right))
            {
                Assert.Single(differences);
                AssertNames(differences.First(), leftMetadata.AssemblyId, rightMetadata.AssemblyId);
            }
        }

        private void AssertNames(CompatDifference difference, string expectedLeftName, string expectedRightName, bool leftFirst = true)
        {
            string message = difference.Message;

            // make sure it is separater by a space and it is not a substr of a word.
            string left = " " + expectedLeftName;
            string right = " " + expectedRightName; 
            if (leftFirst)
            {
                Assert.Contains(left + " ", message);
                Assert.EndsWith(right, message);
            }
            else
            {
                Assert.Contains(right + " ", message);
                Assert.EndsWith(left, message);
            }
        }
    }
}

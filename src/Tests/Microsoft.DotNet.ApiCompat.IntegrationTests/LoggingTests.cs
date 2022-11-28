// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ApiCompat.IntegrationTests
{
    public class LoggingTests : SdkTest
    {
        public LoggingTests(ITestOutputHelper log) : base(log)
        {
        }

        private (TestLogger, CompatibleFrameworkInPackageValidator) CreateLoggerAndValidator()
        {
            TestLogger log = new();
            CompatibleFrameworkInPackageValidator validator = new(log,
                new ApiCompatRunner(log,
                    new SuppressionEngine(),
                    new ApiComparerFactory(new RuleFactory(log)),
                    new AssemblySymbolLoaderFactory()));

            return (log, validator);
        }

        [Fact]
        public void LogWhenBreakingChanges()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
  public class Third { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);


        }

        [Fact]
        public void LogWhenNoBreakingChanges()
        {

        }
    }
}

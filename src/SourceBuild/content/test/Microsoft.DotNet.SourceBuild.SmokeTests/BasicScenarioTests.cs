// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// Basic project create, build, run, publish scenario tests.
/// <see cref="WebScenarioTests"/> for related web scenarios.
/// They are encapsulated in a separate testclass so that they can be run in parallel.
/// </summary>
public class BasicScenarioTests : SmokeTests
{
    public BasicScenarioTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [Theory]
    [MemberData(nameof(GetScenarioObjects))]
    public void VerifyScenario(TestScenario scenario) => scenario.Execute(DotNetHelper);

    private static IEnumerable<object[]> GetScenarioObjects() => GetScenarios().Select(scenario => new object[] { scenario });

    private static IEnumerable<TestScenario> GetScenarios()
    {
        foreach (DotNetLanguage language in Enum.GetValues<DotNetLanguage>())
        {
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.Console,  DotNetActions.Build | DotNetActions.Run | DotNetActions.PublishComplex | DotNetActions.PublishR2R);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.ClassLib, DotNetActions.Build | DotNetActions.Publish);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.XUnit,    DotNetActions.Test);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.NUnit,    DotNetActions.Test);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.MSTest,   DotNetActions.Test);
        }
    }
}

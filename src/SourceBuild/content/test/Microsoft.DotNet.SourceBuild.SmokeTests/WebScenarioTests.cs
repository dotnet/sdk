// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// Web project create, build, run, publish scenario tests.
/// <see cref="BaseScenarioTests"/> for related basic scenarios.
/// They are encapsulated in a separate testclass so that they can be run in parallel.
/// </summary>
public class WebScenarioTests : SmokeTests
{
    public WebScenarioTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [Theory]
    [MemberData(nameof(GetScenarioObjects))]
    public void VerifyScenario(TestScenario scenario) => scenario.Execute(DotNetHelper);

    private static IEnumerable<object[]> GetScenarioObjects() => GetScenarios().Select(scenario => new object[] { scenario });

    private static IEnumerable<TestScenario> GetScenarios()
    {
        foreach (DotNetLanguage language in new[] { DotNetLanguage.CSharp, DotNetLanguage.FSharp })
        {
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.Web,    DotNetActions.Build | DotNetActions.Run | DotNetActions.PublishComplex);
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.Mvc,    DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish) { NoHttps = true };
            yield return new(nameof(WebScenarioTests), language, DotNetTemplate.WebApi, DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        }

        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.Razor,         DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.BlazorWasm,    DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.BlazorServer,  DotNetActions.Build | DotNetActions.Run | DotNetActions.Publish);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.Worker);
        yield return new(nameof(WebScenarioTests), DotNetLanguage.CSharp, DotNetTemplate.Angular);
    }
}

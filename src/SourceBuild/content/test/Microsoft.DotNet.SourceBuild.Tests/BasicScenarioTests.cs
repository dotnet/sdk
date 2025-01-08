// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

/// <summary>
/// Basic project create, build, run, publish scenario tests.
/// <see cref="WebScenarioTests"/> for related web scenarios.
/// They are encapsulated in a separate testclass so that they can be run in parallel.
/// </summary>
public class BasicScenarioTests : SdkTests
{
    public BasicScenarioTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    // [Theory(Skip="https://github.com/dotnet/sdk/issues/42920")]
    [MemberData(nameof(GetScenarioObjects))]
    public void VerifyScenario(TestScenario scenario) => scenario.Execute(DotNetHelper);

    public static IEnumerable<object[]> GetScenarioObjects() => GetScenarios().Select(scenario => new object[] { scenario });

    public static IEnumerable<TestScenario> GetScenarios()
    {
        // Since this has to be a static method, we don't have access to XUnit's output helper. So we use our own version as a placeholder.
        DotNetHelper helper = new(new DebugTestOutputHelper());

        foreach (DotNetLanguage language in Enum.GetValues<DotNetLanguage>())
        {
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.Console,
                // R2R is not supported on Mono (see https://github.com/dotnet/runtime/issues/88419#issuecomment-1623762676)
                DotNetActions.Build | DotNetActions.Run | (DotNetHelper.ShouldPublishComplex() ? DotNetActions.PublishComplex : DotNetActions.None) | (helper.IsMonoRuntime ? DotNetActions.None : DotNetActions.PublishR2R));
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.ClassLib, DotNetActions.Build | DotNetActions.Publish);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.XUnit,    DotNetActions.Test);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.NUnit,    DotNetActions.Test);
            yield return new(nameof(BasicScenarioTests), language, DotNetTemplate.MSTest,   DotNetActions.Test);
        }
    }
}

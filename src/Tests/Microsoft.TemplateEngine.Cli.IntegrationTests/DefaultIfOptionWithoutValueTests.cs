// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class DefaultIfOptionWithoutValueTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(DefaultIfOptionWithoutValueTest))]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue", "NoValueDefaultsNotUsedIfSwitchesNotSpecified.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyChoice", "NoValueDefaultForChoiceParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString", "NoValueDefaultForStringParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString UserString --MyChoice OtherChoice", "NoValueDefault_UserProvidedValuesAreIsUsed.json")]
        public void DefaultIfOptionWithoutValueTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.NET.Sdk.Publish.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests
{
    public class EnvironmentHelperTests
    {
        private const string TelemetryOptout = "DOTNET_CLI_TELEMETRY_OPTOUT";

        [Theory]
        [InlineData("true", true)]
        [InlineData("1", true)]
        [InlineData("yes", true)]
        [InlineData("false", false)]
        [InlineData("0", false)]
        [InlineData("no", false)]
        [InlineData("anyothervalue", false)]
        public void WebConfigTelemetry_RemovesProjectGuid_IfCLIOptedOutEnvVariableIsSet(string value, bool expectedOutput)
        {
            // Arrange
            string originalValue = Environment.GetEnvironmentVariable(TelemetryOptout);
            Environment.SetEnvironmentVariable(TelemetryOptout, value);

            // Act
            bool actualOutput = EnvironmentHelper.GetEnvironmentVariableAsBool(TelemetryOptout);


            // Assert
            Assert.Equal<bool>(expectedOutput, actualOutput);

            // reset the value back to the original value
            Environment.SetEnvironmentVariable(TelemetryOptout, originalValue);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests
{
    [TestClass]
    public class EnvironmentHelperTests
    {
        private const string TelemetryOptout = "DOTNET_CLI_TELEMETRY_OPTOUT";

        [TestMethod]
        [DataRow("true", true)]
        [DataRow("1", true)]
        [DataRow("yes", true)]
        [DataRow("false", false)]
        [DataRow("0", false)]
        [DataRow("no", false)]
        [DataRow("anyothervalue", false)]
        public void WebConfigTelemetry_RemovesProjectGuid_IfCLIOptedOutEnvVariableIsSet(string value, bool expectedOutput)
        {
            // Arrange
            string originalValue = Environment.GetEnvironmentVariable(TelemetryOptout);
            Environment.SetEnvironmentVariable(TelemetryOptout, value);

            // Act
            bool actualOutput = EnvironmentHelper.GetEnvironmentVariableAsBool(TelemetryOptout);


            // Assert
            Assert.AreEqual<bool>(expectedOutput, actualOutput);

            // reset the value back to the original value
            Environment.SetEnvironmentVariable(TelemetryOptout, originalValue);
        }
    }
}

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
        private const string TelemetryOptoutCommon = "DO_NOT_TRACK";

        [Theory]
        [InlineData(TelemetryOptout, "true", true)]
        [InlineData(TelemetryOptout, "1", true)]
        [InlineData(TelemetryOptout, "yes", true)]
        [InlineData(TelemetryOptout, "false", false)]
        [InlineData(TelemetryOptout, "0", false)]
        [InlineData(TelemetryOptout, "no", false)]
        [InlineData(TelemetryOptout, "anyothervalue", false)]
        [InlineData(TelemetryOptoutCommon, "true", true)]
        [InlineData(TelemetryOptoutCommon, "1", true)]
        [InlineData(TelemetryOptoutCommon, "yes", true)]
        [InlineData(TelemetryOptoutCommon, "false", false)]
        [InlineData(TelemetryOptoutCommon, "0", false)]
        [InlineData(TelemetryOptoutCommon, "no", false)]
        [InlineData(TelemetryOptoutCommon, "anyothervalue", false)]
        public void WebConfigTelemetry_RemovesProjectGuid_IfCLIOptedOutEnvVariableIsSet(string variable, string value, bool expectedOutput)
        {
            // Arrange
            string originalValue = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, value);

            // Act
            bool actualOutput = EnvironmentHelper.GetEnvironmentVariableAsBool(variable);


            // Assert
            Assert.Equal<bool>(expectedOutput, actualOutput);

            // reset the value back to the original value
            Environment.SetEnvironmentVariable(variable, originalValue);
        }
    }
}

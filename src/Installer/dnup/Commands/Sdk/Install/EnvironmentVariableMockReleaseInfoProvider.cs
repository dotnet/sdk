using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install
{
    internal class EnvironmentVariableMockReleaseInfoProvider : IReleaseInfoProvider
    {
        public List<string> GetAvailableChannels()
        {
            var channels = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_AVAILABLE_CHANNELS");
            if (string.IsNullOrEmpty(channels))
            {
                return new List<string> { "latest", "preview", "10", "10.0.1xx", "10.0.2xx", "9", "9.0.3xx", "9.0.2xx", "9.0.1xx" };
            }
            return channels.Split(',').ToList();
        }
        public string GetLatestVersion(string channel)
        {
            if (channel == "preview")
            {
                return "11.0.100-preview.1.42424";
            }
            else if (channel == "latest" || channel == "10" || channel == "10.0.2xx")
            {
                return "10.0.0-preview.7";
            }
            else if (channel == "10.0.1xx")
            {
                return "10.0.106";
            }
            else if (channel == "9" || channel == "9.0.3xx")
            {
                return "9.0.309";
            }
            else if (channel == "9.0.2xx")
            {
                return "9.0.212";
            }
            else if (channel == "9.0.1xx")
            {
                return "9.0.115";
            }

            return channel;
        }
    }
}

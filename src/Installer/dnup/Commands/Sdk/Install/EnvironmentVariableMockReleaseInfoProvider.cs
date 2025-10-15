using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install
{
    internal class EnvironmentVariableMockReleaseInfoProvider : IDotnetReleaseInfoProvider
    {
        IEnumerable<string> IDotnetReleaseInfoProvider.GetAvailableChannels()
        {
            var channels = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_AVAILABLE_CHANNELS");
            if (string.IsNullOrEmpty(channels))
            {
                return new List<string> { "latest", "preview", "10", "10.0.1xx", "10.0.2xx", "9", "9.0.3xx", "9.0.2xx", "9.0.1xx" };
            }
            return channels.Split(',').ToList();
        }
        public ReleaseVersion GetLatestVersion(InstallComponent component, string channel)
        {
            if (component != InstallComponent.SDK)
            {
                throw new NotImplementedException("Only SDK component is supported in this mock provider");
            }

            string version;
            if (channel == "preview")
            {
                version = "11.0.100-preview.1.42424";
            }
            else if (channel == "latest" || channel == "10" || channel == "10.0.2xx")
            {
                version = "10.0.0-preview.7";
            }
            else if (channel == "10.0.1xx")
            {
                version = "10.0.106";
            }
            else if (channel == "9" || channel == "9.0.3xx")
            {
                version = "9.0.309";
            }
            else if (channel == "9.0.2xx")
            {
                version = "9.0.212";
            }
            else if (channel == "9.0.1xx")
            {
                version = "9.0.115";
            }

            version = channel;

            return new ReleaseVersion(version);
        }

        public SupportType GetSupportType(InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
        
    }
}

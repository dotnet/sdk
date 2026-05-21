// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using static Microsoft.DotNet.NativeWrapper.Interop;

namespace Microsoft.DotNet.NativeWrapper
{
    public interface INetBundleInfo
    {
        public ReleaseVersion Version { get; }

        public string Path { get; }
    }

    public sealed class NetSdkInfo : INetBundleInfo
    {
        public ReleaseVersion Version { get; private set; }

        public string Path { get; private set; }

        public NetSdkInfo(string version, string path)
        {
            Version = new ReleaseVersion(version);
            Path = path;
        }
    }

    public sealed class NetRuntimeInfo : INetBundleInfo
    {
        public ReleaseVersion Version { get; private set; }

        public string Path { get; private set; }

        public string Name { get; private set; }

        public NetRuntimeInfo(string name, string version, string path)
        {
            Version = new ReleaseVersion(version);
            Path = path;
            Name = name;
        }
    }

    public sealed class NetEnvironmentInfo
    {
        public IEnumerable<NetRuntimeInfo> RuntimeInfo { get; private set; }

        public IEnumerable<NetSdkInfo> SdkInfo { get; private set; }

        public NetEnvironmentInfo(IEnumerable<NetRuntimeInfo> runtimeInfo, IEnumerable<NetSdkInfo> sdkInfo)
        {
            RuntimeInfo = runtimeInfo;
            SdkInfo = sdkInfo;
        }

        public NetEnvironmentInfo()
        {
            RuntimeInfo = [];
            SdkInfo = [];
        }

        internal unsafe void Initialize(ref hostfxr_dotnet_environment_info info, nint _)
        {
            int count = (int)info.framework_count;
            var framework = info.frameworks;

            List<NetRuntimeInfo> runtimeInfo = new(capacity: count);

            for (var i = 0; i < count; i++)
            {
                runtimeInfo.Add(new(framework->name, framework->version, framework->path));
                framework++;
            }

            RuntimeInfo = runtimeInfo;

            count = (int)info.sdk_count;
            var sdk = info.sdks;
            List<NetSdkInfo> sdkInfo = new(count);

            for (var i = 0; i < count; i++)
            {
                sdkInfo.Add(new(sdk->version, sdk->path));
                sdk++;
            }

            SdkInfo = sdkInfo;
        }
    }
}

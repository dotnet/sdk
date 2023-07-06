﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.MSBuildSdkResolver;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Tools.New
{
    internal class SdkInfoProvider : ISdkInfoProvider
    {
        private readonly Func<string> _getCurrentProcessPath;

        public Guid Id { get; } = Guid.Parse("{A846C4E2-1E85-4BF5-954D-17655D916928}");

        public SdkInfoProvider()
            : this(null)
        { }

        internal SdkInfoProvider(Func<string> getCurrentProcessPath)
        {
            _getCurrentProcessPath = getCurrentProcessPath;
        }

        public Task<string> GetCurrentVersionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Product.Version);
        }

        public Task<IEnumerable<string>> GetInstalledVersionsAsync(CancellationToken cancellationToken)
        {
            // Get the dotnet directory, while ignoring custom msbuild resolvers
            string dotnetDir = Microsoft.DotNet.NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(
                key =>
                    key.Equals("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", StringComparison.InvariantCultureIgnoreCase)
                        ? null
                        : Environment.GetEnvironmentVariable(key),
                _getCurrentProcessPath);

            IEnumerable<string> sdks;
            try
            {
                // sdks contain the full path, version is the last part
                //  details: https://github.com/dotnet/runtime/blob/5098d45cc1bf9649fab5df21f227da4b80daa084/src/native/corehost/fxr/sdk_info.cpp#L76
                sdks = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(dotnetDir).Select(Path.GetFileName);
            }
            // The NETCoreSdkResolverNativeWrapper is not properly initialized (case of OSx in test env) - let's manually perform what
            //  sdk_info::get_all_sdk_infos does
            catch (Exception e) when(e is HostFxrRuntimePropertyNotSetException or HostFxrNotFoundException)
            {
                string sdkDir = Path.Combine(dotnetDir, "sdk");
                sdks =
                    Directory.Exists(sdkDir)
                        ? Directory.GetDirectories(sdkDir).Select(Path.GetFileName).Where(IsValidFxVersion)
                        : Enumerable.Empty<string>();
            }
            return Task.FromResult(sdks);
        }

        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedVersions,
            IReadOnlyList<string> viableInstalledVersions)
        {
            if (viableInstalledVersions.Any())
            {
                return string.Format(LocalizableStrings.SdkInfoProvider_Message_SwitchSdk, viableInstalledVersions.ToCsvString());
            }
            else
            {
                return string.Format(LocalizableStrings.SdkInfoProvider_Message_InstallSdk, supportedVersions.ToCsvString());
            }
        }

        private static bool IsValidFxVersion(string versionString)
        {
            return FXVersion.TryParse(versionString, out _);
        }
    }
}

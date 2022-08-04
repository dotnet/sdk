// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.BuiltInManagedProvider;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.TemplateEngine.Edge.Installers.Folder;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.Edge.Mount.Archive;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;

namespace Microsoft.TemplateEngine.Edge
{
    public static class Components
    {
        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IMountPointFactory), new ZipFileMountPointFactory()),
                (typeof(IMountPointFactory), new FileSystemMountPointFactory()),
                (typeof(ITemplatePackageProviderFactory), new GlobalSettingsTemplatePackageProviderFactory()),
                (typeof(IInstallerFactory), new FolderInstallerFactory()),
                (typeof(IInstallerFactory), new NuGetInstallerFactory()),
                (typeof(ITemplateConstraintFactory), new OSConstraintFactory()),
                (typeof(ITemplateConstraintFactory), new HostConstraintFactory()),
                (typeof(ITemplateConstraintFactory), new WorkloadConstraintFactory()),
                (typeof(ITemplateConstraintFactory), new SdkVersionConstraintFactory()),
                (typeof(IBindSymbolSource), new EnvironmentVariablesBindSource()),
                (typeof(IBindSymbolSource), new HostParametersBindSource()),
            };

        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> MandatoryComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IMountPointFactory), new ZipFileMountPointFactory()),
                (typeof(IMountPointFactory), new FileSystemMountPointFactory()),
                (typeof(ITemplatePackageProviderFactory), new GlobalSettingsTemplatePackageProviderFactory()),
                (typeof(IInstallerFactory), new FolderInstallerFactory()),
                (typeof(IBindSymbolSource), new EnvironmentVariablesBindSource()),
                (typeof(IBindSymbolSource), new HostParametersBindSource()),
            };
    }
}

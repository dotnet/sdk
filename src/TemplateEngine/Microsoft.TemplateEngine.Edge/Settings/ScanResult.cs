// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Returned by <see cref="Scanner.Scan"/>.
    /// </summary>
    public class ScanResult : IDisposable
    {
        internal ScanResult(
            IMountPoint mountPoint,
            IReadOnlyList<IScanTemplateInfo> templates,
            IReadOnlyList<ILocalizationLocator> localizations,
            IReadOnlyList<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)> components)
        {
            MountPoint = mountPoint;
            Templates = templates;
#pragma warning disable CS0618 // Type or member is obsolete
            Localizations = localizations;
#pragma warning restore CS0618 // Type or member is obsolete
            Components = components;
        }

        /// <summary>
        /// Gets the mount point that was scanned.
        /// </summary>
        public IMountPoint MountPoint { get; }

        /// <summary>
        /// All components found inside mountpoint.
        /// AssemblyPath is full path inside <see cref="IMountPoint"/>.
        /// InterfaceType is type of interface that is implemented by component.
        /// Instance is object that implements InterfaceType.
        /// </summary>
        public IReadOnlyList<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)> Components { get; }

        /// <summary>
        /// All template localizations found inside mountpoint.
        /// </summary>
        [Obsolete("Use IScanTemplateInfo.Localizations instead.")]
        public IReadOnlyList<ILocalizationLocator> Localizations { get; }

        /// <summary>
        /// All templates found inside mountpoint.
        /// </summary>
        public IReadOnlyList<IScanTemplateInfo> Templates { get; }

        /// <summary>
        /// Disposes <see cref="MountPoint"/> that was scanned.
        /// </summary>
        public void Dispose()
        {
            MountPoint.Dispose();
        }
    }
}

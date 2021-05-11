// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    internal class ZipFileMountPointFactory : IMountPointFactory
    {
        internal static readonly Guid FactoryId = new Guid("94E92610-CF4C-4F6D-AEB6-9E42DDE1899D");

        public Guid Id => FactoryId;

        public bool TryMount(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, string mountPointUri, out IMountPoint mountPoint)
        {
            if (!Uri.TryCreate(mountPointUri, UriKind.Absolute, out var uri))
            {
                mountPoint = null;
                return false;
            }

            if (!uri.IsFile)
            {
                mountPoint = null;
                return false;
            }

            ZipArchive archive;

            if (parent == null)
            {
                if (!environmentSettings.Host.FileSystem.FileExists(uri.LocalPath))
                {
                    mountPoint = null;
                    return false;
                }

                try
                {
                    archive = new ZipArchive(environmentSettings.Host.FileSystem.OpenRead(uri.LocalPath), ZipArchiveMode.Read, false);
                }
                catch
                {
                    mountPoint = null;
                    return false;
                }
            }
            else
            {
                IFile file = parent.Root.FileInfo(uri.LocalPath);

                if (!file.Exists)
                {
                    mountPoint = null;
                    return false;
                }

                try
                {
                    archive = new ZipArchive(file.OpenRead(), ZipArchiveMode.Read, false);
                }
                catch
                {
                    mountPoint = null;
                    return false;
                }
            }

            mountPoint = new ZipFileMountPoint(environmentSettings, parent, mountPointUri, archive);
            return true;
        }
    }
}

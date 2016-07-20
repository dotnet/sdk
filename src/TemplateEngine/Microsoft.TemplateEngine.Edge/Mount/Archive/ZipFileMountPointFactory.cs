using System;
using System.IO;
using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    public class ZipFileMountPointFactory : IMountPointFactory
    {
        internal static readonly Guid FactoryId = new Guid("94E92610-CF4C-4F6D-AEB6-9E42DDE1899D");

        public Guid Id => FactoryId;

        public bool TryMount(IMountPoint parent, string place, out IMountPoint mountPoint)
        {
            return TryMount(parent, Guid.NewGuid(), place, out mountPoint);
        }

        private static bool TryMount(IMountPoint parent, Guid id, string place, out IMountPoint mountPoint)
        {
            ZipArchive archive;

            if (parent == null)
            {
                if (!File.Exists(place))
                {
                    mountPoint = null;
                    return false;
                }

                try
                {
                    archive = new ZipArchive(File.OpenRead(place), ZipArchiveMode.Read, false);
                }
                catch
                {
                    mountPoint = null;
                    return false;
                }
            }
            else
            {
                IFile file = parent.Root.FileInfo(place);

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

            MountPointInfo info = new MountPointInfo(parent?.Info?.MountPointId ?? Guid.Empty, FactoryId, id, place);
            mountPoint = new ZipFileMountPoint(info, archive);
            return true;
        }

        public bool TryMount(IMountPointManager manager, MountPointInfo info, out IMountPoint mountPoint)
        {
            IMountPoint parent = null;

            if (info.ParentMountPointId != Guid.Empty)
            {
                if (!manager.TryDemandMountPoint(info.ParentMountPointId, out parent))
                {
                    mountPoint = null;
                    return false;
                }
            }

            return TryMount(parent, info.MountPointId, info.Place, out mountPoint);
        }

        public void DisposeMountPoint(IMountPoint mountPoint)
        {
            ZipFileMountPoint mp = mountPoint as ZipFileMountPoint;
            mp?.Archive?.Dispose();
        }
    }
}
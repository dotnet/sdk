using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public class MountPointInfo
    {
        public Guid ParentMountPointId { get; }

        public Guid MountPointFactoryId { get; }

        public Guid MountPointId { get; }

        public string Place { get; }

        public MountPointInfo(Guid parentMountPointId, Guid mountPointFactoryId, Guid mountPointId, string place)
        {
            ParentMountPointId = parentMountPointId;
            MountPointFactoryId = mountPointFactoryId;
            MountPointId = mountPointId;
            Place = place;
        }
    }
}
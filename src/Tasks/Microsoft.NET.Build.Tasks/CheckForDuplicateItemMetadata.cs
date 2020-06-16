using Microsoft.Build.Framework;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForDuplicateItemMetadata : TaskBase
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string MetadataName { get; set; }

        [Output]
        public ITaskItem[] DuplicateItems { get; set; }

        [Output]
        public string[] DuplicatedMetadata { get; set; }

        [Output]
        public bool DuplicatesExist { get; set; }

        protected override void ExecuteCore()
        {
            var groupings = Items.GroupBy(item => item.GetMetadata(MetadataName));
            DuplicatesExist = groupings.Where(g => g.Count() > 1).Any();
            DuplicatedMetadata = groupings
                .Where(g => g.Count() > 1)
                .Select(g=> g.Key)
                .ToArray();
            DuplicateItems = groupings
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.ToArray())
                .ToArray();
        }
    }
}

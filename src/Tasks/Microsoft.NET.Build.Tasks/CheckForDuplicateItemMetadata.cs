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
            var groupings = Items.GroupBy(item => item.GetMetadata(MetadataName))
                .Where(g => g.Count() > 1)
                .ToList();
            DuplicatesExist = groupings.Any();
            DuplicatedMetadata = groupings
                .Select(g=> g.Key)
                .ToArray();
            DuplicateItems = groupings
                .SelectMany(g => g)
                .ToArray();
        }
    }
}

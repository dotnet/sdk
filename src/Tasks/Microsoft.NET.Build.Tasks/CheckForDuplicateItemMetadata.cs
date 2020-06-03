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
        public ITaskItem[] DeduplicatedItems { get; set; }

        [Output]
        public bool DuplicatesExist { get; set; }

        protected override void ExecuteCore()
        {
            var groupings = Items.GroupBy(item => item.GetMetadata(MetadataName));
            DuplicatesExist = groupings.Where(g => g.Count() > 1).Any();
            DeduplicatedItems = groupings
                .Where(g => g.Count() == 1)
                .Select(g => g.FirstOrDefault())
                .ToArray();
        }
    }
}

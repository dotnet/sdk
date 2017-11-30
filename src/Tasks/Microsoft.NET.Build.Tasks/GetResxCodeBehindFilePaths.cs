using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class GetResxCodeBehindFilePaths : TaskBase
    {
        [Required]
        public ITaskItem[] ResxFiles { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public ITaskItem[] CodeBehindFiles { get; set; }

        [Output]
        public ITaskItem[] ResxFilesWithCodeBehindPath { get; set; }

        protected override void ExecuteCore()
        {
            List<ITaskItem> codeBehindFiles = new List<ITaskItem>();
            List<ITaskItem> processedResxFiles = new List<ITaskItem>();

            foreach (ITaskItem resxFile in ResxFiles)
            {
                ITaskItem item = new TaskItem(Path.Combine(OutputPath, resxFile.ItemSpec));
                item.SetMetadata("ResxFilePath", resxFile.ItemSpec);
                item.SetMetadata("ResxFileDirectory", Path.GetDirectoryName(resxFile.ItemSpec));
                item.SetMetadata("ResxFileName", Path.GetFileName(resxFile.ItemSpec));
                codeBehindFiles.Add(item);

                ITaskItem resxCopy = new TaskItem(resxFile.ItemSpec);
                resxFile.CopyMetadataTo(resxCopy);
                resxCopy.SetMetadata("CodeBehindFile", item.ItemSpec);
                processedResxFiles.Add(resxCopy);
            }

            CodeBehindFiles = codeBehindFiles.ToArray();
            ResxFilesWithCodeBehindPath = processedResxFiles.ToArray();
        }
    }
}

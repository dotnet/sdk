using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ComputeResxCodeBehindFilePath : TaskBase
    {
        [Required]
        public ITaskItem[] EmbeddedResources { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public string Language { get; set; }

        [Required]
        public bool AutoGenerateByDefault { get; set; }

        [Output]
        public ITaskItem[] ResxFilesWithCodeBehindPath { get; set; }

        protected override void ExecuteCore()
        {
            string languageExtension;
            if (Language.ToUpperInvariant() == "C#")
            {
                languageExtension = ".cs";
            }
            else if (Language.ToUpperInvariant() == "VB")
            {
                languageExtension = ".vb";
            }
            else
            {
                throw new BuildErrorException("Unrecognized Language '{0}'", Language);
            }

            List<ITaskItem> processedResxFiles = new List<ITaskItem>();

            foreach (ITaskItem resxFile in EmbeddedResources)
            {
                if (resxFile.GetMetadata("Extension") != ".resx")
                    // Skip EmbeddedResource items that aren't ResX files.
                    continue;

                string alternateDesignerFilePath = Path.Combine(resxFile.GetMetadata("Directory"), resxFile.GetMetadata("FileName") + ".Designer" + languageExtension);
                if (File.Exists(alternateDesignerFilePath))
                    // If the in-source designer file exists, we assume that this ResX file is maintained by Visual Studio
                    // and do not generate a code-behind for it. (If we did, we would get double-definition errors in the
                    // generated code-behind.)
                    continue;

                string generateCodeBehindString = resxFile.GetMetadata("AutoGenerateCodeBehind");
                bool generateCodeBehind = AutoGenerateByDefault;

                if (!string.IsNullOrEmpty(generateCodeBehindString))
                {
                    bool parseOK = bool.TryParse(generateCodeBehindString, out generateCodeBehind);
                    if (!parseOK)
                        Log.LogWarning("Unrecognized AutoGenerateCodeBehind metadata value on {0}, using default value {1}", resxFile.ItemSpec, AutoGenerateByDefault);
                }

                if (!generateCodeBehind)
                    continue;

                string codeBehindPath = Path.Combine(IntermediateOutputPath, resxFile.GetMetadata("FileName") + ".Designer" + languageExtension);

                ITaskItem resxCopy = new TaskItem(resxFile.ItemSpec);
                resxFile.CopyMetadataTo(resxCopy);
                resxCopy.SetMetadata("CodeBehindFile", codeBehindPath);
                processedResxFiles.Add(resxCopy);
            }

            ResxFilesWithCodeBehindPath = processedResxFiles.ToArray();
        }
    }
}

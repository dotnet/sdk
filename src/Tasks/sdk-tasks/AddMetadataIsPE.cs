// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks
{
    public class AddMetadataIsPE : Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Output]
        public ITaskItem[] ResultItems { get; set; }

        public override bool Execute()
        {
            var resultItemsList = new List<ITaskItem>();
            
            foreach (var item in Items)
            {
                var resultItem = new TaskItem(item);
                item.CopyMetadataTo(resultItem);

                var isPe = File.Exists(resultItem.GetMetadata("FullPath")) && HasMetadata(resultItem.GetMetadata("FullPath"));
                resultItem.SetMetadata("IsPE", isPe.ToString());

                resultItemsList.Add(resultItem);
            }

            ResultItems = resultItemsList.ToArray();

            return true;
        }

        private static bool HasMetadata(string pathToFile)
        {
            try
            {
                using (var inStream = File.OpenRead(pathToFile))
                {
                    using (var peReader = new PEReader(inStream))
                    {
                        return peReader.HasMetadata;
                    }
                }
            }
            catch (BadImageFormatException) { }

            return false;
        }
    }
}

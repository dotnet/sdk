// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.NET.Sdk.Testing.Tasks
{
    public class GetTestsProject : Build.Utilities.Task
    {
        [Required]

        public ITaskItem TargetPath { get; set; }

        [Required]

        public ITaskItem GetTestsProjectPipeName { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Low, $"Target path: {TargetPath}");

                using NamedPipeClient namedPipeClient = new(GetTestsProjectPipeName.ItemSpec);

                namedPipeClient.RegisterSerializer<Module>(new ModuleSerializer());
                namedPipeClient.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());

                namedPipeClient.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();
                namedPipeClient.RequestReplyAsync<Module, VoidResponse>(new Module(TargetPath.ItemSpec), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogMessage(ex.Message);

                throw;
            }

            return true;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class ResolveSdkCommand : MSBuildCommand
    {
        public bool IncludeDefaultSdkResolver { get; set; } = false;

        public string SdkResolversFolder { get; set; } = TestContext.Current.ToolsetUnderTest.SdkResolverPath;

        public ResolveSdkCommand(TestAsset testAsset, string relativePathToProject = null)
            : base(testAsset, "Build", relativePathToProject)
        {
        }

        public override CommandResult Execute(IEnumerable<string> args)
        {
            CommandResult result = new CommandResult();
            var prevResolversFolder = Environment.GetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER");
            var prevIncludeDefault = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER", SdkResolversFolder);
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", IncludeDefaultSdkResolver.ToString());
                TestContext.Current.ToolsetUnderTest.ShouldUseSdkResolverPath = true;

                result = base.Execute(args);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER", prevResolversFolder);
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", prevIncludeDefault);
                TestContext.Current.ToolsetUnderTest.ShouldUseSdkResolverPath = false;
            }
            return result;
        }
    }
}

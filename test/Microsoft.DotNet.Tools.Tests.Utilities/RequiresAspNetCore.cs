// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RequiresAspNetCore : FactAttribute
    {
        public RequiresAspNetCore()
        {
            if (RepoDirectoriesProvider.Stage2AspNetCore == null)
            {
                this.Skip = $"This test requires a AspNetCore but it isn't present.";
            }
        }
    }
}

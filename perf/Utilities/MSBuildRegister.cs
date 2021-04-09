// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public static class MSBuildRegister
    {
        private static int s_registered;

        public static void RegisterInstance()
        {
            if (Interlocked.Exchange(ref s_registered, 1) == 0)
            {
                Build.Locator.MSBuildLocator.RegisterDefaults();
            }
        }
    }
}

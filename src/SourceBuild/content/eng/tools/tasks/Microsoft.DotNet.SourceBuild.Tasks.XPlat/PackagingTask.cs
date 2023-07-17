// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public abstract partial class PackagingTask : ITask
    {
        private Log _log = null;

        internal Log Log
        {
            get { return _log ?? (_log = new Log(new TaskLoggingHelper(this))); }
        }

        public PackagingTask()
        {
        }

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public ITaskHost HostObject
        {
            get;
            set;
        }

        public abstract bool Execute();
    }
}

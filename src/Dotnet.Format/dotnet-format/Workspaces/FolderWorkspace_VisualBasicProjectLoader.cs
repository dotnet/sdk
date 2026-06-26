// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private sealed class VisualBasicProjectLoader : ProjectLoader
        {
            public override string Language => LanguageNames.VisualBasic;
            public override string FileExtension => ".vb";
        }
    }
}

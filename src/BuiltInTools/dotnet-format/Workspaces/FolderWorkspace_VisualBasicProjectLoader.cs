// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

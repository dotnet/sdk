// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    internal class TestXmlReferenceResolver : XmlReferenceResolver
    {
        public Dictionary<string, string> XmlReferences { get; } =
            new Dictionary<string, string>();

        public override bool Equals(object other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
#pragma warning disable RS1024 // Compare symbols correctly
            return RuntimeHelpers.GetHashCode(this);
#pragma warning restore RS1024 // Compare symbols correctly
        }

        public override Stream OpenRead(string resolvedPath)
        {
            if (!XmlReferences.TryGetValue(resolvedPath, out var content))
            {
                return null;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        public override string ResolveReference(string path, string baseFilePath)
        {
            return path;
        }
    }
}

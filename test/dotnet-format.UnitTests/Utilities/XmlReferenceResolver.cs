// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.TestFramework
{
    public class TestFrameworkReference
    {
        public TestFrameworkReference(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}

﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create a DifferenceVisitor instance.
    /// </summary>
    public sealed class DifferenceVisitorFactory : IDifferenceVisitorFactory
    {
        /// <inheritdoc />
        public IDifferenceVisitor Create() => new DifferenceVisitor();
    }
}

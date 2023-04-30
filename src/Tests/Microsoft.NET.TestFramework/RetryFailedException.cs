// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.TestFramework
{
    public class RetryFailedException : Exception
    {
        public RetryFailedException(string message) : base(message)
        {
        }
        public RetryFailedException()
        {
        }
        public RetryFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

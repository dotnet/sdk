﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class WorkloadException : Exception
    {
        public uint Error
        {
            get;
            protected set;
        }

        public WorkloadException() : base()
        {
            
        }

        public WorkloadException(string? message) : base(message)
        {

        }

        public WorkloadException(uint error, string? message) : base(message)
        {
            Error = error;
        }

        public WorkloadException(string? message, Exception? innerException) : base(message, innerException)
        {

        }

        public WorkloadException(string? message, int hresult) : this(message)
        {
            HResult = hresult;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    public class ValueReadEventArgs : EventArgs, IValueReadEventArgs
    {
        public ValueReadEventArgs(string key, object value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }

        public object Value { get; set; }
    }
}

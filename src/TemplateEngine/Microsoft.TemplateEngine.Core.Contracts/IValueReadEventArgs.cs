// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IValueReadEventArgs
    {
        string Key { get; set; }

        object Value { get; set; }
    }
}

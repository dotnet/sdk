// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    /// <summary>
    /// Extends <see cref="IVariableCollection"/> with the events raised when collection is being read or changed.
    /// </summary>
    public interface IMonitoredVariableCollection : IVariableCollection
    {
        event KeysChangedEventHander KeysChanged;

        event ValueReadEventHander ValueRead;
    }
}

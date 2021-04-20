// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IRunSpec
    {
        string VariableFormatString { get; }

        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);

        IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations);

        IVariableCollection ProduceCollection(IVariableCollection parent);
    }
}

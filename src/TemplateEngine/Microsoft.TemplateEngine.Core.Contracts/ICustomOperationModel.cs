// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ICustomOperationModel
    {
        string Type { get; }

        string Condition { get; }
    }
}

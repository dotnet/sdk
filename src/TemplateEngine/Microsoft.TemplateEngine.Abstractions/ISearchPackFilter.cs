// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    [Obsolete("The interface is moved to Microsoft.TemplateSearch.Common.")]
    public interface ISearchPackFilter
    {
        bool ShouldPackBeFiltered(string candidatePackName, string candidatePackVersion);
    }
}

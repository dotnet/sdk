// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    [Obsolete("The ShortNameList is added to ITemplateInfo instead")]
    public interface IShortNameList
    {
        [Obsolete("Use ITemplateInfo.ShortNameList instead")]
        IReadOnlyList<string> ShortNameList { get; }
    }
}

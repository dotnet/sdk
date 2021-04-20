// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("use" + nameof(Microsoft.TemplateEngine.Abstractions.TemplateFiltering.MatchInfo.Name) + " instead")]
    public enum MatchLocation
    {
        Unspecified,
        Name,
        ShortName,
        Alias,      // never used, alias expansion occurs prior to matching
        Classification,
        Language,   // this is meant for the input language
        Context,
        OtherParameter,
        Baseline,
        DefaultLanguage,
        Author
    }    
}

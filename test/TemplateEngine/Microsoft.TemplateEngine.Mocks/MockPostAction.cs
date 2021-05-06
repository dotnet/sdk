// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockPostAction : IPostAction
    {
        public string Description { get; set; }

        public Guid ActionId { get; set; }

        public bool ContinueOnError { get; set; }

        public IReadOnlyDictionary<string, string> Args { get; set; }

        public string ManualInstructions { get; set; }
    }
}

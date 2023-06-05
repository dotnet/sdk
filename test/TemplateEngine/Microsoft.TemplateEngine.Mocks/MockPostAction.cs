// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockPostAction : IPostAction
    {
        public MockPostAction(string? description, string? manualInstructions, Guid actionId, bool continueOnError, IReadOnlyDictionary<string, string> args)
        {
            Description = description;
            ManualInstructions = manualInstructions;
            ActionId = actionId;
            ContinueOnError = continueOnError;
            Args = args;
        }

        public string? Description { get; set; }

        public Guid ActionId { get; set; }

        public bool ContinueOnError { get; set; }

        public IReadOnlyDictionary<string, string> Args { get; set; }

        public string? ManualInstructions { get; set; }
    }
}

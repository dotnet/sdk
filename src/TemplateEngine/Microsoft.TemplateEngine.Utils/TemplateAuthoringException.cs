// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Utils
{
    public class TemplateAuthoringException : Exception
    {
        public TemplateAuthoringException(string message, string configItem)
            : base(message)
        {
            ConfigItem = configItem;
        }

        public TemplateAuthoringException(string message, string configItem, Exception innerException)
            : base(message, innerException)
        {
            ConfigItem = configItem;
        }

        public string ConfigItem { get; }
    }
}

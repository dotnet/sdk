// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the source modifier, these settings are applied to in addition to the top-level source information, in case <see cref="ConditionedConfigurationElement.Condition"/> is met.
    /// </summary>
    public sealed class SourceModifier : ConditionedConfigurationElement
    {
        internal SourceModifier() { }

        /// <summary>
        /// Defines the files to be just copied when instantiating template. Operations will not be applied to them.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> CopyOnly { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be include when instantiating template. Operations will be applied to them.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> Include { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be excluded when instantiating template. These files will not be processed during template instantiation.
        /// Can be glob. If single value is given, it can be also a filename where from to read this configuration.
        /// </summary>
        public IReadOnlyList<string> Exclude { get; internal init; } = Array.Empty<string>();

        /// <summary>
        /// Defines the files to be renamed when instantiating the template. The key is a source file name, the value is the final file name.
        /// </summary>
        public IReadOnlyDictionary<string, string> Rename { get; internal init; } = ExtendedFileSource.RenameDefaults;
    }
}

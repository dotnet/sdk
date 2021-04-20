// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateEngineHost
    {
        IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents { get; }

        IPhysicalFileSystem FileSystem { get; }

        string HostIdentifier { get; }

        IReadOnlyList<string> FallbackHostTemplateConfigNames { get; }

        string Version { get; }

        void LogTiming(string label, TimeSpan duration, int depth);

        void LogMessage(string message);

        void OnCriticalError(string code, string message, string currentFile, long currentPosition);

        bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition);

        // return of true means a new value was provided
        bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges);

        void OnSymbolUsed(string symbol, object value);

        void LogDiagnosticMessage(string message, string category, params string[] details);

        bool TryGetHostParamDefault(string paramName, out string value);

        void VirtualizeDirectory(string path);

        bool OnConfirmPartialMatch(string name);
    }
}

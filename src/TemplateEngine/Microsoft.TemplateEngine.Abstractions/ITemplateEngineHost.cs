// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Gets host-specific properties, loggers and provides access to file system.
    /// </summary>
    public interface ITemplateEngineHost
    {
        /// <summary>
        /// Gets the list of built-in components provided by the host.
        /// </summary>
        IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents { get; }

        /// <summary>
        /// Provides access to file system.
        /// Depending on the settings, the file system can be physical or in-memory or both depending on the file path.
        /// To virtualize certain file path, use <see cref="VirtualizeDirectory(string)"/>.
        /// </summary>
        IPhysicalFileSystem FileSystem { get; }

        /// <summary>
        /// Gets the identifier of the host.
        /// </summary>
        string HostIdentifier { get; }

        /// <summary>
        /// Gets the fallback names that will be probed to locate the host specific template settings file.
        /// The primary host template config name is <see cref="HostIdentifier"/>.
        /// </summary>
        IReadOnlyList<string> FallbackHostTemplateConfigNames { get; }

        /// <summary>
        /// Gets default logger for given template engine host.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets logger factory for given template engine host.
        /// </summary>
        ILoggerFactory LoggerFactory { get; }

        /// <summary>
        /// Gets the version of the host.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Tries to get default host parameter by its name.
        /// </summary>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="value">Default value of the parameter provided by the host.</param>
        /// <returns>true - if default value was provided; false otherwise.</returns>
        bool TryGetHostParamDefault(string paramName, out string? value);

        /// <summary>
        /// Virtualizes access to the <paramref name="path"/>. After the location is virtualized, the file read/writes will be done from/to memory instead of physical file system. All subfolders in the location will be also virtualized.
        /// </summary>
        /// <param name="path"></param>
        void VirtualizeDirectory(string path);

        /// <summary>
        /// Action to be done when potentially destructive changes on template instantiation are detected.
        /// The host can implement it as needed: prompt user, show error, etc.
        /// In case template instantiation should proceed, the method should return true.
        /// In case template instantiation should be aborted, the method should return false.
        /// </summary>
        /// <param name="changes">the list of file changes to be done on template instantiation.</param>
        /// <param name="destructiveChanges">the list of destructive file changes (modify/remove) to be performed on template instantiation.</param>
        /// <returns>
        /// true - in case template engine should proceed with template instantiation and perform destructive changes;
        /// false - if the template instantiation should be aborted.
        /// </returns>
        bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges);

        #region Obsolete

        [Obsolete("Use " + nameof(Logger) + " instead.")]
        void LogTiming(string label, TimeSpan duration, int depth);

        [Obsolete("Use " + nameof(Logger) + " instead.")]
        void LogMessage(string message);

        [Obsolete("Use " + nameof(Logger) + " instead.")]
        void OnCriticalError(string code, string message, string currentFile, long currentPosition);

        [Obsolete("Use " + nameof(Logger) + " instead.")]
        bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition);

        bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue);

        [Obsolete("The method is deprecated.")]
        void OnSymbolUsed(string symbol, object value);

        [Obsolete("Use " + nameof(Logger) + " instead.")]
        void LogDiagnosticMessage(string message, string category, params string[] details);

        [Obsolete("The method is deprecated.")]
        bool OnConfirmPartialMatch(string name);

        #endregion
    }
}

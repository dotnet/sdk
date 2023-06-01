// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Suppression engine that contains a collection of <see cref="Suppression"/> items. It provides API to add a suppression, check if a passed-in suppression is already suppressed
    /// and serialize all suppressions down into a file.
    /// </summary>
    public interface ISuppressionEngine
    {
        /// <summary>
        /// If true, adds the suppression to the collection when passed into <see cref="IsErrorSuppressed(Suppression)"/>. 
        /// </summary>
        bool BaselineAllErrors { get; }

        /// <summary>
        /// The baseline suppressions from the passed-in suppression files.
        /// </summary>
        IReadOnlyCollection<Suppression> BaselineSuppressions { get; }

        /// <summary>
        /// The current suppressions for api compatibility differences.
        /// </summary>
        IReadOnlyCollection<Suppression> Suppressions { get; }

        /// <summary>
        /// Checks if the passed in error is suppressed.
        /// </summary>
        /// <param name="error">The <see cref="Suppression"/> error to check.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        bool IsErrorSuppressed(Suppression error);

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="suppression">The <see cref="Suppression"/> to be added.</param>
        void AddSuppression(Suppression suppression);

        /// <summary>
        /// Retrieves obsolete baseline suppressions.
        /// </summary>
        /// <returns>Returns obsolete suppressions that should be removed from the baseline.</returns>
        IReadOnlyCollection<Suppression> GetObsoleteSuppressions();

        /// <summary>
        /// Writes all suppressions in collection down to a file, if empty it doesn't write anything.
        /// </summary>
        /// <param name="suppressionOutputFile">The path to the file to be written.</param>
        /// <param name="removeObsoleteSuppressions">If <see langword="true"/>, removes obsolete suppressions.</param>
        /// <returns><see langword="true" /> if the suppression file is written.</returns>
        bool WriteSuppressionsToFile(string suppressionOutputFile, bool removeObsoleteSuppressions);
    }
}

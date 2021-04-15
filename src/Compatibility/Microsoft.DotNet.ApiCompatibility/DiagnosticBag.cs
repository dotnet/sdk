﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// This is a bag that contains a list of <see cref="IDiagnostic"/> and filters them out based on the
    /// noWarn and ignoredDifferences settings when they are added to the bag.
    /// </summary>
    /// <typeparam name="T">Type to represent the diagnostics.</typeparam>
    public class DiagnosticBag<T> where T : IDiagnostic
    {
        private readonly Dictionary<string, HashSet<string>> _ignore;
        private readonly HashSet<string> _noWarn;

        private readonly List<T> _differences = new();

        /// <summary>
        /// Instantiate an diagnostic bag with the provided settings to ignore diagnostics.
        /// </summary>
        /// <param name="noWarn">Comma separated list of diagnostic IDs to ignore.</param>
        /// <param name="ignoredDifferences">An array of differences to ignore based on diagnostic ID and reference ID.</param>
        public DiagnosticBag(string noWarn, (string diagnosticId, string referenceId)[] ignoredDifferences)
        {
            _noWarn = new HashSet<string>(noWarn?.Split(';'));
            _ignore = new Dictionary<string, HashSet<string>>();

            foreach ((string diagnosticId, string referenceId) in ignoredDifferences)
            {
                if (!_ignore.TryGetValue(diagnosticId, out HashSet<string> members))
                {
                    members = new HashSet<string>();
                    _ignore.Add(diagnosticId, members);
                }

                members.Add(referenceId);
            }
        }

        /// <summary>
        /// Adds the differences to the diagnostic bag if they are not found in the exclusion settings.
        /// </summary>
        /// <param name="differences">The differences to add.</param>
        public void AddRange(IEnumerable<T> differences)
        {
            foreach (T difference in differences)
                Add(difference);
        }

        /// <summary>
        /// Adds a difference to the diagnostic bag if they are not found in the exclusion settings.
        /// </summary>
        /// <param name="difference">The difference to add.</param>
        public void Add(T difference)
        {
            if (_noWarn.Contains(difference.DiagnosticId))
                return;

            if (_ignore.TryGetValue(difference.DiagnosticId, out HashSet<string> members))
            {
                if (members.Contains(difference.ReferenceId))
                {
                    return;
                }
            }

            _differences.Add(difference);
        }

        /// <summary>
        /// A list of differences contained in the diagnostic bag.
        /// </summary>
        public IEnumerable<T> Differences => _differences;
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    internal class Checker
    {
        private readonly Dictionary<string, HashSet<string>> _ignore;
        private readonly HashSet<string> _noWarn;

        public Checker(string noWarn, (string diagnosticId, string referenceId)[] ignoredDifferences, string[] possibleDiagnosticIds)
        {
            if (possibleDiagnosticIds == null)
            {
                _noWarn = new HashSet<string>(noWarn?.Split(';'));
            }
            else
            {
                _noWarn = new HashSet<string>(noWarn?.Split(';').Where(t => possibleDiagnosticIds.Contains(t)));
            }

            _ignore = new Dictionary<string, HashSet<string>>();

            if (ignoredDifferences != null)
            {
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
        }

        public bool Contain(string diagnosticId, string referenceId)
        {
            if (_noWarn.Contains(diagnosticId))
                return true;

            if (_ignore.TryGetValue(diagnosticId, out HashSet<string> members))
            {
                if (members.Contains(referenceId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

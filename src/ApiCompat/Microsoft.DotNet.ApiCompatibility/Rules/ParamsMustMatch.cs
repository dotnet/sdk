// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class ParamsMustMatch : IRule
    {
        private readonly RuleSettings _settings;

        public ParamsMustMatch(RuleSettings settings, RuleRunnerContext context)
        {
            _settings = settings;
        }
    }
}

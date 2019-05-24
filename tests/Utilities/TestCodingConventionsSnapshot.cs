// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    public class TestCodingConventionsSnapshot : ICodingConventionsSnapshot
    {
        public IUniversalCodingConventions UniversalConventions => throw new NotImplementedException();

        public IReadOnlyDictionary<string, object> AllRawConventions { get; }

        public int Version => 1;

        public TestCodingConventionsSnapshot(IReadOnlyDictionary<string, string> conventions)
        {
            AllRawConventions = conventions.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
        }

        public bool TryGetConventionValue<T>(string conventionName, out T conventionValue)
        {
            if (!AllRawConventions.ContainsKey(conventionName))
            {
                conventionValue = default;
                return false;
            }

            var value = AllRawConventions[conventionName];

            if (typeof(T) == typeof(bool))
            {
                conventionValue = (T)(object)Convert.ToBoolean(value);
            }
            else
            {
                conventionValue = (T)value;
            }

            return true;
        }
    }
}

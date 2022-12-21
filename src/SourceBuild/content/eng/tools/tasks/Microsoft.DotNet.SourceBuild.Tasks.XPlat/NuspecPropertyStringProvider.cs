// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuspecPropertyStringProvider
    {
        public static Dictionary<string, string> GetNuspecPropertyDictionary(string[] nuspecProperties)
        {
            if (nuspecProperties == null)
            {
                return null;
            }

            var propertyDictionary = new Dictionary<string, string>();
            foreach (var propertyString in nuspecProperties)
            {
                var property = GetKeyValuePair(propertyString);
                propertyDictionary[property.Item1] = property.Item2;
            }

            return propertyDictionary;
        }

        public static Func<string, string> GetNuspecPropertyProviderFunction(string[] nuspecPropertyStrings)
        {
            var propertyDictionary = GetNuspecPropertyDictionary(nuspecPropertyStrings);

            if (propertyDictionary == null)
            {
                return null;
            }

            return k => propertyDictionary[k];
        }

        private static Tuple<string, string> GetKeyValuePair(string propertyString)
        {
            propertyString = propertyString.Trim();

            var indexOfEquals = propertyString.IndexOf("=", StringComparison.Ordinal);

            if (indexOfEquals == -1)
            {
                throw new InvalidDataException($"Nuspec property {propertyString} does not have an \'=\' character in it");
            }

            if (indexOfEquals == propertyString.Length - 1)
            {
                throw new InvalidDataException($"Nuspec property {propertyString} does not have a value");
            }

            if (indexOfEquals == 0)
            {
                throw new InvalidDataException($"Nuspec property {propertyString} does not have a key");
            }

            var key = propertyString.Substring(0, indexOfEquals);

            var valueStartIndex = indexOfEquals + 1;
            var valueLength = propertyString.Length - valueStartIndex;
            var value = propertyString.Substring(valueStartIndex, valueLength);

            return new Tuple<string, string>(key, value);
        }
    }
}

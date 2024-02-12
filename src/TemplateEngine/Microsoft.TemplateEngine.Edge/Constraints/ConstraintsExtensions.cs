// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal static class Extensions
    {
        /// <summary>
        /// Attempts to parse input configuration string (presumably string or json array of strings) into enumeration of strings.
        /// </summary>
        /// <param name="args">Input configuration string.</param>
        /// <returns>Enumeration of parsed tokens.</returns>
        /// <exception cref="ConfigurationException">Thrown on unexpected input - not a valid json string or array of string or an empty array.</exception>
        public static IEnumerable<string> ParseArrayOfConstraintStrings(this string? args)
        {
            JToken token = ParseConstraintJToken(args);

            if (token.Type == JTokenType.String)
            {
                return new[] { token.Value<string>() ?? throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_ArgumentHasEmptyString, args)) };
            }

            JArray array = token.ToConstraintsJArray(args, true);

            return array.Values<string>().Select(value =>
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_ArgumentHasEmptyString, args));
                }

                return value!;
            });
        }

        /// <summary>
        /// Attempts to parse input configuration string (presumably string or json array of strings) into enumeration of JObjects.
        /// </summary>
        /// <param name="args">Input configuration string.</param>
        /// <returns>Enumeration of parsed JObject tokens.</returns>
        /// <exception cref="ConfigurationException">Thrown on unexpected input - not a valid json array or an empty array.</exception>
        public static IEnumerable<JObject> ParseArrayOfConstraintJObjects(this string? args)
        {
            JToken token = ParseConstraintJToken(args);
            JArray array = token.ToConstraintsJArray(args, false);

            return array.Select(value =>
            {
                if (value is not JObject jObj)
                {
                    throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_InvalidJsonArray_Objects, args));
                }

                return jObj;
            });
        }

        /// <summary>
        /// Attempts to parse given string and return the version specification (throws <see cref="ConfigurationException"/> if unsuccessful).
        /// checks version in the following order:
        /// NuGet exact version
        /// NuGet floating version
        /// NuGet version range
        /// Legacy template engine exact version
        /// Legacy template engine version range.
        /// </summary>
        /// <param name="versionString">Version string to be parsed.</param>
        /// <returns>IVersionSpecification instance representing the given string representation of the version.</returns>
        /// <exception cref="ConfigurationException">Thrown if given string is not recognized as any valid version format.</exception>
        public static IVersionSpecification ParseVersionSpecification(this string versionString)
        {
            IVersionSpecification? versionInstance = null;

            if (NuGetVersionSpecification.TryParse(versionString, out NuGetVersionSpecification? exactNuGetVersion))
            {
                versionInstance = exactNuGetVersion;
            }
            else if (NuGetFloatRangeSpecification.TryParse(versionString, out NuGetFloatRangeSpecification? floatVersion))
            {
                versionInstance = floatVersion;
            }
            else if (NuGetVersionRangeSpecification.TryParse(versionString, out NuGetVersionRangeSpecification? rangeNuGetVersion))
            {
                versionInstance = rangeNuGetVersion;
            }
            else if (ExactVersionSpecification.TryParse(versionString, out IVersionSpecification? exactVersion))
            {
                versionInstance = exactVersion;
            }
            else if (RangeVersionSpecification.TryParse(versionString, out IVersionSpecification? rangeVersion))
            {
                versionInstance = rangeVersion;
            }

            if (versionInstance == null)
            {
                throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_InvalidVersion, versionString));
            }

            return versionInstance;
        }

        private static JToken ParseConstraintJToken(this string? args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                throw new ConfigurationException(LocalizableStrings.Constraint_Error_ArgumentsNotSpecified);
            }

            JToken? token;
            try
            {
                token = JToken.Parse(args!);
            }
            catch (Exception e)
            {
                throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_InvalidJson, args), e);
            }

            return token;
        }

        private static JArray ToConstraintsJArray(this JToken token, string? args, bool isStringTypeAllowed)
        {
            if (token is not JArray array)
            {
                throw new ConfigurationException(string.Format(
                    isStringTypeAllowed
                        ? LocalizableStrings.Constraint_Error_InvalidJsonType_StringOrArray
                        : LocalizableStrings.Constraint_Error_InvalidJsonType_Array,
                    args));
            }

            if (array.Count == 0)
            {
                throw new ConfigurationException(string.Format(LocalizableStrings.Constraint_Error_ArrayHasNoObjects, args));
            }

            return array;
        }
    }
}

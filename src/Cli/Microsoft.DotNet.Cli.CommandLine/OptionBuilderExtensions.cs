// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extension methods that make it easier to chain option configuration methods when building options.
/// </summary>
public static class OptionBuilderExtensions
{
    extension<T>(T option) where T : Option
    {
        /// <summary>
        /// Forces an option that represents a collection-type to only allow a single
        /// argument per instance of the option. This means that you'd have to
        /// use the option multiple times to pass multiple values.
        /// This prevents ambiguity in parsing when argument tokens may appear after the option.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option"></param>
        /// <returns></returns>
        public T AllowSingleArgPerToken()
        {
            option.AllowMultipleArgumentsPerToken = false;
            return option;
        }


        public T AggregateRepeatedTokens()
        {
            option.AllowMultipleArgumentsPerToken = true;
            return option;
        }

        public T Hide()
        {
            option.Hidden = true;
            return option;
        }
        public T AddCompletions(Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
        {
            option.CompletionSources.Add(completionSource);
            return option;
        }
    }
}

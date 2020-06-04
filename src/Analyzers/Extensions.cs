// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public static class Extensions
    {
        public static Task ForEachAsync<T>(this IEnumerable<T> enumerable,
                                           Func<T, CancellationToken, Task> action,
                                           CancellationToken cancellationToken = default)
            => Task.WhenAll(enumerable
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .WithCancellation(cancellationToken)
                .Select(x => action(x, cancellationToken)));

        public static bool Any(this SolutionChanges solutionChanges)
            => solutionChanges.GetProjectChanges().Any(x => x.GetChangedDocuments().Any());

        public static bool TryCreateInstance<T>(this Type type, [NotNullWhen(returnValue: true)] out T? instance) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(new Type[] { });

                instance = defaultCtor != null
                    ? (T)Activator.CreateInstance(
                        type,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        args: null,
                        culture: null)
                    : null;

                return instance is object;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instrance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
        }
    }
}

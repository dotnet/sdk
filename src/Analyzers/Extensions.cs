// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public static class Extensions
    {
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

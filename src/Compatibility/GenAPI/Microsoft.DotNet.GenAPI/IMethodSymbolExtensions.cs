// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class IMethodSymbolExtensions
    {
        // Compare the equality of two method signatures for the purpose of emitting a "new"
        // keyword on a method's return type. This is *not* meant to be a complete implementation,
        // but rather a heuristic to check that one method may hide another.
        public static bool SignatureEquals(this IMethodSymbol? method, IMethodSymbol? otherMethod)
        {
            if (method is null || otherMethod is null)
            {
                return false;
            }

            if (method.Equals(otherMethod, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (method.Name != otherMethod.Name)
            {
                return false;
            }

            if (method.Arity != otherMethod.Arity || method.Parameters.Length != otherMethod.Parameters.Length)
            {
                return false;
            }

            // compare parameter types
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (!method.Parameters[i].Type.Equals(otherMethod.Parameters[i].Type, SymbolEqualityComparer.Default))
                {
                    return false;
                }
            }

            // TODO: GenAPI does not currently preserve __arglist as a parameter.
            // Add test case for this branch when that is fixed.
            if (method.IsVararg != otherMethod.IsVararg)
            {
                return false;
            }

            return true;
        }

        // Determines whether the declaration can be omitted in favor of a compiler-provided default constructor.
        public static bool CanBeReplacedByImplicitDefaultConstructor(this IMethodSymbol method, ISymbolFilter symbolFilter)
        {
            // ensure this is a parameterless constructor
            if (method.MethodKind != MethodKind.Constructor || !method.Parameters.IsEmpty)
            {
                return false;
            }

            // An implicit replacement would lose the Version2 unsafe calling requirement.
#pragma warning disable RSEXPERIMENTAL006
            if (method.RequiresUnsafeContext)
#pragma warning restore RSEXPERIMENTAL006
            {
                return false;
            }

            // see https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#15115-default-constructors
            // these are the only two accessibilities given to the implicit default constructor, regardless of containing type accessibility.
            if (method.ContainingType.IsAbstract ?
                method.DeclaredAccessibility != Accessibility.Protected :
                method.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            // if the type defines any other instance constructors the default constructor cannot be implicit 
            if (method.ContainingType.InstanceConstructors.Any(c => symbolFilter.Include(c) && !c.Parameters.IsEmpty))
            {
                return false;
            }

            // if the base type does not have a parameterless constructor the default constructor cannot be implicit
            INamedTypeSymbol? baseType = method.ContainingType.BaseType;
            if (baseType != null)
            {
                return method.GetBaseConstructorCandidates(symbolFilter).Any(c => c.Parameters.IsEmpty);
            }

            return true;
        }

        public static IEnumerable<IMethodSymbol> GetBaseConstructorCandidates(this IMethodSymbol method, ISymbolFilter symbolFilter)
        {
            IEnumerable<IMethodSymbol> constructors = method.ContainingType.BaseType?.InstanceConstructors.Where(symbolFilter.Include) ?? [];
#pragma warning disable RSEXPERIMENTAL006
            if (method.ContainingModule.MemorySafetyRulesVersion == MemorySafetyRulesVersion.Version2 && !method.RequiresUnsafeContext)
            {
                // Do not make a safe constructor unsafe just to call a shorter base overload.
                // Use the same candidates for implicit base() calls and explicit initializers.
                constructors = constructors.Where(c => !c.RequiresUnsafeContext);
            }
#pragma warning restore RSEXPERIMENTAL006
            return constructors;
        }
    }
}

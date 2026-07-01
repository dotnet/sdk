// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// Discovers individual test methods from compiled test assemblies using reflection metadata.
/// Produces fully-qualified method names suitable for use in --filter expressions.
/// </summary>
internal static class TestMethodDiscovery
{
    /// <summary>
    /// Information about a discovered test method.
    /// </summary>
    internal readonly record struct TestMethodInfo(string FullyQualifiedName, string AssemblyPath)
    {
        /// <summary>
        /// Gets just the class portion (Namespace.Class) for grouping.
        /// </summary>
        public string ClassName
        {
            get
            {
                var lastDot = FullyQualifiedName.LastIndexOf('.');
                return lastDot > 0 ? FullyQualifiedName[..lastDot] : FullyQualifiedName;
            }
        }
    }

    /// <summary>
    /// Discovers all public test methods from the given assembly.
    /// A test method is defined as a public method with at least one custom attribute
    /// on a public, non-abstract, non-generic class.
    /// </summary>
    public static List<TestMethodInfo> DiscoverTestMethods(string assemblyPath)
    {
        var methods = new List<TestMethodInfo>();

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (!ShouldIncludeType(reader, type))
                continue;

            var typeName = GetFullTypeName(reader, type);

            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (!IsTestMethod(reader, method))
                    continue;

                var methodName = reader.GetString(method.Name);
                methods.Add(new TestMethodInfo($"{typeName}.{methodName}", assemblyPath));
            }
        }

        methods.Sort((a, b) => string.Compare(a.FullyQualifiedName, b.FullyQualifiedName, StringComparison.Ordinal));
        return methods;
    }

    private static bool ShouldIncludeType(MetadataReader reader, TypeDefinition type)
    {
        var isPublic =
            TypeAttributes.Public == (type.Attributes & TypeAttributes.VisibilityMask) ||
            TypeAttributes.NestedPublic == (type.Attributes & TypeAttributes.VisibilityMask);

        if (!isPublic ||
            TypeAttributes.Abstract == (type.Attributes & TypeAttributes.Abstract) ||
            type.GetGenericParameters().Count != 0 ||
            TypeAttributes.Class != (type.Attributes & TypeAttributes.ClassSemanticsMask))
        {
            return false;
        }

        return IsValidIdentifier(reader, type.Name);
    }

    private static bool IsTestMethod(MetadataReader reader, MethodDefinition method)
    {
        if (method.GetCustomAttributes().Count == 0)
            return false;

        if (MethodAttributes.Public != (method.Attributes & MethodAttributes.MemberAccessMask))
            return false;

        return IsValidIdentifier(reader, method.Name);
    }

    private static bool IsValidIdentifier(MetadataReader reader, StringHandle handle)
    {
        var name = reader.GetString(handle);
        for (int i = 0; i < name.Length; i++)
        {
            switch (name[i])
            {
                case '<':
                case '>':
                case '$':
                    return false;
            }
        }
        return true;
    }

    private static string GetFullTypeName(MetadataReader reader, TypeDefinition type)
    {
        var typeName = reader.GetString(type.Name);

        if (TypeAttributes.NestedPublic == (type.Attributes & TypeAttributes.NestedPublic))
        {
            var declaringType = reader.GetTypeDefinition(type.GetDeclaringType());
            var declaringTypeName = GetFullTypeName(reader, declaringType);
            return $"{declaringTypeName}+{typeName}";
        }

        var ns = reader.GetString(type.Namespace);
        return string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
    }
}

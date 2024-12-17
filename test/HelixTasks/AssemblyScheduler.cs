// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public readonly struct AssemblyPartitionInfo
    {
        internal readonly string AssemblyPath;
        internal readonly string DisplayName;
        internal readonly string ClassListArgumentString;

        public AssemblyPartitionInfo(
            string assemblyPath,
            string displayName,
            string classListArgumentString)
        {
            AssemblyPath = assemblyPath;
            DisplayName = displayName;
            ClassListArgumentString = classListArgumentString;
        }

        public AssemblyPartitionInfo(string assemblyPath)
        {
            AssemblyPath = assemblyPath;
            DisplayName = Path.GetFileName(assemblyPath);
            ClassListArgumentString = string.Empty;
        }

        public override string ToString() => DisplayName;
    }

    public sealed class AssemblyScheduler
    {

        private readonly struct TypeInfo
        {
            internal readonly string FullName;
            internal readonly int MethodCount;

            internal TypeInfo(string fullName, int methodCount)
            {
                FullName = fullName;
                MethodCount = methodCount;
            }
        }

        private readonly struct Partition
        {
            internal readonly string AssemblyPath;
            internal readonly int Id;
            internal readonly List<TypeInfo> TypeInfoList;

            internal Partition(string assemblyPath, int id, List<TypeInfo> typeInfoList)
            {
                AssemblyPath = assemblyPath;
                Id = id;
                TypeInfoList = typeInfoList;
            }
        }

        private sealed class AssemblyInfoBuilder
        {
            private readonly List<Partition> _partitionList = new();
            private readonly List<AssemblyPartitionInfo> _assemblyInfoList = new();
            private readonly StringBuilder _builder = new();
            private readonly string _assemblyPath;
            private readonly int _methodLimit;
            private int _currentId;
            private List<TypeInfo> _currentTypeInfoList = new();

            private AssemblyInfoBuilder(string assemblyPath, int methodLimit)
            {
                _assemblyPath = assemblyPath;
                _methodLimit = methodLimit;
            }

            internal static void Build(string assemblyPath, int methodLimit, List<TypeInfo> typeInfoList, out List<Partition> partitionList, out List<AssemblyPartitionInfo> assemblyInfoList, bool netFramework = false)
            {
                var builder = new AssemblyInfoBuilder(assemblyPath, methodLimit);
                builder.Build(typeInfoList);
                partitionList = builder._partitionList;
                assemblyInfoList = builder._assemblyInfoList;
            }

            private void Build(List<TypeInfo> typeInfoList)
            {
                BeginPartition();

                foreach (var typeInfo in typeInfoList)
                {
                    _currentTypeInfoList.Add(typeInfo);

                    if (_builder.Length > 0)
                    {
                        _builder.Append("|");
                    }
                    _builder.Append($@"{typeInfo.FullName}");

                    CheckForPartitionLimit(done: false);
                }

                CheckForPartitionLimit(done: true);
            }

            private void BeginPartition()
            {
                _currentId++;
                _currentTypeInfoList = new List<TypeInfo>();
                _builder.Length = 0;
            }

            private void CheckForPartitionLimit(bool done)
            {
                if (done)
                {
                    // The builder is done looking at types.  If there are any TypeInfo that have not
                    // been added to a partition then do it now.
                    if (_currentTypeInfoList.Count > 0)
                    {
                        FinishPartition();
                    }

                    return;
                }

                // One item we have to consider here is the maximum command line length in 
                // Windows which is 32767 characters (XP is smaller but don't care).  Once
                // we get close then create a partition and move on. 
                if (_currentTypeInfoList.Sum(x => x.MethodCount) >= _methodLimit ||
                    _builder.Length > 25000)
                {
                    FinishPartition();
                    BeginPartition();
                }
            }

            private void FinishPartition()
            {
                var assemblyName = Path.GetFileName(_assemblyPath);
                var displayName = $"{assemblyName}.{_currentId}";
                var assemblyInfo = new AssemblyPartitionInfo(
                    _assemblyPath,
                    displayName,
                    _builder.ToString());

                _partitionList.Add(new Partition(_assemblyPath, _currentId, _currentTypeInfoList));
                _assemblyInfoList.Add(assemblyInfo);
            }
        }

        /// <summary>
        /// Default number of methods to include per partition.
        /// </summary>
        public const int DefaultMethodLimit = 2000;

        private readonly int _methodLimit;

        public AssemblyScheduler(int methodLimit = DefaultMethodLimit)
        {
            _methodLimit = methodLimit;
        }

        internal IEnumerable<AssemblyPartitionInfo> Schedule(IEnumerable<string> assemblyPaths)
        {
            var list = new List<AssemblyPartitionInfo>();
            foreach (var assemblyPath in assemblyPaths)
            {
                list.AddRange(Schedule(assemblyPath));
            }

            return list;
        }

        public IEnumerable<AssemblyPartitionInfo> Schedule(string assemblyPath, bool force = false, bool netFramework = false)
        {
            var typeInfoList = GetTypeInfoList(assemblyPath);
            var assemblyInfoList = new List<AssemblyPartitionInfo>();
            var partitionList = new List<Partition>();
            AssemblyInfoBuilder.Build(assemblyPath, _methodLimit, typeInfoList, out partitionList, out assemblyInfoList, netFramework);

            // If the scheduling didn't actually produce multiple partition then send back an unpartitioned
            // representation.
            if (assemblyInfoList.Count == 1 && !force)
            {
                // Logger.Log($"Assembly schedule produced a single partition {assemblyPath}");
                return new[] { CreateAssemblyInfo(assemblyPath) };
            }

            return assemblyInfoList;
        }

        public AssemblyPartitionInfo CreateAssemblyInfo(string assemblyPath)
        {
            return new AssemblyPartitionInfo(assemblyPath);
        }

        private static List<TypeInfo> GetTypeInfoList(string assemblyPath)
        {
            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                return GetTypeInfoList(metadataReader);
            }
        }

        private static List<TypeInfo> GetTypeInfoList(MetadataReader reader)
        {
            var list = new List<TypeInfo>();
            foreach (var handle in reader.MethodDefinitions)
            {
                var method = reader.GetMethodDefinition(handle);

                var name = reader.GetString(method.Name);
                if (!IsValidIdentifier(reader, name))
                {
                    continue;
                }

                if (!ShouldIncludeMethod(reader, method))
                {
                    continue;
                }

                var type = reader.GetTypeDefinition(method.GetDeclaringType());
                var fullName = GetFullName(reader, type) + "." + name;
                list.Add(new TypeInfo(fullName, 1));

            }

            // Ensure we get classes back in a deterministic order.
            list.Sort((x, y) => x.FullName.CompareTo(y.FullName));
            return list;
        }

        /// <summary>
        /// Determine if this method method values passed to xunit. This doesn't check for skipping
        /// Include any tests with the known theory and fact attributes
        /// </summary>
        private static bool ShouldIncludeMethod(MetadataReader reader, MethodDefinition method)
        {
                var methodAttributes = method.GetCustomAttributes();
                bool isTestMethod = false;

                foreach (var attributeHandle in methodAttributes)
                {
                    var attribute = reader.GetCustomAttribute(attributeHandle);
                    var attributeConstructorHandle = attribute.Constructor;
                    MemberReference attributeConstructor;
                    if (attributeConstructorHandle.Kind == HandleKind.MemberReference)
                    {
                        attributeConstructor = reader.GetMemberReference((MemberReferenceHandle)attributeConstructorHandle);
                    }
                    else
                    {
                        continue;
                    }
                    var attributeType = reader.GetTypeReference((TypeReferenceHandle)attributeConstructor.Parent);
                    var attributeTypeName = reader.GetString(attributeType.Name);

                    if (attributeTypeName == "FactAttribute" ||
                        attributeTypeName == "TheoryAttribute" ||
                        attributeTypeName == "CoreMSBuildAndWindowsOnlyFactAttribute" ||
                        attributeTypeName == "CoreMSBuildAndWindowsOnlyTheoryAttribute" ||
                        attributeTypeName == "CoreMSBuildOnlyFactAttribute" ||
                        attributeTypeName == "CoreMSBuildOnlyTheoryAttribute" ||
                        attributeTypeName == "FullMSBuildOnlyFactAttribute" ||
                        attributeTypeName == "FullMSBuildOnlyTheoryAttribute" ||
                        attributeTypeName == "PlatformSpecificFact" ||
                        attributeTypeName == "PlatformSpecificTheory" ||
                        attributeTypeName == "RequiresMSBuildVersionFactAttribute" ||
                        attributeTypeName == "RequiresMSBuildVersionTheoryAttribute" ||
                        attributeTypeName == "RequiresSpecificFrameworkFactAttribute" ||
                        attributeTypeName == "RequiresSpecificFrameworkTheoryAttribute" ||
                        attributeTypeName == "WindowsOnlyRequiresMSBuildVersionFactAttribute" ||
                        attributeTypeName == "WindowsOnlyRequiresMSBuildVersionTheoryAttribute")
                    {
                        isTestMethod = true;
                        break;
                    }
                }
                return isTestMethod;
        }

        private static bool IsValidIdentifier(MetadataReader reader, String name)
        {
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

        private static string GetFullName(MetadataReader reader, TypeDefinition type)
        {
            var typeName = reader.GetString(type.Name);

            if (TypeAttributes.NestedPublic == (type.Attributes & TypeAttributes.NestedPublic))
            {
                // Need to take into account the containing type.
                var declaringType = reader.GetTypeDefinition(type.GetDeclaringType());
                var declaringTypeFullName = GetFullName(reader, declaringType);
                return $"{declaringTypeFullName}+{typeName}";
            }

            var namespaceName = reader.GetString(type.Namespace);
            if (string.IsNullOrEmpty(namespaceName))
            {
                return typeName;
            }

            return $"{namespaceName}.{typeName}";
        }
    }
}

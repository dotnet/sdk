using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.Archive;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class ComponentManager : IComponentManager
    {
        private readonly List<string> _loadLocations = new List<string>();
        private readonly Dictionary<Guid, string> _componentIdToAssemblyQualifiedTypeName = new Dictionary<Guid, string>();
        private readonly Dictionary<Type, List<Guid>> _componentIdsByType;
        private readonly HashSet<Type> _acceptedTypes;
        private readonly SettingsStore _settings;

        private interface ICache
        {
            void AddPart(IIdentifiedComponent component);
        }

        private class Cache<T> : ICache
            where T : IIdentifiedComponent
        {
            public static readonly Cache<T> Instance = new Cache<T>();

            public readonly Dictionary<Guid, T> Parts = new Dictionary<Guid, T>();

            public void AddPart(IIdentifiedComponent component)
            {
                Parts[component.Id] = (T) component;
            }
        }

        public ComponentManager(SettingsStore userSettings)
        {
            _settings = userSettings;
            _loadLocations.AddRange(userSettings.ProbingPaths);

            ReflectionLoadProbingPath.Reset();
            foreach (string loadLocation in _loadLocations)
            {
                ReflectionLoadProbingPath.Add(loadLocation);
            }

            _acceptedTypes = new HashSet<Type>(typeof(IIdentifiedComponent)
                .GetTypeInfo()
                .Assembly
                .GetTypes()
                .Where(x =>
                {
                    TypeInfo info = x.GetTypeInfo();
                    return info.IsInterface && typeof(IIdentifiedComponent).IsAssignableFrom(x);
                }));

            _componentIdsByType = new Dictionary<Type, List<Guid>>();
            HashSet<Guid> allowedIds = new HashSet<Guid>();

            foreach (Type acceptedType in _acceptedTypes)
            {
                List<Guid> bucket;
                if (userSettings.ComponentTypeToGuidList.TryGetValue(acceptedType.FullName, out bucket))
                {
                    _componentIdsByType[acceptedType] = bucket;
                    allowedIds.UnionWith(bucket);
                }
            }

            foreach (KeyValuePair<string, string> entry in userSettings.ComponentGuidToAssemblyQualifiedName)
            {
                Guid componentId;
                if (Guid.TryParse(entry.Key, out componentId) && allowedIds.Contains(componentId))
                {
                    _componentIdToAssemblyQualifiedTypeName[componentId] = entry.Value;
                }
            }

            List<Guid> ids;
            if (!_componentIdsByType.TryGetValue(typeof(IMountPointFactory), out ids))
            {
                _componentIdsByType[typeof(IMountPointFactory)] = ids = new List<Guid>();
            }

            if (!ids.Contains(FileSystemMountPointFactory.FactoryId))
            {
                ids.Add(FileSystemMountPointFactory.FactoryId);
                Cache<IMountPointFactory>.Instance.AddPart(new FileSystemMountPointFactory());
            }

            if (!ids.Contains(ZipFileMountPointFactory.FactoryId))
            {
                ids.Add(ZipFileMountPointFactory.FactoryId);
                Cache<IMountPointFactory>.Instance.AddPart(new ZipFileMountPointFactory());
            }
        }

        public IEnumerable<T> OfType<T>()
            where T : class, IIdentifiedComponent
        {
            List<Guid> ids;
            if (_componentIdsByType.TryGetValue(typeof(T), out ids))
            {
                foreach (Guid id in ids)
                {
                    T component;
                    if (TryGetComponent(id, out component))
                    {
                        yield return component;
                    }
                }
            }
        }

        public void Register(Type type)
        {
            if (!typeof(IIdentifiedComponent).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null || !type.GetTypeInfo().IsClass)
            {
                return;
            }

            IIdentifiedComponent instance = (IIdentifiedComponent) Activator.CreateInstance(type);
            foreach (Type t in _acceptedTypes)
            {
                if (t.IsAssignableFrom(type))
                {
                    FieldInfo instanceField = typeof(Cache<>).MakeGenericType(t).GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    ICache cache = (ICache) instanceField.GetValue(null);
                    cache.AddPart(instance);
                    _componentIdToAssemblyQualifiedTypeName[instance.Id] = type.AssemblyQualifiedName;
                    _settings.ComponentGuidToAssemblyQualifiedName[instance.Id.ToString()] = type.AssemblyQualifiedName;

                    List<Guid> ids;
                    if (!_componentIdsByType.TryGetValue(t, out ids))
                    {
                        _componentIdsByType[t] = ids = new List<Guid>();
                    }

                    ids.Add(instance.Id);

                    if (!_settings.ComponentTypeToGuidList.TryGetValue(t.FullName, out ids))
                    {
                        _settings.ComponentTypeToGuidList[t.FullName] = ids = new List<Guid>();
                    }

                    ids.Add(instance.Id);
                    _settings.Save();
                }
            }
        }

        public bool TryGetComponent<T>(Guid id, out T component)
            where T : class, IIdentifiedComponent
        {
            if (Cache<T>.Instance.Parts.TryGetValue(id, out component))
            {
                return true;
            }

            string assemblyQualifiedName;
            if (_componentIdToAssemblyQualifiedTypeName.TryGetValue(id, out assemblyQualifiedName))
            {
                Type t = TypeEx.GetType(assemblyQualifiedName);
                component = Activator.CreateInstance(t) as T;

                if (component != null)
                {
                    Cache<T>.Instance.AddPart(component);
                    return true;
                }

            }

            return false;
        }
    }
}
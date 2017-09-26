using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private readonly Dictionary<Type, HashSet<Guid>> _componentIdsByType;
        private readonly SettingsStore _settings;
        private readonly ISettingsLoader _loader;

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
                Parts[component.Id] = (T)component;
            }
        }

        public ComponentManager(ISettingsLoader loader, SettingsStore userSettings)
        {
            _loader = loader;
            _settings = userSettings;
            _loadLocations.AddRange(userSettings.ProbingPaths);

            ReflectionLoadProbingPath.Reset();
            foreach (string loadLocation in _loadLocations)
            {
                ReflectionLoadProbingPath.Add(loadLocation);
            }

            _componentIdsByType = new Dictionary<Type, HashSet<Guid>>();
            HashSet<Guid> allowedIds = new HashSet<Guid>();

            foreach (KeyValuePair<string, HashSet<Guid>> bucket in userSettings.ComponentTypeToGuidList)
            {
                allowedIds.UnionWith(bucket.Value);
            }

            foreach (KeyValuePair<string, string> entry in userSettings.ComponentGuidToAssemblyQualifiedName)
            {
                if (Guid.TryParse(entry.Key, out Guid componentId) && allowedIds.Contains(componentId))
                {
                    _componentIdToAssemblyQualifiedTypeName[componentId] = entry.Value;
                }
            }

            if (!_componentIdsByType.TryGetValue(typeof(IMountPointFactory), out HashSet<Guid> ids))
            {
                _componentIdsByType[typeof(IMountPointFactory)] = ids = new HashSet<Guid>();
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

            foreach (KeyValuePair<Guid, Func<Type>> components in _loader.EnvironmentSettings.Host.BuiltInComponents)
            {
                if (!ids.Contains(components.Key))
                {
                    RegisterType(components.Value());
                }
            }
        }

        public IEnumerable<T> OfType<T>()
            where T : class, IIdentifiedComponent
        {
            if (!_componentIdsByType.TryGetValue(typeof(T), out HashSet<Guid> ids))
            {
                if (_settings.ComponentTypeToGuidList.TryGetValue(typeof(T).FullName, out ids))
                {
                    _componentIdsByType[typeof(T)] = ids;
                }
                else
                {
                    yield break;
                }
            }

            foreach (Guid id in ids)
            {
                if (TryGetComponent(id, out T component))
                {
                    yield return component;
                }
            }
        }

        // Attempt to register the type, and then save the settings.
        public void Register(Type type)
        {
            if (RegisterType(type))
            {
                Save();
            }
        }

        // Attempt to register every type in the typeList
        // Save once at the end if anything was registered.
        public void RegisterMany(IEnumerable<Type> typeList)
        {
            bool anyRegistered = false;

            foreach (Type type in typeList)
            {
                anyRegistered |= RegisterType(type);
            }

            if (anyRegistered)
            {
                Save();
            }
        }

        // This method does not save the settings, it just registers into the memory cache.
        private bool RegisterType(Type type)
        {
            if (!typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(type) || type.GetTypeInfo().GetConstructor(Type.EmptyTypes) == null || !type.GetTypeInfo().IsClass)
            {
                return false;
            }

            IReadOnlyList<Type> registerFor = type.GetTypeInfo().ImplementedInterfaces.Where(x => x != typeof(IIdentifiedComponent) && typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(x)).ToList();
            if (registerFor.Count == 0)
            {
                return false;
            }

            IIdentifiedComponent instance = (IIdentifiedComponent)Activator.CreateInstance(type);

            foreach (Type t in registerFor)
            {
                FieldInfo instanceField = typeof(Cache<>).MakeGenericType(t).GetTypeInfo().GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                ICache cache = (ICache)instanceField.GetValue(null);
                cache.AddPart(instance);

                _componentIdToAssemblyQualifiedTypeName[instance.Id] = type.AssemblyQualifiedName;
                _settings.ComponentGuidToAssemblyQualifiedName[instance.Id.ToString()] = type.AssemblyQualifiedName;

                if (!_componentIdsByType.TryGetValue(t, out HashSet<Guid> ids))
                {
                    _componentIdsByType[t] = ids = new HashSet<Guid>();
                }

                ids.Add(instance.Id);

                if (!_settings.ComponentTypeToGuidList.TryGetValue(t.FullName, out ids))
                {
                    _settings.ComponentTypeToGuidList[t.FullName] = ids = new HashSet<Guid>();
                }

                ids.Add(instance.Id);
            }

            return true;
        }

        private void Save()
        {
            bool successfulWrite = false;
            const int maxAttempts = 10;
            int attemptCount = 0;

            while (!successfulWrite && attemptCount++ < maxAttempts)
            {
                try
                {
                    _loader.Save();
                    successfulWrite = true;
                }
                catch (IOException)
                {
                    Task.Delay(10).Wait();
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

            if (_componentIdToAssemblyQualifiedTypeName.TryGetValue(id, out string assemblyQualifiedName))
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

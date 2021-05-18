// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;
#if !NETFULL
using System.Runtime.Loader;
#endif

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class ComponentManager : IComponentManager
    {
        private readonly List<string> _loadLocations = new List<string>();
        private readonly Dictionary<Guid, string> _componentIdToAssemblyQualifiedTypeName = new Dictionary<Guid, string>();
        private readonly Dictionary<Type, HashSet<Guid>> _componentIdsByType;
        private readonly SettingsStore _settings;
        private readonly SettingsFilePaths _paths;

        public ComponentManager(IEngineEnvironmentSettings engineEnvironmentSettings)
        {
            _paths = new SettingsFilePaths(engineEnvironmentSettings);
            _settings = SettingsStore.Load(engineEnvironmentSettings, _paths);
            _loadLocations.AddRange(_settings.ProbingPaths);

            ReflectionLoadProbingPath.Reset();
            foreach (string loadLocation in _loadLocations)
            {
                ReflectionLoadProbingPath.Add(loadLocation);
            }

            _componentIdsByType = new Dictionary<Type, HashSet<Guid>>();

            foreach (KeyValuePair<string, HashSet<Guid>> bucket in _settings.ComponentTypeToGuidList)
            {
                Type interfaceType = Type.GetType(bucket.Key);
                if (interfaceType != null)
                {
                    _componentIdsByType[interfaceType] = bucket.Value;
                }
            }

            foreach (KeyValuePair<string, string> entry in _settings.ComponentGuidToAssemblyQualifiedName)
            {
                if (Guid.TryParse(entry.Key, out Guid componentId))
                {
                    _componentIdToAssemblyQualifiedTypeName[componentId] = entry.Value;
                }
            }

            foreach (var (interfaceType, instance) in engineEnvironmentSettings.Host.BuiltInComponents)
            {
                AddComponent(interfaceType, instance);
            }
        }

        internal Dictionary<Type, Dictionary<Guid, object>> ComponentCache { get; } = new Dictionary<Type, Dictionary<Guid, object>>();

        public IEnumerable<T> OfType<T>()
            where T : class, IIdentifiedComponent
        {
            lock (_componentIdToAssemblyQualifiedTypeName)
            {
                if (!_componentIdsByType.TryGetValue(typeof(T), out HashSet<Guid> ids))
                {
                    if (_settings.ComponentTypeToGuidList.TryGetValue(typeof(T).AssemblyQualifiedName, out ids))
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

        public bool TryGetComponent<T>(Guid id, out T component)
                    where T : class, IIdentifiedComponent
        {
            lock (_componentIdToAssemblyQualifiedTypeName)
            {
                component = default;
                if (ComponentCache.TryGetValue(typeof(T), out Dictionary<Guid, object> typeCache) && typeCache != null
                    && typeCache.TryGetValue(id, out object resolvedComponent) && resolvedComponent != null && resolvedComponent is T t)
                {
                    component = t;
                    return true;
                }

                if (_componentIdToAssemblyQualifiedTypeName.TryGetValue(id, out string assemblyQualifiedName))
                {
                    Type type = GetType(assemblyQualifiedName);
                    component = Activator.CreateInstance(type) as T;

                    if (component != null)
                    {
                        AddComponent(typeof(T), component);
                        return true;
                    }
                }
                return false;
            }
        }

        internal void AddProbingPath(string probeIn)
        {
            const int maxAttempts = 10;
            int attemptCount = 0;
            bool successfulWrite = false;

            while (!successfulWrite && attemptCount++ < maxAttempts)
            {
                if (!_settings.ProbingPaths.Add(probeIn))
                {
                    return;
                }

                try
                {
                    Save();
                    successfulWrite = true;
                }
                catch
                {
                    Task.Delay(10).Wait();
                }
            }
        }

        // This method does not save the settings, it just registers into the memory cache.
        private bool RegisterType(Type type)
        {
            if (!typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(type) || type.GetTypeInfo().GetConstructor(Type.EmptyTypes) == null || !type.GetTypeInfo().IsClass)
            {
                return false;
            }

            IReadOnlyList<Type> interfaceTypesToRegisterFor = type.GetTypeInfo().ImplementedInterfaces.Where(x => x != typeof(IIdentifiedComponent) && typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(x)).ToList();
            if (interfaceTypesToRegisterFor.Count == 0)
            {
                return false;
            }

            lock (_componentIdToAssemblyQualifiedTypeName)
            {
                IIdentifiedComponent instance = (IIdentifiedComponent)Activator.CreateInstance(type);

                foreach (Type interfaceType in interfaceTypesToRegisterFor)
                {
                    AddComponent(interfaceType, instance);
                    AddProbingPath(Path.GetDirectoryName(type.Assembly.Location));

                    _componentIdToAssemblyQualifiedTypeName[instance.Id] = type.AssemblyQualifiedName;
                    _settings.ComponentGuidToAssemblyQualifiedName[instance.Id.ToString()] = type.AssemblyQualifiedName;

                    if (!_componentIdsByType.TryGetValue(interfaceType, out HashSet<Guid> idsForInterfaceType))
                    {
                        _componentIdsByType[interfaceType] = idsForInterfaceType = new HashSet<Guid>();
                    }
                    idsForInterfaceType.Add(instance.Id);

                    // for backwards compat & cleanup from when the keys were interfaceType.FullName
                    if (_settings.ComponentTypeToGuidList.TryGetValue(interfaceType.FullName, out HashSet<Guid> idsFromOldStyleKey))
                    {
                        _settings.ComponentTypeToGuidList.Remove(interfaceType.FullName);
                    }

                    if (!_settings.ComponentTypeToGuidList.TryGetValue(interfaceType.AssemblyQualifiedName, out HashSet<Guid> idsForInterfaceTypeForSettings))
                    {
                        _settings.ComponentTypeToGuidList[interfaceType.AssemblyQualifiedName] = idsForInterfaceTypeForSettings = new HashSet<Guid>();
                    }
                    idsForInterfaceTypeForSettings.Add(instance.Id);

                    // for backwards compat & cleanup from when the keys were interfaceType.FullName
                    if (idsFromOldStyleKey != null)
                    {
                        idsForInterfaceTypeForSettings.UnionWith(idsFromOldStyleKey);
                    }
                }

                return true;
            }
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        internal void Save()
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            bool successfulWrite = false;
            const int maxAttempts = 10;
            int attemptCount = 0;

            while (!successfulWrite && attemptCount++ < maxAttempts)
            {
                try
                {
                    JObject serialized = JObject.FromObject(_settings);
                    _paths.WriteAllText(_paths.SettingsFile, serialized.ToString());
                    successfulWrite = true;
                }
                catch (IOException)
                {
                    Task.Delay(10).Wait();
                }
            }
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        public void AddComponent(Type type, IIdentifiedComponent component)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            if (!type.IsAssignableFrom(component.GetType()))
            {
                throw new ArgumentException($"{component.GetType().Name} should be assignable from {type.Name} type", nameof(type));
            }

            if (!ComponentCache.TryGetValue(type, out Dictionary<Guid, object> typeCache))
            {
                typeCache = new Dictionary<Guid, object>();
                ComponentCache[type] = typeCache;
            }
            typeCache[component.Id] = component;

            if (!_componentIdsByType.TryGetValue(type, out HashSet<Guid> ids))
            {
                ids = new HashSet<Guid>();
                _componentIdsByType[type] = ids;
            }
            ids.Add(component.Id);
        }

        private Type GetType(string typeName)
        {
            int commaIndex = typeName.IndexOf(',');
            if (commaIndex < 0)
            {
                return Type.GetType(typeName);
            }

            string asmName = typeName.Substring(commaIndex + 1).Trim();

            if (!ReflectionLoadProbingPath.HasLoaded(asmName))
            {
                AssemblyName name = new AssemblyName(asmName);
#if !NETFULL
                AssemblyLoadContext.Default.LoadFromAssemblyName(name);
#else
                AppDomain.CurrentDomain.Load(name);
#endif
            }

            return Type.GetType(typeName);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockComponentManager : IComponentManager
    {
        private static readonly TypeInfo ComponentType = typeof(IIdentifiedComponent).GetTypeInfo();

        public void Register(Type type)
        {
            if (!ComponentType.IsAssignableFrom(type))
            {
                return;
            }

            List<Type> types = new List<Type>();
            foreach (Type t in type.GetTypeInfo().GetInterfaces())
            {
                if (ComponentType.IsAssignableFrom(t))
                {
                    Type strongCache = typeof(Cache<>).MakeGenericType(t);
                    MethodInfo method = strongCache.GetTypeInfo().GetMethod(nameof(Cache<IIdentifiedComponent>.Get), BindingFlags.Public | BindingFlags.Static);
                    ICache cache = (ICache)method.Invoke(null, new object[] { this });
                    IIdentifiedComponent c = (IIdentifiedComponent)Activator.CreateInstance(type);
                    cache.Register(c.Id, c);
                }
            }
        }

        public void RegisterMany(IEnumerable<Type> typeList)
        {
            foreach (Type type in typeList)
            {
                Register(type);
            }
        }

        IEnumerable<T> IComponentManager.OfType<T>()
        {
            return Cache<T>.Get(this).Lookup.Values;
        }

        bool IComponentManager.TryGetComponent<T>(Guid id, out T component)
        {
            return Cache<T>.Get(this).Lookup.TryGetValue(id, out component);
        }

        private interface ICache
        {
            void Register(Guid id, IIdentifiedComponent o);
        }

        private class Cache<T> : ICache
            where T : IIdentifiedComponent
        {
            private static readonly ConcurrentDictionary<IComponentManager, Cache<T>> InstanceLookup = new ConcurrentDictionary<IComponentManager, Cache<T>>();

            public static Cache<T> Get(IComponentManager self)
            {
                return InstanceLookup.GetOrAdd(self, x => new Cache<T>());
            }

            public readonly Dictionary<Guid, T> Lookup = new Dictionary<Guid, T>();

            public void Register(Guid id, IIdentifiedComponent o)
            {
                Lookup[id] = (T)o;
            }
        }
    }
}

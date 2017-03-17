using System;
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
                if(ComponentType.IsAssignableFrom(t))
                {
                    Type strongCache = typeof(Cache<>).MakeGenericType(t);
                    FieldInfo field = strongCache.GetTypeInfo().GetField(nameof(Cache<IIdentifiedComponent>.Instance), BindingFlags.Public | BindingFlags.Static);
                    ICache cache = (ICache)field.GetValue(null);
                    IIdentifiedComponent c = (IIdentifiedComponent)Activator.CreateInstance(type);
                    cache.Register(c.Id, c);
                }
            }
        }

        IEnumerable<T> IComponentManager.OfType<T>()
        {
            return Cache<T>.Instance.Lookup.Values;
        }

        bool IComponentManager.TryGetComponent<T>(Guid id, out T component)
        {
            return Cache<T>.Instance.Lookup.TryGetValue(id, out component);
        }

        private interface ICache
        {
            void Register(Guid id, IIdentifiedComponent o);
        }

        private class Cache<T> : ICache
            where T : IIdentifiedComponent
        {
            public static Cache<T> Instance = new Cache<T>();

            public readonly Dictionary<Guid, T> Lookup = new Dictionary<Guid, T>();

            public void Register(Guid id, IIdentifiedComponent o)
            {
                Lookup[id] = (T)o;
            }
        }
    }
}

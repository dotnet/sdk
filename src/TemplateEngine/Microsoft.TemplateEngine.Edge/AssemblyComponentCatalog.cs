// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    public class AssemblyComponentCatalog : IReadOnlyList<KeyValuePair<Guid, Func<Type>>>
    {
        private readonly IReadOnlyList<Assembly> _assemblies;
        private IReadOnlyList<KeyValuePair<Guid, Func<Type>>> _lookup;

        public AssemblyComponentCatalog(IReadOnlyList<Assembly> assemblies)
        {
            _assemblies = assemblies;
        }

        public int Count
        {
            get
            {
                EnsureLoaded();
                return _lookup.Count;
            }
        }

        public KeyValuePair<Guid, Func<Type>> this[int index]
        {
            get
            {
                EnsureLoaded();
                return _lookup[index];
            }
        }

        public IEnumerator<KeyValuePair<Guid, Func<Type>>> GetEnumerator()
        {
            EnsureLoaded();
            return _lookup.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void EnsureLoaded()
        {
            if (_lookup != null)
            {
                return;
            }

            Dictionary<Guid, Func<Type>> builder = new Dictionary<Guid, Func<Type>>();

            foreach (Assembly asm in _assemblies)
            {
                foreach (Type type in asm.GetTypes())
                {
                    if (!typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(type) || type.GetTypeInfo().GetConstructor(Type.EmptyTypes) == null || !type.GetTypeInfo().IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    IReadOnlyList<Type> registerFor = type.GetTypeInfo().ImplementedInterfaces.Where(x => x != typeof(IIdentifiedComponent) && typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(x)).ToList();
                    if (registerFor.Count == 0)
                    {
                        continue;
                    }

                    IIdentifiedComponent instance = (IIdentifiedComponent)Activator.CreateInstance(type);
                    builder[instance.Id] = () => type;
                }
            }

            _lookup = builder.ToList();
        }
    }
}

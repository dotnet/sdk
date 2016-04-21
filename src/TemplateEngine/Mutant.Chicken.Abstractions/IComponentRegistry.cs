using System;
using System.Collections.Generic;
using System.Reflection;

namespace Mutant.Chicken.Abstractions
{
    public interface IComponentRegistry
    {
        bool TryGetNamedComponent<TComponent>(string name, out TComponent source);

        IEnumerable<TComponent> OfType<TComponent>();

        void RemoveAll(Assembly asm);

        void Register<T>(Type type);
    }
}
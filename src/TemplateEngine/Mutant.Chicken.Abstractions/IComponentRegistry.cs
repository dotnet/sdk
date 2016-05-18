using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface IComponentRegistry
    {
        bool IsUninitialized { get; }

        bool TryGetNamedComponent<TComponent>(string name, out TComponent source);

        IEnumerable<TComponent> OfType<TComponent>();
    }
}
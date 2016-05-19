using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IComponentRegistry
    {
        void ForceReinitialize();

        bool TryGetNamedComponent<TComponent>(string name, out TComponent source);

        IEnumerable<TComponent> OfType<TComponent>();
    }
}
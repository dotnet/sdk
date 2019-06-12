using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplateSearchCoordinator
    {
        // Implementations should search and deal with any necessary user interactions.
        Task<bool> CoordinateSearchAsync();
    }
}

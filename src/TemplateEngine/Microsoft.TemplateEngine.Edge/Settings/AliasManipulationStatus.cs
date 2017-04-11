
namespace Microsoft.TemplateEngine.Edge.Settings
{
    public enum AliasManipulationStatus
    {
        Created,
        Removed,
        RemoveNonExistentFailed,    // for trying to remove an alias that didn't exist.
        Updated,
        WouldCreateCycle,
        InvalidInput
    }
}

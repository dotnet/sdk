namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ICustomOperationModel
    {
        string Type { get; }

        string Condition { get; }
    }
}

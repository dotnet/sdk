namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IValueReadEventArgs
    {
        string Key { get; set; }

        object Value { get; set; }
    }
}

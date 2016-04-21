namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateParameter
    {
        string Documentation { get; }

        string Name { get; }

        TemplateParameterPriority Priority { get; }

        string Type { get; }

        bool IsName { get; }
    }

    public enum TemplateParameterPriority
    {
        Required,
        Suggested,
        Optional,
        AddOn
    }
}
namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateSourceEntry
    {
        string Name { get; }

        string FullPath { get; }

        TemplateSourceEntryKind Kind { get; }
    }
}
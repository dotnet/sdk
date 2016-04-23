namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateSource : IComponent
    {
        IDisposable<ITemplateSourceFolder> RootFor(string location);

        bool CanHandle(string location);
    }
}

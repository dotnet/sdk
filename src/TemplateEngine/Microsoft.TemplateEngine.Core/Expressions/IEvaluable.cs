namespace Microsoft.TemplateEngine.Core.Expressions
{
    public interface IEvaluable
    {
        bool IsFull { get; }

        bool IsIndivisible { get; }

        IEvaluable Parent { get; set; }

        object Evaluate();

        bool TryAccept(IEvaluable child);
    }
}

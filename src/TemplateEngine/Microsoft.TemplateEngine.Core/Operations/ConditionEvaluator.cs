using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public delegate bool ConditionEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted);
}

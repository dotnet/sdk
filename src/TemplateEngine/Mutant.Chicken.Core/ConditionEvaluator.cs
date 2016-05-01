namespace Mutant.Chicken.Core
{
    public delegate bool ConditionEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition);
}
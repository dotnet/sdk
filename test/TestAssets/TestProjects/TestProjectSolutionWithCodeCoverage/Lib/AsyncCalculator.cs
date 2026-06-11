namespace Lib
{
    public class AsyncCalculator
    {
        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Yield();
            return a + b;
        }

        public async Task<int> SubtractAsync(int a, int b)
        {
            await Task.Delay(1);
            return a - b;
        }

        public async Task<int> MultiplyAsync(int a, int b)
        {
            await Task.Yield();
            int result = 0;
            for (int i = 0; i < b; i++)
            {
                await Task.Yield();
                result += a;
            }
            return result;
        }

        public async IAsyncEnumerable<int> EnumerateAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }
}

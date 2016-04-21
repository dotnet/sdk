namespace Mutant.Chicken.Demo.Files
{
    public class Test2
    {
#if TEST
        /// <summary>
        /// That's not CHEESE
        /// </summary>
        /// <remarks>It's on the PATH</remarks>
        public int Thing { get; set; }
#endif
    }
}

using System;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal static class ConsoleExtensions
    {
        public static string Verbose(this string text)
        {
            return $"[verbose] {text}";
        }
    }

    /// <summary>
    /// Use this class to do verbose output
    /// </summary>
    internal static class Verbose
    {
        /// <summary>
        /// Defines if verbose output is enabled
        /// </summary>
        internal static bool IsEnabled { get; set; } = false;

        /// <summary>
        /// Writes the output conditionally if verbose mode is enabled
        /// </summary>
        /// <param name="text">text to write</param>
        internal static void WriteLine(string text)
        {
            if (IsEnabled)
            {
                Console.WriteLine(text.Verbose());
            }
        }
    }
}

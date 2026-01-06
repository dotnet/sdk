// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tests
{
    public class AutomaticEncodingRestorerTests
    {
        [WindowsOnlyFact]
        public void OnWindows_WhenUTF8EncodingIsSet_DoesNotRestoreOutputEncoding()
        {
            // Save the current encoding
            var originalEncoding = Console.OutputEncoding;

            try
            {
                // Set to a non-UTF8 encoding initially
                Console.OutputEncoding = Encoding.GetEncoding(1252); // Windows-1252

                // Create and dispose the restorer while UTF-8 is set
                using (var restorer = new AutomaticEncodingRestorer())
                {
                    Console.OutputEncoding = Encoding.UTF8;
                }

                // On Windows, UTF-8 should NOT be restored to the original encoding
                Assert.Equal(Encoding.UTF8.CodePage, Console.OutputEncoding.CodePage);
            }
            finally
            {
                // Restore the original encoding for other tests
                Console.OutputEncoding = originalEncoding;
            }
        }

        [WindowsOnlyFact]
        public void OnWindows_WhenNonUTF8EncodingIsSet_RestoresOutputEncoding()
        {
            // Save the current encoding
            var originalEncoding = Console.OutputEncoding;

            try
            {
                // Set to UTF-8 initially
                Console.OutputEncoding = Encoding.UTF8;

                // Create and dispose the restorer while a non-UTF8 encoding is set
                using (var restorer = new AutomaticEncodingRestorer())
                {
                    Console.OutputEncoding = Encoding.GetEncoding(1252); // Windows-1252
                }

                // Non-UTF8 encoding should be restored to the original (UTF-8)
                Assert.Equal(Encoding.UTF8.CodePage, Console.OutputEncoding.CodePage);
            }
            finally
            {
                // Restore the original encoding for other tests
                Console.OutputEncoding = originalEncoding;
            }
        }

        [UnixOnlyFact]
        public void OnUnix_RestoresOutputEncodingRegardlessOfUTF8()
        {
            // Save the current encoding
            var originalEncoding = Console.OutputEncoding;

            try
            {
                // Set to ASCII initially
                Console.OutputEncoding = Encoding.ASCII;

                // Create and dispose the restorer while UTF-8 is set
                using (var restorer = new AutomaticEncodingRestorer())
                {
                    Console.OutputEncoding = Encoding.UTF8;
                }

                // On Unix, encoding should be restored to ASCII
                Assert.Equal(Encoding.ASCII.CodePage, Console.OutputEncoding.CodePage);
            }
            finally
            {
                // Restore the original encoding for other tests
                Console.OutputEncoding = originalEncoding;
            }
        }
    }
}

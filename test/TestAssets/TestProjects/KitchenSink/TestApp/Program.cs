// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Reflection;
using System.Text;

namespace TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Emit UTF-8 deterministically so the localized (non-ASCII) strings this app prints
            // survive being captured by a test harness. When stdout is redirected, Console defaults
            // its output encoding to the host's console code page (e.g. the OEM code page CP437/CP850
            // on Windows CI agents), which varies by machine. That made the encoding of characters
            // such as the French 'à' non-deterministic and the satellite-assembly test flaky. Forcing
            // UTF-8 here (a no-op on platforms that already default to UTF-8, and BOM-free because
            // Console strips the preamble) pairs with the harness capturing stdout as UTF-8.
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine(TestLibrary.Helper.GetMessage());
            VerifySatelliteAssemblies();
        }

        public static void VerifySatelliteAssemblies()
        {
            PrintCultureResources();
            PrintCultureResourcesInFolder();
        }

        public static void PrintCultureResources()
        {
            var rm = new ResourceManager("TestApp.Strings", typeof(Program).GetTypeInfo().Assembly);

            string[] cultures = new string[] { "", "de", "fr" };

            foreach (var culture in cultures)
            {
                Console.WriteLine(rm.GetString("hello", new CultureInfo(culture)));
            }
        }

        public static void PrintCultureResourcesInFolder()
        {
            var rm = new ResourceManager("TestApp.FolderWithResource.Strings", typeof(Program).GetTypeInfo().Assembly);
            Console.WriteLine(rm.GetString("hello", new CultureInfo("da")));
        }
    }
}

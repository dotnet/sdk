// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.CommandLine;
using GracefulException = Microsoft.DotNet.Cli.Utils.GracefulException;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal static class SdkCheckCommandParser
    {
        public static Command GetCommand()
        {
            try
            {
                return new Command("check", LocalizableStrings.AppFullName);
            }
            catch (FileNotFoundException e)
            {
                throw new GracefulException(string.Format(LocalizableStrings.RuntimePropertyNotFound, e.Message));
            }
            catch (ArgumentException e)
            {
                throw new GracefulException(string.Format(LocalizableStrings.HostFxrCouldNotBeLoaded, e.Message));
            }
        }
    }
}

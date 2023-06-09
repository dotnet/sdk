// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Tasks;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenWeWantAGoodErrorExperience : SdkTest
    {
        public GivenWeWantAGoodErrorExperience(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ConfirmNoDuplicateErrorCodes()
        {
            Type type = typeof(Strings);
            var resourceStrings = type.GetRuntimeProperties();
            HashSet<string> listOfErrorCode = new HashSet<string>();
            foreach (var resource in resourceStrings )
            {
                if ( resource.PropertyType == typeof(string))
                {
                    string resourceString = Strings.GetResourceString(resource.Name);
                    if (resourceString.StartsWith("NETSDK"))
                    {
                        string errorCode = resourceString.Substring(0, 10);
                        Assert.True(listOfErrorCode.Add(errorCode), $"Duplicate error code found: {errorCode}");
                    }
                }
            }
        }
    }
}

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
            List<string> listOfErrorCode = new List<string>();
            foreach (var resource in resourceStrings )
            {
                if ( resource.PropertyType == typeof(string))
                {
                    string errorCode = Strings.GetResourceString(resource.Name).Substring(0, 10);
                    if (errorCode.Contains("NETSDK"))
                    {
                        listOfErrorCode.Add(errorCode);
                    }
                }
            }
            var anyDuplicate = listOfErrorCode.GroupBy(x => x).Where(g => g.Count() > 1);
            Assert.True(anyDuplicate.Count() == 0,$"Duplicate error code found: {anyDuplicate.First().Key}");
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace PackageValidationTestProject
{
    public class Program
    {
#if ForceValidationProblem && !NET
      public void SomeAPINotInCore()
      {
      }
#endif

#if !AddBreakingChange
      public void SomeApiNotInLatestVersion()
      {
      }
#endif

#if ForceStrictModeBaselineValidationProblem
      public void SomeApiOnlyInLatestVersion()
      {
      }
#endif
    }
}

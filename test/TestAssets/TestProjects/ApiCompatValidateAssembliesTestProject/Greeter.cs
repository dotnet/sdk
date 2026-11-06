// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ApiCompatValidateAssembliesTestProject
{
    public class Greeter
    {
        public string Hello(string name) => $"Hello, {name}!";

#if !ForceBreakingChange
        public string Goodbye(string name) => $"Goodbye, {name}!";
#endif

#if AddNewMember
        public string Welcome(string name) => $"Welcome, {name}!";
#endif

#if IncludeExperimentalApis
        [System.Diagnostics.CodeAnalysis.Experimental("TEST001")]
        public string ExperimentalRemoved(string name) => $"Experimental goodbye, {name}!";

        [System.Diagnostics.CodeAnalysis.Experimental("TEST002")]
        public string Promoted(string name) => $"Promoted hello, {name}!";
#elif IncludeStablePromotedApi
        public string Promoted(string name) => $"Promoted hello, {name}!";
#endif
    }
}

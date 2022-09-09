// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public static class Components
    {
        private static readonly CaseChangeMacro s_caseChange = new();
        private static readonly GeneratePortNumberMacro s_generatePortNumberMacro = new();
        private static readonly CoalesceMacro s_coalesceMacro = new();
        private static readonly ConstantMacro s_constantMacro = new();
        private static readonly GuidMacro s_guidMacro = new();
        private static readonly SwitchMacro s_switchMacro = new();
        private static readonly RegexMatchMacro s_regexMatchMacro = new();
        private static readonly RegexMacro s_regexMacro = new();
        private static readonly RandomMacro s_randomMacro = new();
        private static readonly NowMacro s_nowMacro = new();
        private static readonly JoinMacro s_joinMacro = new();

        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IGenerator), new RunnableProjectGenerator()),

                (typeof(IOperationConfig), new BalancedNestingConfig()),
                (typeof(IOperationConfig), new ConditionalConfig()),
                (typeof(IOperationConfig), new FlagsConfig()),
                (typeof(IOperationConfig), new IncludeConfig()),
                (typeof(IOperationConfig), new RegionConfig()),
                (typeof(IOperationConfig), new ReplacementConfig()),

                (typeof(IMacro), s_caseChange),
                (typeof(IDeferredMacro), s_caseChange),
                (typeof(IMacro), s_coalesceMacro),
                (typeof(IDeferredMacro), s_coalesceMacro),
                (typeof(IMacro), s_constantMacro),
                (typeof(IDeferredMacro), s_constantMacro),
                (typeof(IMacro), new EvaluateMacro()),
                (typeof(IMacro), s_generatePortNumberMacro),
                (typeof(IDeferredMacro), s_generatePortNumberMacro),
                (typeof(IMacro), s_guidMacro),
                (typeof(IDeferredMacro), s_guidMacro),
                (typeof(IMacro), s_joinMacro),
                (typeof(IDeferredMacro), s_joinMacro),
                (typeof(IMacro), s_nowMacro),
                (typeof(IDeferredMacro), s_nowMacro),
                (typeof(IMacro), new ProcessValueFormMacro()),
                (typeof(IMacro), s_randomMacro),
                (typeof(IDeferredMacro), s_randomMacro),
                (typeof(IMacro), s_regexMacro),
                (typeof(IDeferredMacro), s_regexMacro),
                (typeof(IMacro), s_regexMatchMacro),
                (typeof(IDeferredMacro), s_regexMatchMacro),
                (typeof(IMacro), s_switchMacro),
                (typeof(IDeferredMacro), s_switchMacro),
            };
    }
}

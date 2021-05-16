// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public static class Components
    {
        private static readonly CaseChangeMacro CaseChange = new CaseChangeMacro();
        private static readonly GeneratePortNumberMacro GeneratePortNumberMacro = new GeneratePortNumberMacro();
        private static readonly CoalesceMacro CoalesceMacro = new CoalesceMacro();
        private static readonly ConstantMacro ConstantMacro = new ConstantMacro();
        private static readonly GuidMacro GuidMacro = new GuidMacro();
        private static readonly SwitchMacro SwitchMacro = new SwitchMacro();
        private static readonly RegexMatchMacro RegexMatchMacro = new RegexMatchMacro();
        private static readonly RegexMacro RegexMacro = new RegexMacro();
        private static readonly RandomMacro RandomMacro = new RandomMacro();
        private static readonly NowMacro NowMacro = new NowMacro();
        private static readonly JoinMacro JoinMacro = new JoinMacro();

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

                (typeof(IMacro), CaseChange),
                (typeof(IDeferredMacro), CaseChange),
                (typeof(IMacro), CoalesceMacro),
                (typeof(IDeferredMacro), CoalesceMacro),
                (typeof(IMacro), ConstantMacro),
                (typeof(IDeferredMacro), ConstantMacro),
                (typeof(IMacro), new EvaluateMacro()),
                (typeof(IMacro), GeneratePortNumberMacro),
                (typeof(IDeferredMacro), GeneratePortNumberMacro),
                (typeof(IMacro), GuidMacro),
                (typeof(IDeferredMacro), GuidMacro),
                (typeof(IMacro), JoinMacro),
                (typeof(IDeferredMacro), JoinMacro),
                (typeof(IMacro), NowMacro),
                (typeof(IDeferredMacro), NowMacro),
                (typeof(IMacro), new ProcessValueFormMacro()),
                (typeof(IMacro), RandomMacro),
                (typeof(IDeferredMacro), RandomMacro),
                (typeof(IMacro), RegexMacro),
                (typeof(IDeferredMacro), RegexMacro),
                (typeof(IMacro), RegexMatchMacro),
                (typeof(IDeferredMacro), RegexMatchMacro),
                (typeof(IMacro), SwitchMacro),
                (typeof(IDeferredMacro), SwitchMacro),
            };
    }
}

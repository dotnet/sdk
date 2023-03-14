// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public static class Components
    {
        private static readonly CaseChangeMacro CaseChange = new();
        private static readonly GeneratePortNumberMacro GeneratePortNumberMacro = new();
        private static readonly CoalesceMacro CoalesceMacro = new();
        private static readonly ConstantMacro ConstantMacro = new();
        private static readonly GuidMacro GuidMacro = new();
        private static readonly SwitchMacro SwitchMacro = new();
        private static readonly RegexMatchMacro RegexMatchMacro = new();
        private static readonly RegexMacro RegexMacro = new();
        private static readonly RandomMacro RandomMacro = new();
        private static readonly NowMacro NowMacro = new();
        private static readonly JoinMacro JoinMacro = new();

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
                (typeof(IGeneratedSymbolMacro), CaseChange),
                (typeof(IMacro), CoalesceMacro),
                (typeof(IGeneratedSymbolMacro), CoalesceMacro),
                (typeof(IMacro), ConstantMacro),
                (typeof(IGeneratedSymbolMacro), ConstantMacro),
                (typeof(IMacro), new EvaluateMacro()),
                (typeof(IMacro), GeneratePortNumberMacro),
                (typeof(IGeneratedSymbolMacro), GeneratePortNumberMacro),
                (typeof(IMacro), GuidMacro),
                (typeof(IGeneratedSymbolMacro), GuidMacro),
                (typeof(IMacro), JoinMacro),
                (typeof(IGeneratedSymbolMacro), JoinMacro),
                (typeof(IMacro), NowMacro),
                (typeof(IGeneratedSymbolMacro), NowMacro),
                (typeof(IMacro), new ProcessValueFormMacro()),
                (typeof(IMacro), RandomMacro),
                (typeof(IGeneratedSymbolMacro), RandomMacro),
                (typeof(IMacro), RegexMacro),
                (typeof(IGeneratedSymbolMacro), RegexMacro),
                (typeof(IMacro), RegexMatchMacro),
                (typeof(IGeneratedSymbolMacro), RegexMatchMacro),
                (typeof(IMacro), SwitchMacro),
                (typeof(IGeneratedSymbolMacro), SwitchMacro),

                (typeof(ITemplateValidatorFactory), new MandatoryValidationFactory()),
                (typeof(ITemplateValidatorFactory), new MandatoryLocalizationValidationFactory()),
            };
    }
}

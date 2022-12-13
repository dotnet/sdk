// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Edge.Template;

internal interface IParameterSetBuilder : IParameterDefinitionSet
{
    void SetParameterValue(ITemplateParameter parameter, object value, DataSource dataSource);

    void SetParameterEvaluation(ITemplateParameter parameter, EvaluatedInputParameterData evaluatedParameterData);

    bool HasParameterValue(ITemplateParameter parameter);

    bool CheckIsParametersEvaluationCorrect(IGenerator generator, ILogger logger, bool throwOnError, out IReadOnlyList<string> paramsWithInvalidEvaluations);

    InputDataSet Build(bool evaluateConditions, IGenerator generator, ILogger logger);

    void SetParameterDefault(
        IGenerator generator,
        ITemplateParameter parameter,
        IEngineEnvironmentSettings environment,
        bool useHostDefaults,
        bool isRequired,
        List<string> paramsWithInvalidValues);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template
{
    internal class ParameterSetBuilder : ParameterDefinitionSet, IParameterSetBuilder,
#pragma warning disable CS0618 // Type or member is obsolete
        IParameterSet
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private readonly Dictionary<ITemplateParameter, EvalData> _resolvedValues;
        private InputDataSet? _result;

        internal ParameterSetBuilder(IReadOnlyDictionary<string, ITemplateParameter> parameters) : base(parameters)
        {
            _resolvedValues = parameters.ToDictionary(p => p.Value, p => new EvalData(p.Value));
        }

        internal ParameterSetBuilder(IParameterDefinitionSet parameters) : this(parameters.AsReadonlyDictionary())
        { }

        public static IParameterSetBuilder CreateWithDefaults(IGenerator generator, IParameterDefinitionSet parametersDefinition, IEngineEnvironmentSettings environment, string? name = null)
        {
            var result = CreateWithDefaults(generator, parametersDefinition, name, environment, out IReadOnlyList<string> errors);
            if (errors.Any())
            {
                throw new Exception("ParameterDefinitionSet with errors encountered: " + errors.ToCsvString());
            }

            return result;
        }

        public static IParameterSetBuilder CreateWithDefaults(IGenerator generator, IParameterDefinitionSet parametersDefinition, string? name, IEngineEnvironmentSettings environment, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            IParameterSetBuilder templateParams = new ParameterSetBuilder(parametersDefinition);
            List<string> paramsWithInvalidValuesList = new List<string>();

            foreach (ITemplateParameter param in templateParams)
            {
                if (param.IsName)
                {
                    if (name != null)
                    {
                        templateParams.SetParameterValue(param, name, DataSource.NameParameter);
                    }
                }
                else
                {
                    templateParams.SetParameterDefault(
                        generator,
                        param,
                        environment,
                        true,
                        param.Precedence.CanBeRequired,
                        paramsWithInvalidValuesList);
                }
            }

            paramsWithInvalidValues = paramsWithInvalidValuesList;
            return templateParams;
        }

        #region MyRegion IParameterSet members
        //We want all IParameterSet members in single region - hence not following recommended ordering
#pragma warning disable SA1201 // Elements should appear in the correct order
        IEnumerable<ITemplateParameter> IParameterSet.ParameterDefinitions => this;
#pragma warning restore SA1201 // Elements should appear in the correct order

        IDictionary<ITemplateParameter, object?> IParameterSet.ResolvedValues =>
            _resolvedValues
                .Where(p => p.Value.Value != null)
                .ToDictionary(k => k.Key, k => k.Value.Value);

        bool IParameterSet.TryGetParameterDefinition(string name, out ITemplateParameter parameter) => TryGetValue(name, out parameter);

        #endregion /MyRegion IParameterSet members

        public void SetParameterValue(ITemplateParameter parameter, object value, DataSource dataSource)
        {
            _resolvedValues[parameter].SetValue(value, dataSource);
            _result = null;
        }

        public void SetParameterEvaluation(ITemplateParameter parameter, EvaluatedInputParameterData evaluatedParameterData)
        {
            if (!_resolvedValues.ContainsKey(parameter))
            {
                return;
            }

            var old = _resolvedValues[parameter];
            _resolvedValues[parameter] = new EvalData(evaluatedParameterData);
            if (old.InputDataState != InputDataState.Unset)
            {
                _resolvedValues[parameter].SetValue(old.Value, old.DataSource);
            }

            _result = null;
        }

        public bool HasParameterValue(ITemplateParameter parameter) => _resolvedValues[parameter].InputDataState != InputDataState.Unset;

        public bool CheckIsParametersEvaluationCorrect(IGenerator generator, ILogger logger, bool throwOnError, out IReadOnlyList<string> paramsWithInvalidEvaluations)
        {
            List<EvalData> evaluatedParameters = _resolvedValues.Values.ToList();
            List<EvalData> clonedParameters = evaluatedParameters.Select(v => v.Clone()).ToList();
            List<string> invalidParams = new List<string>();
            try
            {
                RunDatasetEvaluation(clonedParameters, generator, logger);
            }
            catch (Exception e)
            {
                logger.LogInformation(e, "Cross-check evaluation of host provided condition evaluations failed.");
                if (throwOnError)
                {
                    throw;
                }
            }

            foreach (var pair in evaluatedParameters.Zip(clonedParameters, (a, b) => (a, b)))
            {
                if (pair.a.IsEnabledConditionResult != pair.b.IsEnabledConditionResult ||
                    pair.a.IsRequiredConditionResult != pair.b.IsRequiredConditionResult)
                {
                    invalidParams.Add(pair.a.ParameterDefinition.Name);
                }
            }

            paramsWithInvalidEvaluations = invalidParams;
            return invalidParams.Count == 0;
        }

        public InputDataSet Build(bool evaluateConditions, IGenerator generator, ILogger logger)
        {
            if (_result == null)
            {
                if (evaluateConditions)
                {
                    this.EvaluateConditionalParameters(generator, logger);
                }

                _result = new InputDataSet(
                    this,
                    _resolvedValues.Select(p => p.Value.ToParameterData()).ToList());
            }

            return _result!;
        }

        public void SetParameterDefault(IGenerator generator, ITemplateParameter parameter, IEngineEnvironmentSettings environment, bool useHostDefaults, bool isRequired, List<string> paramsWithInvalidValues)
        {
            ITemplateEngineHost host = environment.Host;
            if (useHostDefaults && host.TryGetHostParamDefault(parameter.Name, out string? hostParamValue) && hostParamValue != null)
            {
                object? resolvedValue = generator.ConvertParameterValueToType(environment, parameter, hostParamValue, out bool valueResolutionError);
                if (!valueResolutionError)
                {
                    if (resolvedValue is null)
                    {
                        throw new InvalidOperationException($"{nameof(resolvedValue)} cannot be null when {nameof(valueResolutionError)} is 'false'.");
                    }
                    this.SetParameterValue(parameter, resolvedValue, DataSource.HostDefault);
                }
                else
                {
                    paramsWithInvalidValues.Add(parameter.Name);
                }
            }
            // This for newly optional that does not have value set
            else if (!isRequired && parameter.DefaultValue != null)
            {
                object? resolvedValue = generator.ConvertParameterValueToType(environment, parameter, parameter.DefaultValue, out bool valueResolutionError);
                if (!valueResolutionError)
                {
                    if (resolvedValue is null)
                    {
                        throw new InvalidOperationException($"{nameof(resolvedValue)} cannot be null when {nameof(valueResolutionError)} is 'false'.");
                    }
                    this.SetParameterValue(parameter, resolvedValue, DataSource.Default);
                }
                else
                {
                    paramsWithInvalidValues.Add(parameter.Name);
                }
            }
        }

        private static void RunDatasetEvaluation(List<EvalData> evaluatedParameters, IGenerator generator, ILogger logger)
        {
            Dictionary<string, EvalData> variables =
                evaluatedParameters
                    .Where(p => p.Value != null)
                    .ToDictionary(p => p.ParameterDefinition.Name, p => p);

            IDictionary<string, object> variableCollection =
                variables.ToDictionary(p => p.Key, p => p.Value.Value!);

            EvaluateEnablementConditions(generator, evaluatedParameters, variableCollection, variables, logger);
            EvaluateRequirementCondition(generator, evaluatedParameters, variableCollection, logger);
        }

        private static void EvaluateEnablementConditions(
            IGenerator generator,
            IReadOnlyList<EvalData> parameters,
            IDictionary<string, object> variableCollection,
            Dictionary<string, EvalData> variables,
            ILogger logger)
        {
            Dictionary<EvalData, HashSet<EvalData>> parametersDependencies = new();

            // First parameters traversal.
            //   - evaluate all IsEnabledCondition - and get the dependencies between the parameters during doing so
            foreach (EvalData parameter in parameters)
            {
                if (!string.IsNullOrEmpty(parameter.ParameterDefinition.Precedence.IsEnabledCondition))
                {
                    HashSet<string> referencedVariablesKeys = new HashSet<string>();
                    // Do not remove from the variable collection though - we want to capture all dependencies between parameters in the first traversal.
                    // Those will be bulk removed before second traversal (traversing only the required dependencies).
                    parameter.IsEnabledConditionResult = EvaluateParameterCondition(
                        parameter.ParameterDefinition.Precedence.IsEnabledCondition!,
                        parameter.ParameterDefinition.Name,
                        "IsEnabled",
                        generator,
                        variableCollection,
                        referencedVariablesKeys,
                        logger);

                    if (referencedVariablesKeys.Any())
                    {
                        parametersDependencies[parameter] = new HashSet<EvalData>(referencedVariablesKeys.Select(idx => variables[idx]));
                    }
                }
            }

            // No dependencies between parameters detected - no need to process further the second evaluation
            if (parametersDependencies.Count == 0)
            {
                return;
            }

            DirectedGraph<EvalData> parametersDependenciesGraph = new(parametersDependencies);
            // Get the transitive closure of parameters that need to be recalculated, based on the knowledge of params that
            IReadOnlyList<EvalData> disabledParameters = parameters.Where(p => p.IsEnabledConditionResult.HasValue && !p.IsEnabledConditionResult.Value).ToList();
            DirectedGraph<EvalData> parametersToRecalculate =
                parametersDependenciesGraph.GetSubGraphDependentOnVertices(disabledParameters, includeSeedVertices: false);

            // Second traversal - for transitive dependencies of parameters that need to be disabled
            if (parametersToRecalculate.TryGetTopologicalSort(out IReadOnlyList<EvalData> orderedParameters))
            {
                disabledParameters.ForEach(p => variableCollection.Remove(p.ParameterDefinition.Name));

                if (parametersDependenciesGraph.HasCycle(out var cycle))
                {
                    logger.LogWarning(LocalizableStrings.ConditionEvaluation_Warning_CyclicDependency, cycle.Select(p => p.ParameterDefinition.Name).ToCsvString());
                }

                foreach (EvalData parameter in orderedParameters)
                {
                    bool isEnabled = EvaluateParameterCondition(
                        parameter.ParameterDefinition.Precedence.IsEnabledCondition!,
                        parameter.ParameterDefinition.Name,
                        "IsEnabled",
                        generator,
                        variableCollection,
                        null,
                        logger);
                    parameter.IsEnabledConditionResult = isEnabled;
                    if (!isEnabled)
                    {
                        variableCollection.Remove(parameter.ParameterDefinition.Name);
                    }
                }
            }
            else if (parametersToRecalculate.HasCycle(out var cycle))
            {
                throw new TemplateAuthoringException(
                    string.Format(
                        LocalizableStrings.ConditionEvaluation_Error_CyclicDependency,
                        cycle.Select(p => p.ParameterDefinition.Name).ToCsvString()),
                    "Conditional ParameterDefinitionSet");
            }
            else
            {
                throw new Exception(LocalizableStrings.ConditionEvaluation_Error_TopologicalSort);
            }
        }

        private static bool EvaluateParameterCondition(
            string condition,
            string parameterName,
            string conditionName,
            IGenerator generator,
            IDictionary<string, object> variableCollection,
            HashSet<string>? referencedVariablesKeys,
            ILogger logger)
        {
            if (!generator.TryEvaluateFromString(logger, condition, variableCollection, out bool result, out string evaluationError, referencedVariablesKeys))
            {
                throw new TemplateAuthoringException(
                    string.Format(
                        LocalizableStrings.ConditionEvaluation_Error_MismatchedCondition,
                        conditionName,
                        parameterName,
                        condition,
                        evaluationError),
                    parameterName);
            }

            return result;
        }

        private static void EvaluateRequirementCondition(
            IGenerator generator,
            IReadOnlyList<EvalData> parameters,
            IDictionary<string, object> variableCollection,
            ILogger logger)
        {
            foreach (EvalData parameter in parameters)
            {
                if (!string.IsNullOrEmpty(parameter.ParameterDefinition.Precedence.IsRequiredCondition))
                {
                    parameter.IsRequiredConditionResult = EvaluateParameterCondition(
                        parameter.ParameterDefinition.Precedence.IsRequiredCondition!,
                        parameter.ParameterDefinition.Name,
                        "IsRequired",
                        generator,
                        variableCollection,
                        null,
                        logger);
                }
            }
        }

        private void EvaluateConditionalParameters(IGenerator generator, ILogger logger)
        {
            List<EvalData> evaluatedParameters = _resolvedValues.Values.ToList();
            RunDatasetEvaluation(evaluatedParameters, generator, logger);
        }

        private class EvalData
        {
            private object? _value;
            private DataSource _dataSource = DataSource.NoSource;

            public EvalData(ITemplateParameter parameterDefinition)
            {
                ParameterDefinition = parameterDefinition;
            }

            public EvalData(EvaluatedInputParameterData other)
                : this(other.ParameterDefinition, other.Value, other.IsEnabledConditionResult, other.IsRequiredConditionResult)
            {
                this._dataSource = DataSource.NoSource;
            }

            private EvalData(
                ITemplateParameter parameterDefinition,
                object? value,
                bool? isEnabledConditionResult,
                bool? isRequiredConditionResult)
            {
                ParameterDefinition = parameterDefinition;
                _value = value;
                IsEnabledConditionResult = isEnabledConditionResult;
                IsRequiredConditionResult = isRequiredConditionResult;
            }

            public ITemplateParameter ParameterDefinition { get; }

            public InputDataState InputDataState { get; private set; } = InputDataState.Unset;

            public bool? IsEnabledConditionResult { get; set; }

            public bool? IsRequiredConditionResult { get; set; }

            public DataSource DataSource => _dataSource;

            public object? Value => _value;

            public void SetValue(object? value, DataSource source)
            {
                _value = value;
                _dataSource = source;
                InputDataState = InputDataStateUtil.GetInputDataState(value);
            }

            public override string ToString() => $"{ParameterDefinition}: {Value?.ToString() ?? "<null>"}";

            public EvaluatedInputParameterData ToParameterData()
            {
                return new EvaluatedInputParameterData(
                    this.ParameterDefinition,
                    this.Value,
                    _dataSource,
                    this.IsEnabledConditionResult,
                    this.IsRequiredConditionResult,
                    InputDataState);
            }

            public EvalData Clone()
            {
                var ds = _dataSource;
                return new EvalData(ParameterDefinition, Value, IsEnabledConditionResult, IsRequiredConditionResult)
                {
                    _dataSource = ds
                };
            }

        }
    }
}

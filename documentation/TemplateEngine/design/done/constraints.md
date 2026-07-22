# Template constraints

User story: https://github.com/dotnet/templating/issues/3107.

Template constraints may be configured in the template configuration to restrict the context the template may be used in.
The following built-in constraints will be provided by template engine:
- operating system (limited to Windows, Linux, MacOS)
- host name and version

The following constraints will be implemented in `dotnet new` host:
- SDK version
- installed optional workloads

Additional constraint for `dotnet new` is planned based on MSBuild properties of the current project (if any) after ability to evaluate project context is implemented.

The host may implement its own constraints and load them as components when initializing work with template engine (creating `IEngineEnvironmentSettings` or `Bootstrapper`).

The host might use the result of the constraint evaluation for the following (but not limited to):
- restrict the template from being instantiated
- restrict the template from being listed / list requirements for the template to be instantiated
- restrict the template from being installed

`dotnet new` will implement the following changes based on constraints:
- the template which is not allowed in the context will not be shown in `dotnet new list` output
   - additional option (`--force`) may be considered to show all the templates anyway
- the template which is not allowed in the context will not be allowed to be instantiated (unless `--force` is specified).
- the templates that are not allowed to be run will be installed anyway, but in case the template cannot be run, the message will be shown after installation.
- tab completion: tab completion should not suggest the values from restricted templates. Due to context evaluation for some constraints may take time, time restrictions should be considered(?).

Out of scope:
- adaptation of `dotnet new search` to list the constraints

## Constraints configuration
Constraints should be configured in `template.json` in `constraints` section.
`Constraints` section is JSON object type, containing 1 or more constraint definition key-value pairs. 
```json
  "constraints": {
       "constraintOS": {
           "type": "os",
           "args": ["MacOS", "Linux"]      //only allowed on MacOS and Linux
       },
       "constraintVS": {
           "type": "vs-components",
           "args": {
                "components": [            //requires Xamarin of version 1.0.0.0 - version 5.0.0.0.
                    {
                        "name": "Xamarin",
                        "minVersion": "1.0.0.0",
                        "maxVersion": "5.0.0.0"
                    }
                ],
                "whatever": true 
           }
       }
  }
```
Constraint definition key is a unique name. The value is an object that has two mandatory keys:
- `type`: string. Constraint type. All implemented constraints must have unique types. The type will be used to identify constraint component (as alternative to using guids). Case-sensitive.
- `args`: array or object. Constraint arguments. Arguments depends on constraint implementation. The JSON array or object will be passed to constraint component for evaluation as string. The constraint implementation should be able to parse the content.

Constraint configuration will be read when `template.json` is read and extracted to template engine cache. 
It will be available in `ITemplateInfo`, therefore for constraint evaluation `template.json` won't be read, and cached value will be used instead. 
New property `Constraints` will be added to `ITemplateInfo` of type `IReadOnlyList<TemplateConstraintInfo>` containing template constraints information.

`TemplateConstraintInfo` has the following properties:
- `string Type`: constraint type
- `string Args`: JSON string containing args read from `template.json`. Args to be parsed when evaluating the constraint by constraint itself.

### Types uniqueness consideration
At the moment 3rd party constraints won't be supported. 1st party developers should ensure unique naming.
When 3rd party constraints are supported, the way to check if the constraint type is already used should be considered.


## Constraint component

Constraint implementation should implement `ITemplateConstraintFactory` interface that creates `ITemplateConstraint`:
```csharp
    public interface ITemplateConstraintFactory : IIdentifiedComponent
    {
        /// <summary>
        /// Unique constraint type. Should match the definition in `template.json`.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Creates new <see cref="ITemplateConstraint"/> based on current <see cref="IEngineEnvironmentSettings"/>.
        /// </summary>
        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings);
    }
```
When creating the constraint, it is assumed that constraint will be initialized and its context will be evaluated.
Example: if constraint needs to know the project MSBuild properties it is assumed that project will be identified and MSBuild properties will be evaluated when the constraint is being created prior to first evaluation.
Template constraints initialization will be started in parallel once `TemplateConstraintManager` is created. The methods that work with constraint will ensure task completion before evaluation.

`ITemplateConstraint` definition:
```csharp
    public interface ITemplateConstraint
    {
        /// <summary>
        /// Unique constraint type. Should match the definition in `template.json`.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// The user friendly constraint name, that can be used in UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Evaluates <see cref="ITemplateInfo"/> and returns <see cref="TemplateConstraintResult"/> containing result.
        /// </summary>
        TemplateConstraintResult Evaluate(string args);
    }

```
where `args` contains arguments for constraint read from `template.json`. `Type` matches `type` from constraint configuration (case-sensitive).
Note: the parameter type may be extended if needed. 
`Evaluate` method should have optimal performance and operate on the context already evaluated when the constraint was created. It should not launch any time-consuming operation.
`TemplateConstraintResult` contains the following properties:
- `Allowed`: (`Allowed|Restricted|NotEvaluated`) - the template is allowed in the given context.
- `LocalizedErrorMessage`: string - localized error message in case the template is restricted (when `Allowed == false`).
- `CallToAction`: string - localized message that indicate the action to be taken so that the constraint is fulfilled (optional).

Examples for constraints above:
- template being run on Windows
    - `Allowed`: `false`
    - `LocalizedErrorMessage`: `The template can only be run on the following operating systems: Linux, MacOS.`
    - `CallToAction`: `null`
- Xamarin constraint is not met
    - `Allowed`: `false`
    - `LocalizedErrorMessage`: `The template requires Xamarin component [1.0.0.0 - 5.0.0.0].`
    - `CallToAction`: `Install Xamarin component [1.0.0.0 - 5.0.0.0] using Visual Studio installer.`

## API to work with constraints

### `TemplateConstraintManager` class

Class works similar to `TemplatePackageManager`. Once created, it starts initialization of template constraints in background. The host has to create it passing `IEngineEnvironmentSettings`. To reinitialize constraints, new instance of class should be created. It is assumed that the host creates it once, and uses it until disposed.

So far, 2 methods are planned:

`Task<TemplateConstraintResult> EvaluateConstraintAsync(string type, string args)`

Evaluates constraint with defined `type` and `args`. The method may wait until constraint defined by `type` is initialized.

`Task<IReadOnlyList<ITemplateConstraint>> GetConstraintsAsync(IEnumerable<ITemplateInfo>? templates = null)`

Gets initialized constraints, if parameter is passed gets only constraints needed for passed `ITemplateInfo`.
The method will not throw an exception in case some of constraints failed to be initialized, the warning will be logged. `ITemplateConstraint` itself may evaluate arguments in synch way using `Evaluate` method.

`Task<IReadOnlyList<(ITemplateInfo Template, IReadOnlyList<TemplateConstraintResult> Result)>> EvaluateConstraintsAsync(IEnumerable<ITemplateInfo> templates)` 

Evaluates all constraints required for given `templates`.

More methods/alterations might be considered if required.

### `TemplatePackageManager` class

`Task<IReadOnlyList<ITemplateMatchInfo>> TemplatePackageManager.GetTemplatesAsync(Func<ITemplateMatchInfo, bool> matchFilter, IEnumerable<Func<ITemplateInfo, MatchInfo?>> filters, CancellationToken cancellationToken)`

This method already exists. New predefined (well-known) filter will be added capable to evaluate constraints and it requires `ITemplateConstraint` to be initialized prior to defining and using the filter.

The following `MatchInfo` will be used for constraints:
- `Kind`: `Exact` or `Mismatch`
- `Name`: `"Constraints.<constraint type>"`
- `Value`: `null`

This method will not provide the reason why template is not a match (`LocalizedErrorMessage`) and call to action (`CallToAction`).

## Generic considerations
- Failure to initialize the constraint or unknown constraint may not restrict the template when using `GetTemplatesAsync`. The warning will be sent via logger and might be used by host. 
If this behavior needs to be configurable, we may introduce an additional parameter to configuration (`restrictOnError`). 
- Knowing output directory for constraint evaluation. 
Introducing output directory to environment settings might be needed, in case the constraint initialization might depend on it. 
This is the case for planned implementation of constraint based on project MSBuild properties. The way to pass output directory should be considered, passing output directory will be optional. 
In case output directory is not passed, the current directory will be used. The change should not affect using output directory when instantiating template, the output directory passed to environment settings will only be used for context evaluation. 
- For `Bootstrapper`, new method may be considered to work with constraints. It may return all templates satisfying constraints without using `GetTemplatesAsync` or detailed constraints information for all passed templates. However, for hosts that are working using members of `Edge` directly, the methods defined above should be sufficient.
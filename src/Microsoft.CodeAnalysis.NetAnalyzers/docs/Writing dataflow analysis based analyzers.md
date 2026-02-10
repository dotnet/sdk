# Writing dataflow analysis based analyzers

## Introduction

### Definition

Data-flow analysis is a technique for gathering information about the possible set of values calculated at various points in a computer program. A program's control flow graph (CFG) is used to determine those parts of a program to which a particular value assigned to a variable might propagate.

### Theory and concepts

Please read [this introductory article](https://wikipedia.org/wiki/Data-flow_analysis) to understand the basic terminology, concepts and common algorithms for dataflow analysis.

### ControlFlowGraph (CFG) API

[Microsoft.CodeAnalysis](https://www.nuget.org/packages/Microsoft.CodeAnalysis/) NuGet package provides public APIs to generate a ControlFlowGraph based on low-level IOperation nodes as statements/instructions within a basic block. See more [details](https://github.com/dotnet/roslyn/blob/1deafee3682da88bf07d1c18521a99f47446cee8/src/Compilers/Core/Portable/Operations/ControlFlowGraph.cs#L13-L20).

## Dataflow analysis framework

We have built a dataflow analysis [framework](https://github.com/dotnet/roslyn-analyzers/tree/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow) based on the above CFG API in this repo. Additionally, we have implemented certain [well-known dataflow analyses](https://github.com/dotnet/roslyn-analyzers/tree/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis) on top of this framework. This enables you to implement either or both of the following:

1. Write dataflow based analyzers which consume the analysis result from these well-known analyses.
2. Write your own custom dataflow analyses, which can optionally consume analysis results from these well-known analyses.

Let us start by listing out the most important concepts and datatypes in our framework.

### Important concepts and datatypes

1. [DataflowAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/89f1193364ef535a508f63e89d7c0e701b52c45c/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/DataFlowAnalysis.cs): Base type for all dataflow analyses on a control flow graph. It performs a worklist based approach to flow abstract data values across the basic blocks until a fix point is reached.

2. [AbstractDataFlowAnalysisContext](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractDataFlowAnalysisContext.cs): Base type for analysis contexts for execution of DataFlowAnalysis on a control flow graph. It is the primary input to the core dataflow analysis computation routine [DataFlowAnalysis.Run](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/DataFlowAnalysis.cs#L52), and includes things such as input CFG, owning symbol, etc.

3. [DataFlowAnalysisResult](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/DataFlowAnalysisResult.cs): Result from execution of DataFlowAnalysis on a control flow graph. It stores:
    1. Analysis values for all operations in the graph and
    2. `AbstractBlockAnalysisResult` for every basic block in the graph and
    3. Merged analysis state for all the unhandled throw operations in the graph.

4. [AbstractBlockAnalysisResult](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractBlockAnalysisResult.cs): Common base type for result from execution of DataFlowAnalysis on a basic block.

5. [AbstractDomain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractDomain.cs): Abstract domain for DataFlowAnalysis to merge and compare values across different control flow paths. The primary abstract domains of interest are:
    1. [AbstractValueDomain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractValueDomain.cs): Abstract value domain for a DataFlowAnalysis to merge and compare individual dataflow analysis values.
    2. [AbstractAnalysisDomain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractAnalysisDomain.cs): Abstract analysis domain for a DataFlowAnalysis to merge and compare entire analysis data sets or dictionary of analysis values.

    Each dataflow analysis must define its own value domain and analysis domain. These domains are used by DataFlowAnalysis to perform following primary operations:
    1. _Merge_ individual analysis values and analysis sets at various program points in the graph and also at start of basic blocks which have more then one incoming control flow branches.
    2. _Compare_ analysis values at same program point/basic blocks across different flow analysis iterations to determine if the algorithm has reached a fix point and can be terminated.

6. [DataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/DataFlowOperationVisitor.cs): Operation visitor to flow the abstract dataflow analysis values across a given statement (IOperation) in a basic block or a given control flow branch. Operation visitor basically defines the _transfer functions_ for analysis values.

7. [AnalysisEntity](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AnalysisEntity.cs): Primary entity for which analysis data is tracked by majority of dataflow analyses. The entity is based on one or more of the following:
    1. An [ISymbol](https://github.com/dotnet/roslyn/blob/version-3.0.0/src/Compilers/Core/Portable/Symbols/ISymbol.cs)
    2. One or more [AbstractIndex](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractIndex.cs) indices to index into the parent entity. For example, an index into an array or collection.
    3. "this" or "Me" instance.
    4. An allocation or an object creation.

    Each entity has:
    1. An associated non-null "Type" and
    2. A non-null "InstanceLocation" indicating the abstract location at which the entity is located and
    3. An optional parent entity if this entity has the same "InstanceLocation" as the parent (i.e. parent is a value type allocated on stack).

8. [AbstractLocation](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractLocation.cs): Represents an abstract analysis location. This may be used to represent a location where an AnalysisEntity resides, i.e. `AnalysisEntity.InstanceLocation` or a location that is pointed to by a reference type variable, and tracked with [PointsToAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/PointsToAnalysis/PointsToAnalysis.cs). An analysis location can be created for one of the following cases:
    1. An allocation or an object creation operation.
    2. Location for the implicit 'this' or 'Me' instance being analyzed.
    3. Location created for certain symbols which do not have a declaration in executable code, i.e. no IOperation for declaration (such as parameter symbols, member symbols, etc.).
    4. Location created for flow capture entities, i.e. for [InterproceduralCaptureId](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/InterproceduralCaptureId.cs) created for [IFlowCaptureOperation](https://github.com/dotnet/roslyn/blob/version-3.0.0/src/Compilers/Core/Portable/Operations/IFlowCaptureOperation.cs) or [IFlowCaptureReferenceOperation](https://github.com/dotnet/roslyn/blob/version-3.0.0/src/Compilers/Core/Portable/Operations/IFlowCaptureReferenceOperation.cs).

9. [PointsToAbstractValue](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/PointsToAnalysis/PointsToAbstractValue.cs): Abstract PointsTo value for an AnalysisEntity/IOperation tracked by PointsToAnalysis. It contains the set of possible AbstractLocations that the entity or the operation can point to and the "Kind" of the location(s).

## Implementing a custom dataflow analysis

Now that we are familiar with the basic concepts and data types for flow analysis, let us walk through an existing flow analysis implementation and a step by step guide to creating your own flow analysis implementation, say `MyCustomAnalysis`. We will use [ValueContentAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis) as the sample flow analysis implementation.

1. Start by creating a new folder, say `MyCustomAnalysis` within [this folder](https://github.com/dotnet/roslyn-analyzers/tree/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis) in the repo.

2. Add **MyCustomAnalysis.cs** that defines **MyCustomAnalysis**: Sub-type of `DataFlowAnalysis` that provides the public entry points `TryGetOrComputeResult` into your analysis. It takes a bunch of input parameters, packages them into your analysis specific analysis context, and invokes `DataFlowAnalysis.TryGetOrComputeResultForAnalysisContext` to compute the analysis result. The implementation of this type should be almost identical for all analyses. Note that the type arguments for this type will be defined in subsequent steps. See [ValueContentAnalysis.cs](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.cs) for reference.

3. Add **MyCustomAbstractValue.cs** that defines **MyCustomAbstractValue**: This type defines the core analysis _value_ that needs to be tracked by your analysis. For example, ValueContentAnalysis defines [ValueContentAbstractValue](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAbstractValue.cs), such that each instance of `ValueContentAbstractValue` contains a set of potential constant literal values and a [ValueContainsNonLiteralState](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContainsNonLiteralState.cs) for the non-literal state of the abstract value. It also defines a bunch of static instances of common value content values, see [here](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAbstractValue.cs#L23-L32).

4. Add **MyCustomAnalysis.MyCustomAbstractValueDomain.cs** that defines **MyCustomAbstractValueDomain**: Sub-type of `AbstractValueDomain` that defines how to compare and merge different `MyCustomAbstractValue` across different control flow paths or flow analysis iterations. For example, see [ValueContentAbstractValueDomain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.ValueContentAbstractDomain.cs)

5. Define the core data structure for the global CFG wide and/or per-block _analysis data_ that will tracked by your analysis, i.e. **MyCustomAnalysisData**. For most analyses, you likely won't need to define a separate type or source file for `MyCustomAnalysisData`. A likely definition would be just a using such as `using MyCustomAnalysisData = DictionaryAnalysisData<AnalysisEntity, MyCustomAbstractValue>;`. See [here](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/DisposeAnalysis/DisposeAnalysis.cs#L13) for such an example for `DisposeAnalysisData`. However, for complex cases, you may need to define your own `MyCustomAnalysisData` user defined type. For example, see [ValueContentAnalysisData](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysisData.cs) that has an aggregated analysis data, which contains core analysis data dictionary from AnalysisEntity to ValueContentAbstractValue per-basic block and additional predicated analysis data.

6. Define **MyCustomAnalysisDomain**: Sub-type of `AbstractAnalysisDomain` that defines how to compare and merge different `MyCustomAnalysisData` data sets across different control flow paths or flow analysis iterations. For most analyses, where `MyCustomAnalysisData` is defined as a simple dictionary, this domain will be defined with a simple using such as `using MyCustomAnalysisDomain = MapAbstractDomain<AnalysisEntity, MyCustomAbstractValue>;`. See [here](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/DisposeAnalysis/DisposeAnalysis.cs#L14) for such an example for `DisposeAnalysisDomain`. However, for complex cases, you may need to define your own `MyCustomAnalysisDomain` user defined type, such as [ValueContentAnalysis.CoreAnalysisDataDomain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.CoreAnalysisDataDomain.cs).

7. Add **MyCustomAnalysis.MyCustomBlockAnalysisResult.cs** that defines **MyCustomBlockAnalysisResult**: Sub-type of `AbstractBlockAnalysisResult` that is the immutable per-basic block result from dataflow analysis. It is immutable equivalent of the mutable `MyCustomAnalysisData` that was used during flow analysis execution. For reference, see [ValueContentBlockAnalysisResult](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentBlockAnalysisResult.cs), the core immutable data exposed being `ImmutableDictionary<AnalysisEntity, ValueContentAbstractValue>`.

8. Define **MyCustomAnalysisResult**: Parameterized version of `DataFlowAnalysisResult` with `MyCustomBlockAnalysisResult` and `MyCustomAbstractValue` as type arguments. For most cases, this will be just a simple using directive such as `using MyCustomAnalysisResult = DataFlowAnalysisResult<MyCustomBlockAnalysisResult, MyCustomAbstractValue>;`. For example, see [ValueContentAnalysisResult](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.cs#L12).

9. Add **MyCustomAnalysisContext.cs** that defines **MyCustomAnalysisContext**: Sub-type of `AbstractDataFlowAnalysisContext` that packages the core input parameters to dataflow analysis. Most of the code in this type is common boiler plate code that is identical for all analyses. You should be able to just clone an existing file, for example [ValueContentAnalysisContext.cs](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysisContext.cs), and just do a simple find and replace of "ValueContent" with "MyCustom" in the file.

10. Add **MyCustomAnalysis.MyCustomDataFlowOperationVisitor.cs** that defines **MyCustomDataFlowOperationVisitor**: Sub-type of `DataFlowOperationVisitor` that contains the core operation visitor, which tracks the `CurrentAnalysisData` and overrides the required `VisitXXXOperation` to define the transfer functions for how the current analysis data changes with operations and also computes the analysis values for the overridden operation (program points in CFG). You have three potential options for implementing this operation visitor:
    1. Derive from [AnalysisEntityDataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AnalysisEntityDataFlowOperationVisitor.cs): If your core `MyCustomAnalysisData` is a dictionary keyed on `AnalysisEntity`, then you should most likely be deriving from this type. This operation visitor is intended for all analyses which track some data pertaining to symbols, which are represented by analysis entities. For example, [ValueContentDataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.ValueContentDataFlowOperationVisitor.cs#L18) derives from this visitor.
    2. Derive from [AbstractLocationDataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/AbstractLocationDataFlowOperationVisitor.cs): If your core `MyCustomAnalysisData` is a dictionary keyed on `AbstractLocation`, then you should most likely be deriving from this type. This operation visitor is intended for all analyses which track some data pertaining to locations/allocations, which are represented by abstract locations. For example, [DisposeDataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/DisposeAnalysis/DisposeAnalysis.DisposeDataFlowOperationVisitor.cs#L22) derives from this visitor.
    3. Derive from [DataFlowOperationVisitor](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/DataFlowOperationVisitor.cs): If none of the above two special visitor sub-types are suitable for your analysis, you can directly sub-type the core `DataFlowOperationVisitor`, although you will likely have a more complicated implementation with many more overrides. Hopefully, this will not be required often.

Once you have implemented the above custom analysis pieces, your dataflow analyzers can invoke `MyCustomAnalysis.TryGetOrComputeResult` API to get the analysis result. Your analyzer can then consume any of the below components of the analysis result:

   1. Analysis values for any given operation in the graph OR
   2. AbstractBlockAnalysisResult for any basic block in the graph OR
   3. Merged analysis state for all the unhandled throw operations in the graph.

**PERFORMANCE NOTE:** Dataflow analysis is computationally quite expensive, especially for large method bodies with complex control flow and symbols that can lead to large analysis data sets and multiple flow analysis iterations and data set merges to reach fix point. Analyzers should make sure that they have adequate syntactic and/or semantic checks upfront to try and limit the cases where flow analysis is invoked.

## Well-known flow analyses

We have some common analyses that you may likely want to consume for your custom dataflow analysis implementation or use directly in dataflow analyzers:

1. [PointsToAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/PointsToAnalysis/PointsToAnalysis.cs): Dataflow analysis to track locations pointed to by AnalysisEntity and IOperation instances. This is the most commonly used dataflow analysis in all our flow based analyzers/analyses. Consider the following example:

    ```csharp
    var x = new MyClass();
    object y = x;
    var z = flag ? new MyClass() : y;
    ```

    PointsToAnalysis will compute that variables `x` and `y` have identical non-null `PointsToAbstractValue`, which contains a single `AbstractLocation` corresponding to the first `IObjectCreationOperation` for `new MyClass()`. Variable `z` has a different `PointsToAbstractValue`, which is guaranteed to be non-null, but has two potential `AbstractLocation`, one for each `IObjectCreationOperation` in the above code.

2. [CopyAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/CopyAnalysis/CopyAnalysis.cs): Dataflow analysis to track AnalysisEntity instances that share the same value type or reference type value, determined based on [CopyAbstractValueKind](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/CopyAnalysis/CopyAbstractValueKind.cs#L8). Consider the following example:

    ```csharp
    var x = new MyClass();
    object y = x;
    int c1 = 0;
    int c2 = 0;
    ```

   CopyAnalysis will compute that variables `x` and `y` have identical `CopyAbstractValue` with `CopyAbstractValueKind.KnownReferenceCopy` with two `AnalysisEntity` instances, one for `x` and one for `y`. Similarly, it will compute that `c1` and `c2` have identical `CopyAbstractValue` with `CopyAbstractValueKind.KnownValueCopy` with two `AnalysisEntity` instances, one for `c1` and one for `c2`. CopyAnalysis is currently off by default for all analyzers as it has known performance issues and needs performance tuning. It can be enabled by end users with editorconfig option [copy_analysis](https://github.com/dotnet/roslyn-analyzers/blob/main/docs/Analyzer%20Configuration.md#configure-execution-of-copy-analysis-tracks-value-and-reference-copies).

3. [ValueContentAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/ValueContentAnalysis/ValueContentAnalysis.cs): Dataflow analysis to track possible constant values that might be stored in an AnalysisEntity and IOperation instances. This is identical to constant propagation for constant values stored in non-constant symbols. Consider the following example:

    ```csharp
    int c1 = 0;
    int c2 = 0;
    int c3 = c1 + c2;
    int c4 = flag ? c3 : c3 + param;     // assume 'param' is a parameter for this method block with unknown value content from callers.
    ```

    ValueContentAnalysis will compute that variables `c1`, `c2` and `c3` have identical `ValueContentAbstractValue` with a single literal value `0` and `ValueContainsNonLiteralState.No` to indicate it cannot contain a non-literal value. It will compute that `c4` has a different `ValueContentAbstractValue` with a single literal value `0` and `ValueContainsNonLiteralState.Maybe` to indicate that it may contain some non-literal value(s) in some code path(s).

4. [TaintedDataAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/TaintedDataAnalysis/TaintedDataAnalysis.cs): Dataflow analysis to track tainted state of AnalysisEntity and IOperation instances.

5. [PropertySetAnalysis](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Analysis/PropertySetAnalysis/PropertySetAnalysis.cs): Dataflow analysis to track values assigned to one or more properties of an object to identify and flag incorrect/insecure object state. See its [PropertySetAnalysisTests.cs](https://github.com/dotnet/roslyn-analyzers/blob/0b21b2163220669981f682e58a8ddcdc9a839774/src/Utilities.UnitTests/FlowAnalysis/Analysis/PropertySetAnalysis/PropertySetAnalysisTests.cs) for examples.

## Interprocedural dataflow analysis

We also support a complete context sensitive interprocedural flow analysis for invocations of methods within the same compilation. See [InterproceduralAnalysisKind](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/InterproceduralAnalysisKind.cs) for more details.

Interprocedural analysis support is baked into the core `DataFlowOperationVisitor` and each custom dataflow analysis implementation gets all this support for free, without requiring to add any code specific to interprocedural analysis. Each dataflow analysis defines the default `InterproceduralAnalysisKind` in its `TryGetOrComputeResult` entry point, and the analyzer is free to override the interprocedural analysis kind. Interprocedural analysis almost always leads to more precise analysis results at the expense of more computation resources, i.e. it likely takes more memory and time to complete. So, an analyzer should be extremely fine tuned for performance if it defaults to enabling context sensitive interprocedural analysis by default. Note that the end user can override the interprocedural analysis kind for specific rule ID or all dataflow rules with the editorconfig option [interprocedural-analysis-kind](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/docs/Analyzer%20Configuration.md#interprocedural-analysis-kind). This option takes precedence over the defaults in the `TryGetOrComputeResult` entry points to analysis and also any overrides from individual analyzers invoking this API.

We also have couple of additional configuration/customization points for interprocedural analysis:

1. [InterproceduralAnalysisConfiguration](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/InterproceduralAnalysisConfiguration.cs): Defines interprocedural analysis configuration parameters. For example, `MaxInterproceduralMethodCallChain` and `MaxInterproceduralLambdaOrLocalFunctionCallChain` control the size of the maximum height of the interprocedural call tree. Each analyzer can override the defaults for these chain lengths (3 as of current implementation), and end users can override it with editorconfig options [max_interprocedural_method_call_chain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/docs/Analyzer%20Configuration.md#maximum-method-call-chain-length-to-analyze-for-interprocedural-dataflow-analysis) and [max_interprocedural_lambda_or_local_function_call_chain](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/docs/Analyzer%20Configuration.md#maximum-lambda-or-local-function-call-chain-length-to-analyze-for-interprocedural-dataflow-analysis).

2. [InterproceduralAnalysisPredicate](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Utilities/FlowAnalysis/FlowAnalysis/Framework/DataFlow/InterproceduralAnalysisPredicate.cs): Optional predicates that can be provided by each analyzer to determine if interprocedural analysis should be invoked or not for specific callsites. For example, [this predicate](https://github.com/dotnet/roslyn-analyzers/blob/v2.9.7/src/Microsoft.NetCore.Analyzers/Core/Runtime/DisposeObjectsBeforeLosingScope.cs#L175) used by dispose analysis significantly trims down the size of interprocedural call trees and provides huge performance improvements for interprocedural analysis.

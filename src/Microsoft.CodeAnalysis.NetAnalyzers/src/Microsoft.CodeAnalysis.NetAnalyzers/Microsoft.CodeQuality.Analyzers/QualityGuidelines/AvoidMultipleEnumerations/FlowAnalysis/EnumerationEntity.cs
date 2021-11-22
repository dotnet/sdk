// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

//using System.Collections.Immutable;
//using System.Linq;
//using Analyzer.Utilities;
//using Analyzer.Utilities.PooledObjects;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
//using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
//using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// The entity that could be enumerated by for each loop or method.
    /// </summary>
    internal interface IEnumerationEntity
    {
    }
    //    /// <summary>
    //    /// A set of the possible entities that would be enumerated by this entity.
    //    /// </summary>
    //    private ImmutableHashSet<DeferredTypeEntity> Entities { get; }

    //    private EnumerationEntity(ImmutableHashSet<DeferredTypeEntity> entities) => Entities = entities;

    //    public static EnumerationEntity? Create(
    //        IOperation operation,
    //        PointsToAnalysisResult? pointsToAnalysisResult,
    //        WellKnownSymbolsInfo wellKnownSymbolsInfo)
    //    {
    //        if (pointsToAnalysisResult is null)
    //        {
    //            return null;
    //        }

    //        var result = pointsToAnalysisResult[operation.Kind, operation.Syntax];
    //        if (result.Kind != PointsToAbstractValueKind.KnownLocations)
    //        {
    //            return null;
    //        }

    //        if (result.Locations.IsEmpty || result.Locations.Any(
    //            location => !IsDeferredType(location.LocationType, wellKnownSymbolsInfo.AdditionalDeferredTypes)))
    //        {
    //            return null;
    //        }

    //        using var builder = PooledHashSet<DeferredTypeEntity>.GetInstance();
    //        foreach (var location in result.Locations)
    //        {
    //            builder.Add(new DeferredTypeEntity(location.Symbol, location.Creation));
    //        }

    //        return new EnumerationEntity(builder.ToImmutable());
    //    }

    //    protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
    //    {
    //        hashCode.Add(HashUtilities.Combine(Entities));
    //    }

    //    protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<EnumerationEntity> obj)
    //    {
    //        return HashUtilities.Combine(((EnumerationEntity)obj).Entities) == HashUtilities.Combine(Entities);
    //    }
    //}
}
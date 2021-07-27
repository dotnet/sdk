// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Operations;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public partial class DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        public static TheoryData<BinaryOperatorKind, int> LeftCount_NoDiagnostic_TheoryData { get; } = new TheoryData<BinaryOperatorKind, int>
        {
            { BinaryOperatorKind.Equals            , 1 },
            { BinaryOperatorKind.NotEquals         , 1 },
            { BinaryOperatorKind.LessThanOrEqual   , 1 },
            { BinaryOperatorKind.GreaterThan       , 1 },
            { BinaryOperatorKind.LessThan          , 0 },
            { BinaryOperatorKind.GreaterThanOrEqual, 0 },
            { BinaryOperatorKind.Equals            , 2 },
            { BinaryOperatorKind.NotEquals         , 2 },
            { BinaryOperatorKind.LessThanOrEqual   , 2 },
            { BinaryOperatorKind.GreaterThan       , 2 },
            { BinaryOperatorKind.LessThan          , 2 },
            { BinaryOperatorKind.GreaterThanOrEqual, 2 },
        };

        public static TheoryData<BinaryOperatorKind, int, bool> LeftCount_NoDiagnostic_Predicate_TheoryData { get; } = Build_LeftCount_NoDiagnostic_Predicate_TheoryData();

        private static TheoryData<BinaryOperatorKind, int, bool> Build_LeftCount_NoDiagnostic_Predicate_TheoryData()
        {
            var theoryData = new TheoryData<BinaryOperatorKind, int, bool>();
            foreach (var withPredicate in new[] { false, true })
            {
                foreach (var fixerData in LeftCount_NoDiagnostic_TheoryData)
                {
                    theoryData.Add((BinaryOperatorKind)fixerData[0], (int)fixerData[1], withPredicate);
                }
            }
            return theoryData;
        }

        public static TheoryData<int, BinaryOperatorKind> RightCount_NoDiagnostic_TheoryData { get; } = new TheoryData<int, BinaryOperatorKind>
        {
            { 1, BinaryOperatorKind.Equals             },
            { 1, BinaryOperatorKind.NotEquals          },
            { 1, BinaryOperatorKind.LessThan           },
            { 1, BinaryOperatorKind.GreaterThanOrEqual },
            { 0, BinaryOperatorKind.GreaterThan        },
            { 0, BinaryOperatorKind.LessThanOrEqual    },
            { 2, BinaryOperatorKind.Equals             },
            { 2, BinaryOperatorKind.NotEquals          },
            { 2, BinaryOperatorKind.LessThan           },
            { 2, BinaryOperatorKind.GreaterThanOrEqual },
            { 2, BinaryOperatorKind.GreaterThan        },
            { 2, BinaryOperatorKind.LessThanOrEqual    },
        };

        public static TheoryData<int, BinaryOperatorKind, bool> RightCount_NoDiagnostic_Predicate_TheoryData { get; } = Build_RightCount_NoDiagnostic_Predicate_TheoryData();

        private static TheoryData<int, BinaryOperatorKind, bool> Build_RightCount_NoDiagnostic_Predicate_TheoryData()
        {
            var theoryData = new TheoryData<int, BinaryOperatorKind, bool>();
            foreach (var withPredicate in new[] { false, true })
            {
                foreach (var fixerData in RightCount_NoDiagnostic_TheoryData)
                {
                    theoryData.Add((int)fixerData[0], (BinaryOperatorKind)fixerData[1], withPredicate);
                }
            }
            return theoryData;
        }

        public static TheoryData<BinaryOperatorKind, int, bool> LeftCount_Fixer_TheoryData { get; } = new TheoryData<BinaryOperatorKind, int, bool>
        {
            { BinaryOperatorKind.Equals            , 0 , true }, // !Any
            { BinaryOperatorKind.NotEquals         , 0 , false }, // Any
            { BinaryOperatorKind.LessThanOrEqual   , 0 , true }, // !Any
            { BinaryOperatorKind.GreaterThan       , 0 , false }, // Any
            { BinaryOperatorKind.LessThan          , 1 , true }, // !Any
            { BinaryOperatorKind.GreaterThanOrEqual, 1 , false }, // Any
        };

        public static TheoryData<BinaryOperatorKind, int, bool, bool> LeftCount_Fixer_Predicate_TheoryData { get; } = Build_LeftCount_Fixer_Predicate_TheoryData();

        private static TheoryData<BinaryOperatorKind, int, bool, bool> Build_LeftCount_Fixer_Predicate_TheoryData()
        {
            var theoryData = new TheoryData<BinaryOperatorKind, int, bool, bool>();
            foreach (var withPredicate in new[] { false, true })
            {
                foreach (var fixerData in LeftCount_Fixer_TheoryData)
                {
                    theoryData.Add((BinaryOperatorKind)fixerData[0], (int)fixerData[1], withPredicate, (bool)fixerData[2]);
                }
            }
            return theoryData;
        }

        public static TheoryData<BinaryOperatorKind, int> LeftCount_Diagnostic_TheoryData { get; } = Build_LeftCount_Diagnostic_TheoryData();

        private static TheoryData<BinaryOperatorKind, int> Build_LeftCount_Diagnostic_TheoryData()
        {
            var theoryData = new TheoryData<BinaryOperatorKind, int>();
            foreach (var fixerData in LeftCount_Fixer_TheoryData)
            {
                theoryData.Add((BinaryOperatorKind)fixerData[0], (int)fixerData[1]);
            }
            return theoryData;
        }

        public static TheoryData<int, BinaryOperatorKind, bool> RightCount_Fixer_TheoryData { get; } = new TheoryData<int, BinaryOperatorKind, bool>
        {
            { 0, BinaryOperatorKind.Equals             , true }, // !Any
            { 0, BinaryOperatorKind.NotEquals          , false }, // Any
            { 0, BinaryOperatorKind.LessThan           , false }, // Any
            { 0, BinaryOperatorKind.GreaterThanOrEqual , true }, // !Any
            { 1, BinaryOperatorKind.GreaterThan        , true }, // !Any
            { 1, BinaryOperatorKind.LessThanOrEqual    , false }, // Any
        };

        public static TheoryData<int, BinaryOperatorKind, bool, bool> RightCount_Fixer_Predicate_TheoryData { get; } = Build_RightCount_Fixer_Predicate_TheoryData();

        private static TheoryData<int, BinaryOperatorKind, bool, bool> Build_RightCount_Fixer_Predicate_TheoryData()
        {
            var theoryData = new TheoryData<int, BinaryOperatorKind, bool, bool>();
            foreach (var withPredicate in new[] { false, true })
            {
                foreach (var fixerData in RightCount_Fixer_TheoryData)
                {
                    theoryData.Add((int)fixerData[0], (BinaryOperatorKind)fixerData[1], withPredicate, (bool)fixerData[2]);
                }
            }
            return theoryData;
        }

        public static TheoryData<int, BinaryOperatorKind> RightCount_Diagnostic_TheoryData { get; } = Build_RightCount_Diagnostic_TheoryData();

        private static TheoryData<int, BinaryOperatorKind> Build_RightCount_Diagnostic_TheoryData()
        {
            var theoryData = new TheoryData<int, BinaryOperatorKind>();
            foreach (var fixerData in RightCount_Fixer_TheoryData)
            {
                theoryData.Add((int)fixerData[0], (BinaryOperatorKind)fixerData[1]);
            }
            return theoryData;
        }
    }
}

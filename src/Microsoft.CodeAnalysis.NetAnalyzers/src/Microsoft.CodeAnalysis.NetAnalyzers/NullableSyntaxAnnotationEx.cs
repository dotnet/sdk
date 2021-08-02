// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Lightup
{
    internal static class NullableSyntaxAnnotationEx
    {
        public static SyntaxAnnotation? Oblivious { get; }
        public static SyntaxAnnotation? AnnotatedOrNotAnnotated { get; }

        static NullableSyntaxAnnotationEx()
        {
            var nullableSyntaxAnnotation = typeof(Workspace).Assembly.GetType("Microsoft.CodeAnalysis.CodeGeneration.NullableSyntaxAnnotation", throwOnError: false);
            if (nullableSyntaxAnnotation is object)
            {
                Oblivious = (SyntaxAnnotation?)nullableSyntaxAnnotation.GetField(nameof(Oblivious), BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                AnnotatedOrNotAnnotated = (SyntaxAnnotation?)nullableSyntaxAnnotation.GetField(nameof(AnnotatedOrNotAnnotated), BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            }
        }
    }
}

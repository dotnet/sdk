// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            if (nullableSyntaxAnnotation is not null)
            {
                Oblivious = (SyntaxAnnotation?)nullableSyntaxAnnotation.GetField(nameof(Oblivious), BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                AnnotatedOrNotAnnotated = (SyntaxAnnotation?)nullableSyntaxAnnotation.GetField(nameof(AnnotatedOrNotAnnotated), BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            }
        }
    }
}

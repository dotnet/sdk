// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace generator_lib
{
    [Generator]
    public class GreeterSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var code = @"
public class Greeter
{
    public void Greet() { }
}";

            context.AddSource("Greeter.cs", code);
        }
    }
}

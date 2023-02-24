// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//using System.CommandLine;
//using System.CommandLine.Parsing;

//public static class Program
//{
//    public static async Task Main(string[] args)
//    {
//        var delayOption = new Option<int>("--delay");
//        var messageOption = new Option<string>("--message");

//        var rootCommand = new RootCommand("Middleware example");
//        rootCommand.Add(delayOption);
//        rootCommand.Add(messageOption);

//        rootCommand.SetHandler(
//            (delayOptionValue, messageOptionValue) =>
//        {
//            DoRootCommand(delayOptionValue, messageOptionValue);
//        },
//            delayOption,
//            messageOption);

//        var commandLineBuilder = new CommandLineBuilder(rootCommand);

//        _ = commandLineBuilder.AddMiddleware(async (context, next) =>
//        {
//            context.Console.WriteLine("Hi!");
//            await next(context).ConfigureAwait(false);
//        });

//        commandLineBuilder.UseDefaults();
//        var parser = commandLineBuilder.Build();
//        await parser.InvokeAsync(args).ConfigureAwait(false);
//    }

//    public static void DoRootCommand(int delay, string message)
//    {
//        Console.WriteLine($"--delay = {delay}");
//        Console.WriteLine($"--message = {message}");
//    }
//}

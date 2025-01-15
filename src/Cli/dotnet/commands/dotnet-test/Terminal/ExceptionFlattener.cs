// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class ExceptionFlattener
{
    internal static FlatException[] Flatten(string? errorMessage, Exception? exception)
    {
        if (errorMessage is null && exception is null)
        {
            return Array.Empty<FlatException>();
        }

        string? message = !String.IsNullOrWhiteSpace(errorMessage) ? errorMessage : exception?.Message;
        string? type = exception?.GetType().FullName;
        string? stackTrace = exception?.StackTrace;
        var flatException = new FlatException(message, type, stackTrace);

        List<FlatException> flatExceptions = new()
        {
           flatException,
        };

        // Add all inner exceptions. This will flatten top level AggregateExceptions,
        // and all AggregateExceptions that are directly in AggregateExceptions, but won't expand
        // AggregateExceptions that are in non-aggregate exception inner exceptions.
        IEnumerable<Exception?> aggregateExceptions = exception switch
        {
            AggregateException aggregate => aggregate.Flatten().InnerExceptions,
            _ => [exception?.InnerException],
        };

        foreach (Exception? aggregate in aggregateExceptions)
        {
            Exception? currentException = aggregate;
            while (currentException is not null)
            {
                flatExceptions.Add(new FlatException(
                    aggregate?.Message,
                    aggregate?.GetType().FullName,
                    aggregate?.StackTrace));

                currentException = currentException.InnerException;
            }
        }

        return flatExceptions.ToArray();
    }
}

internal sealed record FlatException(string? ErrorMessage, string? ErrorType, string? StackTrace);

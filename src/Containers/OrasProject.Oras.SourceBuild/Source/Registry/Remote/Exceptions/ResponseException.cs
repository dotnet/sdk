// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Exceptions/ResponseException.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using OrasProject.Oras.Serialization;

namespace OrasProject.Oras.Registry.Remote.Exceptions;

/// <summary>
/// Exception thrown for HTTP responses from registry operations.
/// </summary>
public class ResponseException : HttpRequestException
{
    private class ErrorResponse
    {
        [JsonPropertyName("errors")]
        public required IList<Error> Errors { get; set; }
    }

    /// <summary>
    /// Gets the HTTP method used in the request.
    /// </summary>
    public HttpMethod? Method { get; }

    /// <summary>
    /// Gets the URI of the request.
    /// </summary>
    public Uri? RequestUri { get; }

    /// <summary>
    /// Gets the list of errors returned in the response.
    /// </summary>
    public IList<Error>? Errors { get; }

    private readonly string _formattedMessage;

    /// <summary>
    /// Gets the error message including HTTP details and registry errors.
    /// </summary>
    /// <remarks>
    /// Format: "{HTTP info}: {Custom message}; {Registry errors}"
    /// Where HTTP info is either "{Method} {URI} returned {StatusCode}" or "HTTP {StatusCode}"
    /// </remarks>
    public override string Message => _formattedMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseException"/> class from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response that caused the exception.</param>
    /// <param name="responseBody">The response body, used to parse registry errors.</param>
    public ResponseException(HttpResponseMessage response, string? responseBody = null)
        : this(response, responseBody, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseException"/> class with a specified error message.
    /// </summary>
    /// <param name="response">The HTTP response that caused the exception.</param>
    /// <param name="responseBody">The response body, used to parse registry errors.</param>
    /// <param name="message">The message that describes the error.</param>
    public ResponseException(HttpResponseMessage response, string? responseBody, string? message)
        : this(response, responseBody, response.StatusCode == HttpStatusCode.Unauthorized ? HttpRequestError.UserAuthenticationError : HttpRequestError.Unknown, message, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseException"/> class with a specified
    /// HTTP request error, error message, and inner exception.
    /// </summary>
    /// <param name="response">The HTTP response that caused the exception.</param>
    /// <param name="responseBody">The response body, used to parse registry errors.</param>
    /// <param name="httpRequestError">The HTTP request error associated with the response.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public ResponseException(HttpResponseMessage response, string? responseBody, HttpRequestError httpRequestError, string? message, Exception? inner)
        : base(httpRequestError, message, inner, response.StatusCode)
    {
        var request = response.RequestMessage;
        Method = request?.Method;
        RequestUri = request?.RequestUri;
        IList<Error>? errors = null;

        if (responseBody != null)
        {
            try
            {
                var errorResponse = OciJsonSerializer.Deserialize<ErrorResponse>(responseBody);
                errors = errorResponse?.Errors;
            }
            catch
            {
                // If deserialization fails, continue without setting Errors
            }
        }

        Errors = errors;
        _formattedMessage = FormatMessage(message);
    }

    /// <summary>
    /// Formats the exception message including HTTP details and registry errors.
    /// </summary>
    private string FormatMessage(string? customMessage)
    {
        var messageBuilder = new StringBuilder();

        // Add HTTP request and status information
        var statusCode = StatusCode ?? HttpStatusCode.InternalServerError;
        if (Method != null && RequestUri != null)
        {
            messageBuilder.Append($"{Method} {RequestUri} returned {(int)statusCode} {statusCode}");
        }
        else
        {
            messageBuilder.Append($"HTTP {(int)statusCode} {statusCode}");
        }

        // Add custom message if provided
        if (!string.IsNullOrWhiteSpace(customMessage))
        {
            messageBuilder.Append($": {customMessage}");
        }
        else
        {
            customMessage = null; // Treat empty messages as null for delimiter logic
        }

        // Add registry error details if available
        if (Errors != null && Errors.Count > 0)
        {
            // Add delimiter: use ":" after HTTP info or ";" after custom message
            messageBuilder.Append(customMessage == null ? ": " : "; ");

            if (Errors.Count == 1)
            {
                messageBuilder.Append(Errors[0].ToString());
            }
            else
            {
                // For multiple errors, format as "Error1; Error2; Error3"
                messageBuilder.Append(string.Join("; ", Errors.Select(error => error.ToString())));
            }
        }

        return messageBuilder.ToString();
    }
}

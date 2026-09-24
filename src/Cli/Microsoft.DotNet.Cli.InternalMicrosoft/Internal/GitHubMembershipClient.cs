// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Checks whether one of a bounded set of GitHub credentials belongs to the Microsoft organization.
/// A GitHub provider creates this client for one probe execution.
/// </summary>
internal sealed class GitHubMembershipClient
{
    private const string MicrosoftGitHubOrg = "microsoft";
    private const int MaxTokenCandidates = 5;

    private static readonly TimeSpan s_cancelledProbeDrainTimeout = TimeSpan.FromSeconds(1);

    private readonly InternalMicrosoftDetectionContext _context;

    internal GitHubMembershipClient(InternalMicrosoftDetectionContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Checks candidates concurrently and stops after the first Microsoft membership result.
    /// </summary>
    internal async Task<InternalMicrosoftProbeResult> CheckAnyAsync(
        IEnumerable<GitHubTokenCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var candidatesToCheck = candidates
            .DistinctBy(candidate => candidate.Token, StringComparer.Ordinal)
            .Take(MaxTokenCandidates)
            .ToArray();
        if (candidatesToCheck.Length == 0)
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        using var timeoutSource = new CancellationTokenSource(_context.GitHubCandidateTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var candidateTasks = candidatesToCheck
            .Select(candidate => CheckAsync(candidate.Token, linkedSource.Token))
            .ToList();
        InternalMicrosoftProbeFailure? failure = null;

        try
        {
            while (candidateTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(candidateTasks).WaitAsync(linkedSource.Token).ConfigureAwait(false);
                candidateTasks.Remove(completedTask);

                var result = await completedTask.ConfigureAwait(false);
                if (result.IsInternalMicrosoft)
                {
                    await linkedSource.CancelAsync().ConfigureAwait(false);
                    return result;
                }

                failure ??= result.Failure;
            }

            return failure is null
                ? InternalMicrosoftProbeResult.NotDetected
                : InternalMicrosoftProbeResult.Failed(failure);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeoutSource.IsCancellationRequested)
        {
            return InternalMicrosoftProbeResult.Failed(new(
                InternalMicrosoftProbeFailureCode.RequestFailed,
                InternalMicrosoftProbeFailureStage.GitHubCandidates,
                ExceptionType: nameof(TaskCanceledException)));
        }
        finally
        {
            await linkedSource.CancelAsync().ConfigureAwait(false);
            await DrainCandidateTasksAsync(candidateTasks).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks one GitHub credential without returning its login or token.
    /// </summary>
    internal async Task<InternalMicrosoftProbeResult> CheckAsync(
        string token,
        CancellationToken cancellationToken)
    {
        using var client = _context.GitHubHttpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(_context.GitHubHttpMessageHandler, disposeHandler: false);
        client.Timeout = _context.GitHubHttpTimeout;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("dotnet-sdk", _context.GitHubUserAgentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            using var userResponse = await client.GetAsync(
                "https://api.github.com/user",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!userResponse.IsSuccessStatusCode)
            {
                return HttpFailure("user", userResponse.StatusCode);
            }

            await using var userStream = await userResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var userDocument = await JsonDocument.ParseAsync(userStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!InternalMicrosoftDetectionUtilities.TryGetString(userDocument.RootElement, "login", out var login))
            {
                return InternalMicrosoftProbeResult.Failed(new(
                    InternalMicrosoftProbeFailureCode.JsonShape,
                    InternalMicrosoftProbeFailureStage.GitHubUser));
            }

            using var membershipResponse = await client.GetAsync(
                $"https://api.github.com/user/memberships/orgs/{MicrosoftGitHubOrg}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (membershipResponse.IsSuccessStatusCode)
            {
                await using var membershipStream = await membershipResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var membershipDocument = await JsonDocument.ParseAsync(membershipStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (InternalMicrosoftDetectionUtilities.TryGetString(
                        membershipDocument.RootElement,
                        "state",
                        out var state) &&
                    state.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    return new InternalMicrosoftProbeResult(true, null, null);
                }

                return InternalMicrosoftProbeResult.NotDetected;
            }

            if (membershipResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return HttpFailure("membership", membershipResponse.StatusCode);
            }

            using var publicMembershipResponse = await client.GetAsync(
                $"https://api.github.com/orgs/{MicrosoftGitHubOrg}/public_members/{Uri.EscapeDataString(login)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            return publicMembershipResponse.StatusCode switch
            {
                HttpStatusCode.NoContent => new InternalMicrosoftProbeResult(true, null, null),
                HttpStatusCode.NotFound => InternalMicrosoftProbeResult.NotDetected,
                _ => HttpFailure("public_membership", publicMembershipResponse.StatusCode)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return InternalMicrosoftProbeResult.Failed(new(
                InternalMicrosoftProbeFailureCode.HttpTimeout,
                InternalMicrosoftProbeFailureStage.GitHub,
                ExceptionType: nameof(TaskCanceledException)));
        }
        catch (Exception exception)
        {
            return InternalMicrosoftProbeResult.Failed(
                InternalMicrosoftDetectionUtilities.CreateExceptionFailure(
                    exception,
                    InternalMicrosoftProbeFailureStage.GitHub));
        }
    }

    private static async Task DrainCandidateTasksAsync(
        IReadOnlyList<Task<InternalMicrosoftProbeResult>> candidateTasks)
    {
        if (candidateTasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(candidateTasks).WaitAsync(s_cancelledProbeDrainTimeout).ConfigureAwait(false);
        }
        catch
        {
            // Candidate diagnostics report only the bounded aggregate failure.
        }
    }

    private static InternalMicrosoftProbeResult HttpFailure(string request, HttpStatusCode statusCode) =>
        InternalMicrosoftProbeResult.Failed(new(
            InternalMicrosoftProbeFailureCode.HttpStatus,
            request,
            HttpStatusCode: (int)statusCode));
}

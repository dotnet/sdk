// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier;

/// <summary>
/// Delegate signature for performing custom directory content verifications.
/// Expectable verification failures should be signaled with <see cref="TemplateVerificationException"/>.
/// API provider can either perform content enumeration, skipping and scrubbing by themselves (then the second argument can be ignored)
/// or the contentFetcher can be awaited to get the content of files - filtered by exclusion patterns and scrubbed by scrubbers.
/// </summary>
/// <param name="contentDirectory"></param>
/// <param name="contentFetcher"></param>
/// <returns></returns>
public delegate Task VerifyDirectory(string contentDirectory, Lazy<IAsyncEnumerable<(string FilePath, string ScrubbedContent)>> contentFetcher);

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Shared GitHub reference pattern sources so collection, validation, and
// resolution agree on what counts as an issue or pull-request reference.
export const ownerPattern = '[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?';
export const repositoryPattern = '[A-Za-z0-9_.-]{1,100}';
export const numberPattern = '[1-9][0-9]*';
export const referenceUrlPattern = `https://github\\.com/${ownerPattern}/${repositoryPattern}/(?:issues|pull)/${numberPattern}`;
export const shorthandPattern = `${ownerPattern}/${repositoryPattern}#${numberPattern}`;

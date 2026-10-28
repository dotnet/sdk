import {splitNonEmptyLines} from "./evidence-utils.mjs";

function exceptionTypeLine(lines)
{
  const line = lines.find(candidate => /(?:\b|\.)(?:\w+Exception|AssertionFailedException)\s*:/i.test(candidate));
  if (!line) return null;
  const match = line.match(/((?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*(?:Exception|AssertionFailedException))\s*:/i);
  return match?.[1] ?? null;
}

function stableDiagnosticLine(lines)
{
  const response = lines.find(line => /response status code does not indicate success/i.test(line));
  if (response) return response.slice(response.search(/response status code/i));
  return lines.find(line => /\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b/.test(line))
    ?? lines.find(line => /timed? ?out|connection refused|access denied|not found/i.test(line))
    ?? null;
}

export function validateErrorMessagePattern(pattern, logContent)
{
  const logLines = splitNonEmptyLines(logContent);
  let previousIndex = -1;
  for (const value of pattern)
  {
    const index = logLines.findIndex((line, candidateIndex) => candidateIndex > previousIndex && line.includes(value));
    if (index < 0) return {valid: false, missing: value};
    previousIndex = index;
  }
  return {valid: true, missing: null};
}

export function createTestKbeCandidate(test, fingerprint)
{
  const lines = splitNonEmptyLines(test.errorMessage);
  const testName = test.fullyQualifiedName || test.testName;
  const pattern = [testName, exceptionTypeLine(lines), stableDiagnosticLine(lines)].filter(Boolean);
  const validation = validateErrorMessagePattern(pattern, test.errorMessage);
  return {
    fingerprint,
    eligible: pattern.length >= 2 && validation.valid,
    errorMessage: pattern,
    buildRetry: /(?:status code[^\r\n]*(?:429|5\d\d)|timed? ?out|connection refused)/i.test(pattern.join("\n")),
    excludeConsoleLog: false,
    validation
  };
}

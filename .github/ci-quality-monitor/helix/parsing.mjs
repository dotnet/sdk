import {normalizeEvidenceText, splitNonEmptyLines} from "../evidence-utils.mjs";

export function parseHelixWorkItemReferences(messages)
{
  const pattern = /Work item '([^']+)' in job '(.+) \(([0-9a-f-]{36})\)' failed \([^,]+, exit code (-?\d+)\)\./i;
  return messages.flatMap(message =>
  {
    const match = `${message}`.match(pattern);
    return match ? [{
      workItem: match[1],
      queue: match[2],
      jobId: match[3],
      exitCode: Number.parseInt(match[4], 10)
    }] : [];
  });
}

export function classifyWorkItem(exitCode, consoleText, testFailures = [])
{
  if (testFailures.length > 0)
  {
    return {phase: "test-execution", failureType: "test-assertion", evidenceSources: ["helix-trx"]};
  }
  const text = `${consoleText ?? ""}`;
  if (/test run completed|detected test end tag/i.test(text)
    && /app_crash|timed_out|exit(?:ed)? with (?:80|143)/i.test(text))
  {
    return {
      phase: "test-post-processing",
      failureType: /app_crash/i.test(text) ? "process-crash" : "harness-error",
      evidenceSources: ["helix-console", "process-exit-code"]
    };
  }
  if (/workload timed out|run timed out|timed_out|timeout|timed out/i.test(text)
    || exitCode === 130 || exitCode === 143)
  {
    return {
      phase: "test-execution",
      failureType: "timeout",
      evidenceSources: ["helix-console", "process-exit-code"]
    };
  }
  if (/segmentation fault|stack overflow|core dump(?:ed)?|assert failed|app_crash|created crash dump/i.test(text)
    || [133, 134, 139].includes(exitCode))
  {
    return {
      phase: "test-execution",
      failureType: "process-crash",
      evidenceSources: ["helix-console", "process-exit-code"]
    };
  }
  if ([137, 143, 255].includes(exitCode))
  {
    return {
      phase: "test-execution",
      failureType: "process-termination",
      evidenceSources: ["helix-console", "process-exit-code"]
    };
  }
  if (/device_not_found|infrastructure error|agent connection|machine is not available/i.test(text)
    || [-4, 71, 81].includes(exitCode))
  {
    return {
      phase: "test-execution",
      failureType: "infrastructure-unavailable",
      evidenceSources: ["helix-console", "helix-work-item"]
    };
  }
  return {
    phase: "test-execution",
    failureType: "unknown-error",
    evidenceSources: ["helix-console", "helix-work-item"]
  };
}

export function summarizeHelixConsole(consoleText)
{
  const lines = splitNonEmptyLines(consoleText);
  const runningTestsMarker = lines.findIndex(line => /tests were still running when dump was taken/i.test(line));
  const markedActiveTest = runningTestsMarker >= 0
    ? lines.slice(runningTestsMarker + 1).find(line => /^\[[\d:.]+\]\s+\S/.test(line))
    : null;
  const relevant = lines
    .filter(line => /hang|timed? ?out|active test|currently running|process tree|test host crashed|exit code|dump|permission denied|diagnostics IPC/i.test(line))
    .filter(line => !/^[-*]?\s*(?:\/|[A-Za-z]:\\)/.test(line));
  const hostExitCode = [...lines].reverse().map(line => line.match(/exit code(?: is)?\s*['"]?(-?\d+)/i)?.[1])
    .find(Boolean);
  const activeTest = markedActiveTest
    ?? [...relevant].reverse().find(line => /active test|currently running|has been running/i.test(line));
  if (activeTest && !relevant.includes(activeTest)) relevant.push(activeTest);
  const dumpFailures = relevant.filter(line => /dump.*(?:fail|error)|permission denied|diagnostics IPC/i.test(line)).slice(-4);
  return {
    activeTest: activeTest ? normalizeEvidenceText(activeTest) : null,
    hostExitCode: hostExitCode ? Number(hostExitCode) : null,
    hangEvidence: [...new Set(relevant.slice(-12).map(line => normalizeEvidenceText(line)))],
    dumpFailures: [...new Set(dumpFailures.map(line => normalizeEvidenceText(line)))]
  };
}

export function summarizeTestMechanism(errorMessage, outcome)
{
  const lines = splitNonEmptyLines(errorMessage);
  const salient = lines.filter(line => /exception|error|expected|actual|exit code|status code|timed? ?out|failed/i.test(line));
  return normalizeEvidenceText((salient.length > 0 ? salient : lines).slice(0, 8).join("\n") || `${outcome} test result`);
}

export function summarizeSharedTestMechanism(errorMessage, outcome)
{
  const lines = splitNonEmptyLines(errorMessage);
  const diagnosticLines = lines.filter(line => /\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b/i.test(line));
  const responseLines = lines.filter(line => /response status code/i.test(line))
    .map(line => line.slice(line.search(/response status code/i)));
  const operationalLines = responseLines.length > 0 ? responseLines
    : lines.filter(line => /service unavailable|timed? ?out|connection|refused|not found|access denied/i.test(line));
  const exceptionLines = lines.filter(line => !/^Test method .+ threw exception:?$/i.test(line))
    .filter(line => /(?:system\.)?\w+exception/i.test(line));
  const rootCauseLines = diagnosticLines.length > 0 ? diagnosticLines
    : operationalLines.length > 0 ? operationalLines
      : exceptionLines;
  const distinctLines = [...new Set(rootCauseLines.length > 0 ? rootCauseLines : lines.slice(-3))];
  return normalizeEvidenceText(distinctLines.slice(0, 4).join("\n") || `${outcome} test result`);
}

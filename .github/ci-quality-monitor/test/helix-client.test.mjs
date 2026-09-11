import assert from "node:assert/strict";
import test from "node:test";

import {HelixEvidenceClient} from "../helix/client.mjs";

const reference = {
  jobId: "00000000-0000-0000-0000-000000000000",
  workItem: "tests.dll",
  queue: "Windows x64",
  exitCode: 1
};

function trxResult({name = "Example", outcome = "Failed", message = "Expected true but found false."} = {})
{
  return `<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
    <Results><UnitTestResult testId="1" testName="${name}" outcome="${outcome}">
      <Output><ErrorInfo><Message>${message}</Message></ErrorInfo></Output>
    </UnitTestResult></Results>
  </TestRun>`;
}

async function collect({files, bodies, consoleText = "work item failed", exitCode = 1})
{
  const fetchImplementation = async url =>
  {
    if (url.endsWith("/console")) return new Response(consoleText, {status: 200});
    if (url.includes("/workitems/"))
    {
      return new Response(JSON.stringify({ExitCode: exitCode, Files: files}), {status: 200});
    }
    return new Response(bodies[url], {status: 200});
  };
  return new HelixEvidenceClient(fetchImplementation).collectWorkItemObservations(reference);
}

test("prefers TRX over unrelated XML artifacts", async () =>
{
  const observations = await collect({
    files: [
      {FileName: "coverage.xml", Uri: "https://files/coverage.xml"},
      {FileName: "results.trx", Uri: "https://files/results.trx"}
    ],
    bodies: {
      "https://files/coverage.xml": "<coverage />",
      "https://files/results.trx": trxResult()
    }
  });

  assert.equal(observations[0].kind, "test");
  assert.equal(observations[0].component, "Example");
});

test("uses explicit TRX timeout and aborted outcomes", async () =>
{
  for (const [outcome, failureType] of [["Timeout", "timeout"], ["Aborted", "process-termination"]])
  {
    const observations = await collect({
      files: [{FileName: "results.trx", Uri: "https://files/results.trx"}],
      bodies: {"https://files/results.trx": trxResult({outcome, message: ""})}
    });
    assert.equal(observations[0].failureType, failureType);
  }
});

test("does not treat bare assertion numbers as HTTP status codes", async () =>
{
  const observations = await collect({
    files: [{FileName: "results.trx", Uri: "https://files/results.trx"}],
    bodies: {"https://files/results.trx": trxResult({message: "Expected 500 but found 499."})}
  });

  assert.equal(observations[0].failureType, "test-assertion");
});

test("preserves a teardown hang alongside a failed assertion", async () =>
{
  const observations = await collect({
    files: [{FileName: "results.trx", Uri: "https://files/results.trx"}],
    bodies: {"https://files/results.trx": trxResult()},
    consoleText: "Hang timeout expired. Test host crashed. exit code is 137",
    exitCode: 137
  });

  assert.deepEqual(observations.map(observation => observation.failureType), ["test-assertion", "timeout"]);
});
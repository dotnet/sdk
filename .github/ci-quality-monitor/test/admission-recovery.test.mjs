import assert from "node:assert/strict";
import {createRequire} from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {collectAdmissionDiagnostics, recoverStalledAdmissions} = require("../admission-recovery.js");
const now = Date.parse("2026-11-04T20:00:00Z");
const context = {
    repo: {owner: "dotnet", repo: "sdk"},
    payload: {repository: {default_branch: "main"}},
};

function createHarness()
{
    const state = {
        run: {id: 42, run_attempt: 1, workflow_id: 7, head_branch: "main", status: "waiting",
            path: ".github/workflows/ci-quality-monitor.lock.yml", html_url: "https://github.com/dotnet/sdk/actions/runs/42"},
        jobs: [{id: 101, node_id: "check-node", name: "pre_activation", status: "waiting",
            runner_id: 0, steps: [], created_at: "2026-11-04T19:00:00Z"}],
        environment: {name: "copilot-pat-pool", protection_rules: [{type: "branch_policy"}]},
        customProtectionRules: {total_count: 0, custom_deployment_protection_rules: []},
        pending: [],
        cancelled: [],
        logs: [],
    };
    const actions = {
        getWorkflow: async () => ({data: {id: 7}}),
        listWorkflowRuns: async () => [state.run],
        listJobsForWorkflowRun: async () => state.jobs,
        getWorkflowRun: async () => ({data: state.run}),
        getPendingDeploymentsForRun: async () => ({data: state.pending}),
        getReviewsForRun: async () => ({data: []}),
        cancelWorkflowRun: async args => state.cancelled.push(args),
    };
    const github = {
        rest: {actions, repos: {getEnvironment: async () => ({data: state.environment})}},
        paginate: (method, args) => method(args),
        request: async () => ({data: state.customProtectionRules}),
        graphql: async () => ({
            node: {databaseId: 101, status: "WAITING",
                deployment: {databaseId: 99, environment: "copilot-pat-pool", state: "WAITING"}},
        }),
    };
    const core = {info: message => state.logs.push(message), warning: message => state.logs.push(message)};
    return {github, core, context, now, state};
}

test("records an old unassigned admission and cancels only the matching investigator run", async () =>
{
    const harness = createHarness();
    const report = await collectAdmissionDiagnostics(harness);

    assert.equal(report.admissions.length, 1);
    assert.deepEqual(report.admissions[0].stalledJobIds, [101]);
    assert.equal(report.admissions[0].deployments[0].deployment.state, "WAITING");
    const cancelled = await recoverStalledAdmissions({...harness, report, dryRun: false});
    assert.deepEqual(cancelled, [42]);
    assert.deepEqual(harness.state.cancelled, [{owner: "dotnet", repo: "sdk", run_id: 42}]);
    assert.ok(harness.state.logs.some(message => message.includes("deliberate replay")));
});

test("does not mistake queued, assigned, recent or executing jobs for stale admission", async () =>
{
    const variants = [
        {name: "collect"},
        {status: "queued"},
        {status: "in_progress"},
        {runner_id: 123},
        {steps: [{status: "in_progress"}]},
        {created_at: "2026-11-04T19:30:01Z"},
    ];
    for (const variant of variants)
    {
        const harness = createHarness();
        Object.assign(harness.state.jobs[0], variant);
        const report = await collectAdmissionDiagnostics(harness);
        assert.deepEqual(report.admissions, [], JSON.stringify(variant));
    }
});

test("recognizes the second PAT admission at the thirty-minute threshold", async () =>
{
    const harness = createHarness();
    Object.assign(harness.state.jobs[0], {name: "pat_pool", created_at: "2026-11-04T19:30:00Z"});
    const report = await collectAdmissionDiagnostics(harness);
    assert.deepEqual(report.admissions[0].stalledJobIds, [101]);
});

test("never targets another workflow, branch or completed run", async () =>
{
    for (const variant of [{workflow_id: 8}, {path: "other.yml"}, {head_branch: "release/11.0.2xx"}, {status: "completed"}])
    {
        const harness = createHarness();
        Object.assign(harness.state.run, variant);
        const report = await collectAdmissionDiagnostics(harness);
        assert.deepEqual(report.admissions, []);
    }
});

test("records but does not cancel reviewer, timer or custom-protection waits", async () =>
{
    for (const type of ["required_reviewers", "wait_timer", "custom_deployment_protection_rule"])
    {
        const harness = createHarness();
        harness.state.environment.protection_rules.push({type});
        const report = await collectAdmissionDiagnostics(harness);
        assert.equal(report.admissions[0].recoverable, false);
        await recoverStalledAdmissions({...harness, report, dryRun: false});
        assert.deepEqual(harness.state.cancelled, []);
    }
});

test("pending deployment reviews and timers independently prevent recovery", async () =>
{
    for (const pending of [
        {wait_timer: 1, reviewers: []},
        {wait_timer: 0, reviewers: [{type: "User"}]},
        {wait_timer: 0, reviewers: [], environment: {name: "production"}},
    ])
    {
        const harness = createHarness();
        harness.state.pending = [{environment: {name: "copilot-pat-pool"}, ...pending}];
        const report = await collectAdmissionDiagnostics(harness);
        assert.equal(report.admissions[0].recoverable, false);
        await recoverStalledAdmissions({...harness, report, dryRun: false});
        assert.deepEqual(harness.state.cancelled, []);
    }
});

test("custom protection apps are checked even when omitted from environment.protection_rules", async () =>
{
    const harness = createHarness();
    harness.state.customProtectionRules = {
        total_count: 1, custom_deployment_protection_rules: [{id: 55}],
    };
    const report = await collectAdmissionDiagnostics(harness);
    assert.equal(report.admissions[0].recoverable, false);
    await recoverStalledAdmissions({...harness, report, dryRun: false});
    assert.deepEqual(harness.state.cancelled, []);
});

test("requires a waiting deployment in the expected environment, not just a waiting job", async () =>
{
    for (const deployment of [null, {environment: "production", state: "WAITING"},
        {environment: "copilot-pat-pool", state: "QUEUED"}])
    {
        const harness = createHarness();
        harness.github.graphql = async () => ({node: {status: "WAITING", deployment}});
        const report = await collectAdmissionDiagnostics(harness);
        assert.equal(report.admissions[0].recoverable, false);
        await recoverStalledAdmissions({...harness, report, dryRun: false});
        assert.deepEqual(harness.state.cancelled, []);
    }
});

test("dry run is the helper default and makes no cancellation request", async () =>
{
    const harness = createHarness();
    const report = await collectAdmissionDiagnostics(harness);
    await recoverStalledAdmissions({...harness, report});
    assert.deepEqual(harness.state.cancelled, []);
    assert.ok(harness.state.logs.some(message => message.includes("Dry run: would cancel")));
});

test("rechecks current state, attempt, job and protection rules before cancellation", async () =>
{
    const changes = [
        state => {state.run = {...state.run, status: "completed"};},
        state => {state.run = {...state.run, run_attempt: 2};},
        state => {state.jobs = [{...state.jobs[0], status: "in_progress", runner_id: 123}];},
        state => {state.jobs = [{...state.jobs[0], id: 102}];},
        state => {state.environment = {...state.environment, protection_rules: [{type: "required_reviewers"}]};},
    ];
    for (const change of changes)
    {
        const harness = createHarness();
        const report = await collectAdmissionDiagnostics(harness);
        change(harness.state);
        await recoverStalledAdmissions({...harness, report, dryRun: false});
        assert.deepEqual(harness.state.cancelled, []);
    }
});

test("API failures surface instead of cancelling without diagnostics", async () =>
{
    const harness = createHarness();
    harness.github.rest.actions.getPendingDeploymentsForRun = async () => {throw new Error("HTTP 403");};
    await assert.rejects(collectAdmissionDiagnostics(harness), /HTTP 403/);
    assert.deepEqual(harness.state.cancelled, []);
});

test("a run that resumes during diagnostic collection is not cancelled", async () =>
{
    const harness = createHarness();
    const report = await collectAdmissionDiagnostics(harness);
    harness.github.graphql = async () =>
    {
        harness.state.run = {...harness.state.run, status: "in_progress"};
        return {node: {deployment: {state: "IN_PROGRESS"}}};
    };
    await recoverStalledAdmissions({...harness, report, dryRun: false});
    assert.deepEqual(harness.state.cancelled, []);
});

test("invalid admission timestamps surface instead of silently skipping a stall", async () =>
{
    const harness = createHarness();
    harness.state.jobs[0].created_at = "invalid";
    await assert.rejects(collectAdmissionDiagnostics(harness), /Invalid admission creation time/);
});

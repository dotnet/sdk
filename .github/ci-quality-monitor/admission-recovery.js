const workflowFile = "ci-quality-monitor.lock.yml";
const workflowPath = `.github/workflows/${workflowFile}`;
const environmentName = "copilot-pat-pool";
const admissionJobs = new Set(["pre_activation", "pat_pool"]);
const minimumWaitMs = 30 * 60 * 1000;

function isMonitorRun(run, workflowId, branch)
{
    return run.workflow_id === workflowId
        && run.path === workflowPath
        && run.head_branch === branch
        && run.status === "waiting";
}

function findStalledJobs(jobs, now)
{
    return jobs.filter(job =>
    {
        if (!admissionJobs.has(job.name) || job.status !== "waiting"
            || job.runner_id || job.steps.length !== 0)
        {
            return false;
        }

        const createdAt = Date.parse(job.created_at);
        if (!Number.isFinite(createdAt))
        {
            throw new Error(`Invalid admission creation time for job ${job.id}`);
        }

        return now - createdAt >= minimumWaitMs;
    });
}

function allowsRecovery(environment, pendingDeployments, customProtectionRules, deployments)
{
    return environment.name === environmentName
        && environment.protection_rules.every(rule => rule.type === "branch_policy")
        && customProtectionRules.total_count === 0
        && customProtectionRules.custom_deployment_protection_rules.length === 0
        && deployments.every(check => check?.status === "WAITING"
            && check.deployment?.environment === environmentName && check.deployment.state === "WAITING")
        && pendingDeployments.every(pending => pending.environment.name === environmentName
            && pending.wait_timer === 0 && pending.reviewers.length === 0);
}

async function inspectAdmission({github, repo, run, now})
{
    const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
        ...repo, run_id: run.id, filter: "latest", per_page: 100,
    });
    const stalledJobs = findStalledJobs(jobs, now);
    if (stalledJobs.length === 0)
    {
        return null;
    }

    const {data: environment} = await github.rest.repos.getEnvironment({
        ...repo, environment_name: environmentName,
    });
    const {data: customProtectionRules} = await github.request(
        "GET /repos/{owner}/{repo}/environments/{environment_name}/deployment_protection_rules",
        {...repo, environment_name: environmentName},
    );
    const {data: pendingDeployments} = await github.rest.actions.getPendingDeploymentsForRun({
        ...repo, run_id: run.id,
    });
    const {data: reviews} = await github.rest.actions.getReviewsForRun({...repo, run_id: run.id});
    const deployments = [];
    for (const job of stalledJobs)
    {
        const data = await github.graphql(`query($id: ID!) {
            node(id: $id) { ... on CheckRun {
                databaseId status title summary text
                deployment { databaseId environment state latestStatus { state description logUrl } }
                pendingDeploymentRequest { waitTimer waitTimerStartedAt currentUserCanApprove }
            } }
        }`, {id: job.node_id});
        deployments.push(data.node);
    }

    return {
        run, jobs, environment, customProtectionRules, pendingDeployments, reviews, deployments,
        stalledJobIds: stalledJobs.map(job => job.id),
        recoverable: allowsRecovery(environment, pendingDeployments, customProtectionRules, deployments),
    };
}

async function collectAdmissionDiagnostics({github, context, core, now = Date.now()})
{
    const repo = context.repo;
    const branch = context.payload.repository.default_branch;
    const {data: workflow} = await github.rest.actions.getWorkflow({
        ...repo, workflow_id: workflowFile,
    });
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
        ...repo, workflow_id: workflow.id, branch, status: "waiting", per_page: 100,
    });
    const admissions = [];
    for (const run of runs.filter(run => isMonitorRun(run, workflow.id, branch)))
    {
        const admission = await inspectAdmission({github, repo, run, now});
        if (admission)
        {
            admissions.push(admission);
            core.warning(`Run ${run.id} has stalled PAT admission; recoverable=${admission.recoverable}`);
        }
    }

    core.info(`Found ${admissions.length} stalled admission(s).`);
    return {collectedAt: new Date(now).toISOString(), workflowId: workflow.id, branch, admissions};
}

async function recoverStalledAdmissions({github, context, core, report, dryRun = true, now = Date.now()})
{
    const repo = context.repo;
    const branch = context.payload.repository.default_branch;
    const {data: workflow} = await github.rest.actions.getWorkflow({
        ...repo, workflow_id: workflowFile,
    });
    const cancelled = [];
    for (const admission of report.admissions)
    {
        const {data: run} = await github.rest.actions.getWorkflowRun({
            ...repo, run_id: admission.run.id,
        });
        if (!isMonitorRun(run, workflow.id, branch) || run.run_attempt !== admission.run.run_attempt)
        {
            core.info(`Run ${run.id} changed state or attempt; not cancelling.`);
            continue;
        }

        const current = await inspectAdmission({github, repo, run, now});
        if (!admission.recoverable || !current?.recoverable
            || !current.stalledJobIds.some(id => admission.stalledJobIds.includes(id)))
        {
            core.warning(`Run ${run.id} no longer matches the recorded unreviewed admission stall; not cancelling.`);
            continue;
        }

        const {data: latestRun} = await github.rest.actions.getWorkflowRun({...repo, run_id: run.id});
        if (!isMonitorRun(latestRun, workflow.id, branch) || latestRun.run_attempt !== run.run_attempt)
        {
            core.info(`Run ${run.id} changed during diagnosis; not cancelling.`);
            continue;
        }

        if (dryRun)
        {
            core.warning(`Dry run: would cancel stalled run ${run.id}.`);
            continue;
        }

        await github.rest.actions.cancelWorkflowRun({...repo, run_id: run.id});
        cancelled.push(run.id);
        core.warning(`Requested cancellation of ${run.html_url}. Checkpointed builds require deliberate replay.`);
    }

    return cancelled;
}

module.exports = {collectAdmissionDiagnostics, recoverStalledAdmissions};

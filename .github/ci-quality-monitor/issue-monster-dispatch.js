function parseIssueNumber(value)
{
    const issueNumber = Number(value);
    return Number.isSafeInteger(issueNumber) && issueNumber > 0 ? issueNumber : null;
}

function getCreatedIssueNumbers({temporaryIdMapInput, createdIssueNumberInput, repository})
{
    let temporaryIdMap = {};
    if (temporaryIdMapInput)
    {
        try
        {
            temporaryIdMap = JSON.parse(temporaryIdMapInput);
        } catch (error)
        {
            throw new Error(`Invalid safe-output temporary ID map: ${error.message}`, {cause: error});
        }
    }

    if (!temporaryIdMap || Array.isArray(temporaryIdMap) || typeof temporaryIdMap !== "object")
    {
        throw new Error("Invalid safe-output temporary ID map: expected an object");
    }

    const issueNumbers = new Set();
    for (const item of Object.values(temporaryIdMap))
    {
        if (item?.repo !== repository)
        {
            continue;
        }

        const issueNumber = parseIssueNumber(item.number);
        if (issueNumber)
        {
            issueNumbers.add(issueNumber);
        }
    }

    const fallbackIssueNumber = parseIssueNumber(createdIssueNumberInput);
    if (fallbackIssueNumber)
    {
        issueNumbers.add(fallbackIssueNumber);
    }

    return [...issueNumbers];
}

const runMarkerPattern = /^<!-- gh-aw-agentic-workflow: .*, id: (\d+), workflow_id: ci-quality-monitor,/m;

function isCreatedByRun(issue, runId)
{
    return !issue.pull_request && runMarkerPattern.exec(issue.body ?? "")?.[1] === String(runId);
}

// gh-aw v0.89 no longer exports the temporary ID map, so later issues from a
// multi-issue run are found through the provenance marker gh-aw writes into
// every issue body. The REST list is used because search indexing lags.
async function findRunCreatedIssueNumbers({github, context, label})
{
    const {data: issues} = await github.rest.issues.listForRepo({
        ...context.repo,
        labels: label,
        state: "open",
        sort: "created",
        direction: "desc",
        per_page: 100,
    });

    return issues
        .filter(issue => isCreatedByRun(issue, context.runId))
        .map(issue => issue.number);
}

async function dispatchCreatedIssues({
    github,
    context,
    core,
    temporaryIdMapInput,
    createdIssueNumberInput,
    ref,
    label = "live-build-incident",
})
{
    const declaredIssueNumbers = getCreatedIssueNumbers({
        temporaryIdMapInput,
        createdIssueNumberInput,
        repository: `${context.repo.owner}/${context.repo.repo}`,
    });
    const discoveredIssueNumbers = declaredIssueNumbers.length > 0
        ? await findRunCreatedIssueNumbers({github, context, label})
        : [];
    const issueNumbers = [...new Set([...declaredIssueNumbers, ...discoveredIssueNumbers])]
        .sort((left, right) => left - right);

    for (const issueNumber of issueNumbers)
    {
        await github.rest.actions.createWorkflowDispatch({
            ...context.repo,
            workflow_id: "issue-monster.lock.yml",
            ref,
            inputs: {issue_number: String(issueNumber)},
        });
        core.info(`Dispatched Issue Monster for issue #${issueNumber}`);
    }

    return issueNumbers;
}

module.exports = {
    dispatchCreatedIssues,
    getCreatedIssueNumbers,
    isCreatedByRun,
};

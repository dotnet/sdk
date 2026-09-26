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

async function dispatchCreatedIssues({
    github,
    context,
    core,
    temporaryIdMapInput,
    createdIssueNumberInput,
    ref,
})
{
    const issueNumbers = getCreatedIssueNumbers({
        temporaryIdMapInput,
        createdIssueNumberInput,
        repository: `${context.repo.owner}/${context.repo.repo}`,
    });

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
};

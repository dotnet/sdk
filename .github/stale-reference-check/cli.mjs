import { expandContext, prepare, printBatch, record } from "./workflow.mjs";

const [command, ...args] = process.argv.slice(2);
const repoRoot = process.cwd();

switch (command)
{
    case "collect":
        if (args.length !== 0)
        {
            throw new Error("collect takes no arguments.");
        }
        await prepare({ repoRoot, refreshCache: process.env.REFRESH_CACHE === "true" });
        break;
    case "read-batch":
        if (args.length > 1 || (args.length === 1 && !/^[1-9]\d*$/.test(args[0])))
        {
            throw new Error("Usage: read-batch [PAGE]");
        }
        await printBatch(repoRoot, args.length ? Number(args[0]) : 1);
        break;
    case "context":
        if (args.length !== 3 || !/^\d+$/.test(args[1]) || !/^\d+$/.test(args[2]))
        {
            throw new Error("Usage: context CANDIDATE_ID START_LINE END_LINE");
        }
        await expandContext(repoRoot, {
            candidateId: args[0],
            startLine: Number(args[1]),
            endLine: Number(args[2]),
        });
        break;
    case "record":
        if (args.length !== 0)
        {
            throw new Error("record takes no arguments.");
        }
        await record(repoRoot, process.env.GH_AW_AGENT_OUTPUT);
        break;
    default:
        throw new Error("Expected collect, read-batch, context, or record.");
}

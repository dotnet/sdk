const fs = require("fs");

if (Object.keys(process.env).some(name => /token|secret|authorization|(^|_)pat(_|$)/i.test(name))) {
  throw new Error("Parser environment contains credential-like variables");
}

const [routingPath, responsePath, outputPath] = process.argv.slice(2);
if (!routingPath || !responsePath || !outputPath) {
  throw new Error("Expected expanded routing, load response, and output paths");
}

const routing = JSON.parse(fs.readFileSync(routingPath, "utf8"));
if (fs.statSync(responsePath).size > 1048576) {
  throw new Error("Load response exceeds the supported size");
}
const response = JSON.parse(fs.readFileSync(responsePath, "utf8"));
if (!response || typeof response.data !== "object" || response.data === null ||
    (Array.isArray(response.errors) && response.errors.length > 0)) {
  throw new Error("GitHub returned an invalid load response");
}

const loads = {};
for (const [index, login] of routing.candidates.entries()) {
  const count = response.data[`u${index}`]?.issueCount;
  if (!Number.isSafeInteger(count) || count < 0) {
    throw new Error("GitHub returned an invalid issue count");
  }
  loads[login] = count;
}

const snapshot = {
  areas: routing.areas,
  team_members: routing.team_members,
  loads,
  cutoff_date: routing.cutoff_date,
  fallback_team: routing.fallback_team
};
const serialized = JSON.stringify(snapshot);
if (serialized.length > 400000) {
  throw new Error("Routing snapshot exceeds the supported output size");
}
fs.writeFileSync(outputPath, serialized);

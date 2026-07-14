const fs = require("fs");
const path = require("path");

const AREA_LABEL = /^Area-[A-Za-z0-9][A-Za-z0-9 .+_-]{0,79}$/;
const OWNER = /^@[A-Za-z0-9-]+(?:\/[A-Za-z0-9_.-]+)?$/;
const PATTERN = /^[A-Za-z0-9_./*?![\]-]+$/;

function fail(message) {
  throw new Error(message);
}

function assertEmptyEnvironment() {
  if (Object.keys(process.env).length !== 0) {
    fail("Parser environment must be empty");
  }
}

function readBounded(filePath, maximumBytes) {
  const stat = fs.statSync(filePath);
  if (!stat.isFile() || stat.size > maximumBytes) {
    fail("Input file exceeds the supported size");
  }
  return fs.readFileSync(filePath, "utf8");
}

function readAreaLabels(rawDirectory) {
  const labelFiles = fs.readdirSync(rawDirectory)
    .filter(name => /^labels-[0-9]+\.json$/.test(name))
    .sort();
  if (labelFiles.length === 0 || labelFiles.length > 10) {
    fail("Unexpected label response count");
  }

  const labels = new Set();
  for (const fileName of labelFiles) {
    const response = JSON.parse(readBounded(path.join(rawDirectory, fileName), 1048576));
    if (!Array.isArray(response) || response.length > 100) {
      fail("Invalid labels response");
    }
    for (const item of response) {
      if (typeof item?.name === "string" && AREA_LABEL.test(item.name)) {
        labels.add(item.name);
      }
    }
  }
  if (labels.size === 0 || labels.size > 200) {
    fail("Unexpected Area label count");
  }
  return [...labels].sort((left, right) => left.localeCompare(right));
}

function escapeRegularExpression(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function labelsInHeading(heading, areaLabels) {
  return areaLabels.filter(label => {
    const escaped = escapeRegularExpression(label);
    return new RegExp(`(?:^|\\s)${escaped}(?=\\s+Area-|\\s*$)`, "i").test(heading);
  });
}

function getOrCreateArea(areas, areaLabel) {
  if (!areas.has(areaLabel)) {
    areas.set(areaLabel, { owners: new Set(), rules: [] });
  }
  return areas.get(areaLabel);
}

function addRule(area, pattern, ruleOwners) {
  for (const owner of ruleOwners) {
    area.owners.add(owner);
  }
  area.rules.push({
    pattern,
    individual_owners: ruleOwners.filter(owner => !owner.includes("/")),
    team_owners: ruleOwners.filter(owner => owner.includes("/"))
  });
  if (area.owners.size > 100 || area.rules.length > 200) {
    fail("CODEOWNERS area exceeds the supported owner or rule count");
  }
}

function parseCodeowners(codeowners, areaLabels) {
  const areas = new Map();
  let activeAreas = [];

  for (const line of codeowners.split(/\r?\n/)) {
    const comment = line.match(/^\s*#\s*(.*)$/);
    if (comment) {
      activeAreas = labelsInHeading(comment[1], areaLabels);
      for (const areaLabel of activeAreas) {
        getOrCreateArea(areas, areaLabel);
      }
      continue;
    }
    if (activeAreas.length === 0 || !line.trim()) continue;

    const fields = line.trim().split(/\s+/);
    const pattern = fields[0];
    if (pattern.length > 200 || !PATTERN.test(pattern)) continue;
    const ruleOwners = fields.slice(1).filter(owner => OWNER.test(owner));
    if (ruleOwners.length === 0) continue;
    for (const areaLabel of activeAreas) {
      addRule(getOrCreateArea(areas, areaLabel), pattern, ruleOwners);
    }
  }
  return areas;
}

function serializeAreas(areas) {
  const result = {};
  for (const [areaLabel, area] of [...areas].sort(([left], [right]) => left.localeCompare(right))) {
    const owners = [...area.owners].sort((left, right) => left.localeCompare(right));
    result[areaLabel] = {
      individual_owners: owners.filter(owner => !owner.includes("/")),
      team_owners: owners.filter(owner => owner.includes("/")),
      rules: area.rules
    };
  }
  return result;
}

function createLoadQuery(repository, candidates, cutoffDate) {
  if (candidates.length === 0) {
    return { query: "query { rateLimit { cost } }", variables: {} };
  }
  const declarations = candidates.map((_, index) => `$query${index}: String!`).join(", ");
  const fields = candidates.map((_, index) =>
    `owner${index}: search(query: $query${index}, type: ISSUE, first: 1) { issueCount }`).join("\n");
  const variables = {};
  candidates.forEach((owner, index) => {
    const login = owner.slice(1);
    variables[`query${index}`] =
      `repo:${repository} is:issue is:open assignee:${login} created:>=${cutoffDate}`;
  });
  return { query: `query(${declarations}) {\n${fields}\n}`, variables };
}

function main() {
  assertEmptyEnvironment();
  const [rawDirectory, workDirectory, repository] = process.argv.slice(2);
  if (!rawDirectory || !workDirectory || !/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository)) {
    fail("Invalid parser arguments");
  }

  const codeowners = readBounded(path.join(rawDirectory, "CODEOWNERS"), 262144);
  const areas = serializeAreas(parseCodeowners(codeowners, readAreaLabels(rawDirectory)));
  const candidates = [...new Set(Object.values(areas).flatMap(area => area.individual_owners))]
    .sort((left, right) => left.localeCompare(right));
  if (candidates.length > 100) fail("Too many individual owners");

  const cutoff = new Date();
  cutoff.setUTCDate(cutoff.getUTCDate() - 14);
  const cutoffDate = cutoff.toISOString().slice(0, 10);
  fs.mkdirSync(workDirectory, { recursive: true });
  fs.writeFileSync(path.join(workDirectory, "routing-base.json"),
    JSON.stringify({ schema_version: 1, cutoff_date: cutoffDate, areas, candidates }));
  fs.writeFileSync(path.join(workDirectory, "load-query.json"),
    JSON.stringify(createLoadQuery(repository, candidates, cutoffDate)));
}

main();

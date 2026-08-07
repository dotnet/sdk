import { spawnSync } from "node:child_process";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const MODULE_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));

export function parseTestResultXml(xml, command = process.env.PYTHON || (process.platform === "win32" ? "python" : "python3")) {
  const parser = path.join(MODULE_DIRECTORY, "parse-test-results.py");
  const result = spawnSync(command, [parser], { input: xml, encoding: "utf8", maxBuffer: 2 * 1024 * 1024 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`TRX parser failed: ${result.stderr.trim()}`);
  return JSON.parse(result.stdout);
}
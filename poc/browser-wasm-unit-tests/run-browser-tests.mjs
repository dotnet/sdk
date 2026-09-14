import { spawn } from "node:child_process";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import process from "node:process";
import { chromium } from "playwright";

const pocDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(pocDirectory, "..", "..");
const projectDirectory = path.join(
  repoRoot,
  "test",
  "TestAssets",
  "TestProjects",
  "BlazorWasmTestApp",
);
const projectPath = path.join(projectDirectory, "BlazorWasmTestApp.csproj");
const skipBuild = process.env.BROWSER_WASM_POC_SKIP_BUILD === "1";
const dotnetPath = resolveDotnetPath(repoRoot);
const projectExtensionsPath = ensureTrailingSeparator(
  path.join(
    repoRoot,
    "artifacts",
    "tmp",
    "wasm-standalone",
    "project-extensions",
  ),
);
const intermediateOutputPath = ensureTrailingSeparator(
  path.join(
    repoRoot,
    "artifacts",
    "tmp",
    "wasm-standalone",
    "obj",
  ),
);
const outputPath = ensureTrailingSeparator(
  path.join(
    repoRoot,
    "artifacts",
    "tmp",
    "wasm-standalone",
    "bin",
  ),
);
const standaloneProperties = [
  `-p:MSBuildProjectExtensionsPath=${projectExtensionsPath}`,
  `-p:BaseIntermediateOutputPath=${intermediateOutputPath}`,
  `-p:BaseOutputPath=${outputPath}`,
];

let browser;
let server;
let activeProcess;
let stopping = false;
let signalExitCode;

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.once(signal, () => {
    if (signalExitCode !== undefined) {
      return;
    }

    signalExitCode = signal === "SIGINT" ? 130 : 143;
    void cleanup().finally(() => process.exit(signalExitCode));
  });
}

try {
  if (!skipBuild) {
    await runProcess(
      dotnetPath,
      ["build", projectPath, "-c", "Debug", ...standaloneProperties],
      projectDirectory,
      true,
    );
  }

  const listening = createDeferred();
  server = spawn(
    dotnetPath,
    [
      "run",
      "--no-build",
      "-c",
      "Debug",
      "--project",
      projectPath,
      ...standaloneProperties,
      "--",
      "--urls",
      "http://127.0.0.1:0",
    ],
    {
      cwd: projectDirectory,
      env: {
        ...process.env,
        DOTNET_CLI_TELEMETRY_OPTOUT: "1",
      },
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
      detached: process.platform !== "win32",
    },
  );

  observeServerOutput(server.stdout, process.stdout, listening);
  observeServerOutput(server.stderr, process.stderr, listening);
  server.once("error", listening.reject);
  server.once("exit", (code, signal) => {
    listening.reject(
      new Error(
        `The Blazor Gateway exited before reporting its URL (code=${code}, signal=${signal}).`,
      ),
    );
  });

  const appUrl = await withTimeout(
    listening.promise,
    60_000,
    "Timed out waiting for the Blazor Gateway to start.",
  );

  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ locale: "en-US" });
  const page = await context.newPage();
  const browserDiagnostics = [];
  let sawTestStart = false;
  let sawPassingSummary = false;

  page.on("console", (message) => {
    const line = `[browser:${message.type()}] ${message.text()}`;
    browserDiagnostics.push(line);
    sawTestStart ||= line.includes("running RunsInsideBrowserWasm");
    sawPassingSummary ||= line.includes("Test run summary: Passed!");
    console.log(line);
  });
  page.on("pageerror", (error) => {
    const line = `[browser:pageerror] ${error.stack ?? error.message}`;
    browserDiagnostics.push(line);
    console.error(line);
  });

  console.log(`Opening ${appUrl}`);
  await page.goto(appUrl, {
    waitUntil: "domcontentloaded",
    timeout: 60_000,
  });

  try {
    await page.waitForFunction(
      () => {
        const status = document.querySelector('[role="status"]')?.textContent;
        return status === "Passed" || status?.startsWith("Failed");
      },
      undefined,
      { timeout: 60_000 },
    );
  } catch (error) {
    const currentStatus = await page
      .locator('[role="status"]')
      .textContent()
      .catch(() => "<unavailable>");
    throw new Error(
      [
        `Timed out waiting for the browser test to pass. Current status: ${currentStatus}`,
        ...browserDiagnostics,
      ].join("\n"),
      { cause: error },
    );
  }

  const status = await page.locator('[role="status"]').textContent();
  if (status !== "Passed") {
    throw new Error(
      [`The browser test failed: ${status}`, ...browserDiagnostics].join("\n"),
    );
  }

  const title = await page.title();
  if (!sawTestStart || !sawPassingSummary) {
    throw new Error(
      [
        "The page reported success without the expected MSTest execution diagnostics.",
        ...browserDiagnostics,
      ].join("\n"),
    );
  }

  if (browserDiagnostics.some((line) => line.includes("[browser:pageerror]"))) {
    throw new Error(
      ["The page reported a JavaScript error.", ...browserDiagnostics].join("\n"),
    );
  }

  console.log(`Browser test status: ${status}`);
  console.log(`Page title: ${title}`);
  console.log("PoC succeeded: MSTest executed inside browser-wasm.");
} finally {
  await cleanup();
}

function observeServerOutput(stream, destination, listening) {
  let pending = "";
  stream.setEncoding("utf8");
  stream.on("data", (chunk) => {
    destination.write(chunk);
    pending += chunk;

    for (;;) {
      const newline = pending.indexOf("\n");
      if (newline < 0) {
        break;
      }

      const line = pending.slice(0, newline).trim();
      pending = pending.slice(newline + 1);
      const match = /Now listening on:\s+(https?:\/\/\S+)/.exec(line);
      if (match) {
        listening.resolve(`${match[1]}/`);
      }
    }
  });
}

async function runProcess(command, args, cwd, trackForCleanup = false) {
  const child = spawn(command, args, {
    cwd,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: "1",
    },
    stdio: "inherit",
    windowsHide: true,
    detached: trackForCleanup && process.platform !== "win32",
  });
  if (trackForCleanup) {
    activeProcess = child;
  }

  try {
    const exit = await new Promise((resolve, reject) => {
      child.once("error", reject);
      child.once("exit", (code, signal) => resolve({ code, signal }));
    });

    if (exit.code !== 0) {
      throw new Error(
        `${command} ${args.join(" ")} failed (code=${exit.code}, signal=${exit.signal}).`,
      );
    }
  } finally {
    if (activeProcess === child) {
      activeProcess = undefined;
    }
  }
}

async function stopProcess(child) {
  if (!child || child.exitCode !== null || child.signalCode !== null) {
    return;
  }

  if (process.platform === "win32") {
    await stopWindowsProcessTree(child.pid);
    return;
  }

  process.kill(-child.pid, "SIGTERM");
  try {
    await withTimeout(
      new Promise((resolve) => child.once("exit", resolve)),
      5_000,
      "Timed out waiting for the Gateway to exit.",
    );
  } catch {
    process.kill(-child.pid, "SIGKILL");
  }
}

async function withTimeout(promise, milliseconds, message) {
  let timer;
  try {
    return await Promise.race([
      promise,
      new Promise((_, reject) => {
        timer = setTimeout(() => reject(new Error(message)), milliseconds);
      }),
    ]);
  } finally {
    clearTimeout(timer);
  }
}

function createDeferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

async function cleanup() {
  if (stopping) {
    return;
  }

  stopping = true;
  try {
    await browser?.close();
  } catch (error) {
    console.error(`Failed to close Chromium: ${error}`);
  }

  try {
    await stopProcess(activeProcess);
  } catch (error) {
    console.error(`Failed to stop the active dotnet command: ${error}`);
  }

  try {
    await stopProcess(server);
  } catch (error) {
    console.error(`Failed to stop the Blazor Gateway: ${error}`);
  }
}

function resolveDotnetPath(root) {
  const executable = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [
    path.join(root, "artifacts", "bin", "redist", "Debug", "dotnet", executable),
    path.join(root, ".dotnet", executable),
  ];
  return candidates.find(fs.existsSync) ?? "dotnet";
}

function ensureTrailingSeparator(value) {
  return value.endsWith(path.sep) ? value : `${value}${path.sep}`;
}

async function stopWindowsProcessTree(pid) {
  const escapedScript = `
    $rootPid = ${pid}
    $all = Get-CimInstance Win32_Process
    $ids = New-Object System.Collections.Generic.List[int]
    function Add-Children([int]$parent) {
      foreach ($child in $all | Where-Object ParentProcessId -eq $parent) {
        Add-Children $child.ProcessId
        $ids.Add([int]$child.ProcessId)
      }
    }
    Add-Children $rootPid
    $ids.Add($rootPid)
    foreach ($id in $ids) {
      Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
  `;

  await runProcess(
    "powershell",
    ["-NoProfile", "-NonInteractive", "-Command", escapedScript],
    pocDirectory,
  );
}

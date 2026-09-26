import assert from "node:assert/strict";
import {createRequire} from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {resolveAzureBuildId} = require("../github/check-suite.js");

test("resolves the build ID from an Azure root check", () =>
{
    const checks = [
        {name: "dotnet-sdk-public-ci (Build Linux x64)", details_url: "https://example.test?buildId=1"},
        {
            name: "dotnet-sdk-public-ci",
            details_url: "https://dev.azure.com/dnceng-public/public/_build/results?buildId=1552578"
        }
    ];

    assert.equal(resolveAzureBuildId(checks), "1552578");
});

test("returns null when the Azure root check is absent", () =>
{
    assert.equal(resolveAzureBuildId([{name: "Build Linux x64"}]), null);
});

test("rejects ambiguous or malformed Azure root checks", () =>
{
    assert.throws(() => resolveAzureBuildId([
        {name: "dotnet-sdk-public-ci", details_url: "https://example.test?buildId=1"},
        {name: "dotnet-sdk-public-ci", details_url: "https://example.test?buildId=2"}
    ]), /at most one/);
    assert.throws(() => resolveAzureBuildId([
        {name: "dotnet-sdk-public-ci", details_url: "https://example.test?buildId=not-a-number"}
    ]), /numeric buildId/);
});

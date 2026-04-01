#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..");
const issues = [];

function readFile(relativePath) {
  return fs.readFileSync(path.join(repoRoot, relativePath), "utf8");
}

function parseJsonFile(relativePath) {
  const raw = readFile(relativePath).replace(/^\uFEFF/, "");
  return JSON.parse(raw);
}

function requireMatch(content, regex, label) {
  const match = content.match(regex);
  if (!match) {
    throw new Error(`Could not parse ${label}.`);
  }

  return match;
}

function parseEnumMembers(content, enumName) {
  const match = requireMatch(
    content,
    new RegExp(`public enum ${enumName}\\s*\\{([\\s\\S]*?)\\n\\}`, "m"),
    `${enumName} enum`,
  );

  return [...match[1].matchAll(/\b([A-Za-z][A-Za-z0-9_]*)\s*=\s*\d+/g)]
    .map((item) => item[1])
    .filter((item) => item !== "Unknown");
}

function parseObjectStringArrayProperty(content, propertyName) {
  const match = requireMatch(
    content,
    new RegExp(`${propertyName}:\\s*\\[([\\s\\S]*?)\\]`, "m"),
    `${propertyName} array`,
  );

  return [...match[1].matchAll(/"([^"]*)"/g)]
    .map((item) => item[1])
    .filter((item) => item.length > 0);
}

function parseSortValues(constantsContent) {
  const match = requireMatch(constantsContent, /sort:\s*\[([\s\S]*?)\]/m, "sort array");
  return [...match[1].matchAll(/value:\s*"([^"]+)"/g)].map((item) => item[1]);
}

function parseDefaultSort(constantsContent) {
  return requireMatch(constantsContent, /DEFAULT_SORT = "([^"]+)"/, "DEFAULT_SORT")[1];
}

function parseTextQueryKeys(apiProxyContent) {
  const match = requireMatch(apiProxyContent, /const TEXT_QUERY_KEYS = \[([\s\S]*?)\] as const;/m, "TEXT_QUERY_KEYS");
  return [...match[1].matchAll(/"([^"]+)"/g)].map((item) => item[1]);
}

function parseBackendQueryParams(programContent) {
  const match = requireMatch(
    programContent,
    /app\.MapGet\("\/api\/jobs",\s*async\s*\(([\s\S]*?)\)\s*=>/m,
    "Jobs.Api /api/jobs signature",
  );

  return [...match[1].matchAll(/\b(?:string\?|DateTime\?|int)\s+([A-Za-z_][A-Za-z0-9_]*)\s*,/g)].map((item) => item[1]);
}

function parseBackendSortValues(programContent) {
  const match = requireMatch(
    programContent,
    /return sort\.Trim\(\)\.ToLowerInvariant\(\) switch\s*\{([\s\S]*?)\n\s*\};/m,
    "ResolveSort switch",
  );

  return [...new Set([...match[1].matchAll(/"([^"]+:[^"]+)"/g)].map((item) => item[1]))];
}

function extractApiPort(launchSettings) {
  const httpUrl = launchSettings?.profiles?.http?.applicationUrl;
  const match = typeof httpUrl === "string" ? httpUrl.match(/http:\/\/localhost:(\d+)/) : null;
  if (!match) {
    throw new Error("Could not parse API port from launchSettings.");
  }

  return match[1];
}

function assertSubset(label, expectedSubset, actualSet) {
  const missing = expectedSubset.filter((item) => !actualSet.has(item));
  if (missing.length > 0) {
    issues.push(`${label}: missing ${missing.join(", ")}`);
  }
}

function assertExactSet(label, actualItems, expectedItems) {
  const actual = new Set(actualItems);
  const expected = new Set(expectedItems);

  const missing = [...expected].filter((item) => !actual.has(item));
  const extra = [...actual].filter((item) => !expected.has(item));

  if (missing.length > 0 || extra.length > 0) {
    issues.push(
      `${label}: expected [${[...expected].join(", ")}], got [${[...actual].join(", ")}]`,
    );
  }
}

function assertIncludes(label, content, expectedFragment) {
  if (!content.includes(expectedFragment)) {
    issues.push(`${label}: expected to find "${expectedFragment}"`);
  }
}

const enumsContent = readFile("src/backend/Jobs.Domain/Models/Enums.cs");
const constantsContent = readFile("src/frontend/lib/constants.ts");
const apiProxyContent = readFile("src/frontend/lib/api-proxy.ts");
const programContent = readFile("src/backend/Jobs.Api/Program.cs");
const readmeContent = readFile("README.md");
const contributingContent = readFile("CONTRIBUTING.md");
const ciContent = readFile(".github/workflows/ci.yml");
const frontendEnvExample = readFile("src/frontend/.env.local.example");
const launchSettings = parseJsonFile("src/backend/Jobs.Api/Properties/launchSettings.json");

const backendWorkModes = new Set(parseEnumMembers(enumsContent, "WorkMode"));
const backendSeniorities = new Set(parseEnumMembers(enumsContent, "Seniority"));
const backendEmploymentTypes = new Set(parseEnumMembers(enumsContent, "EmploymentType"));
const backendSortValues = new Set(parseBackendSortValues(programContent));
const backendQueryParams = parseBackendQueryParams(programContent);

const frontendWorkModes = parseObjectStringArrayProperty(constantsContent, "workMode");
const frontendSeniorities = parseObjectStringArrayProperty(constantsContent, "seniority");
const frontendEmploymentTypes = parseObjectStringArrayProperty(constantsContent, "employmentType");
const frontendSortValues = parseSortValues(constantsContent);
const defaultSort = parseDefaultSort(constantsContent);
const frontendQueryKeys = parseTextQueryKeys(apiProxyContent);

const apiPort = extractApiPort(launchSettings);
const expectedBackendUrl = `http://localhost:${apiPort}`;

assertSubset("Frontend work modes must be backed by backend enums", frontendWorkModes, backendWorkModes);
assertSubset("Frontend seniority filters must be backed by backend enums", frontendSeniorities, backendSeniorities);
assertSubset("Frontend employmentType filters must be backed by backend enums", frontendEmploymentTypes, backendEmploymentTypes);
assertSubset("Frontend sort values must be recognized by backend ResolveSort", frontendSortValues, backendSortValues);

if (!frontendSortValues.includes(defaultSort)) {
  issues.push(`DEFAULT_SORT must be present in frontend sort options: ${defaultSort}`);
}

assertExactSet(
  "BFF query keys must match backend /api/jobs query params",
  [...frontendQueryKeys, "page", "pageSize"],
  backendQueryParams,
);

assertIncludes("README backend URL example", readmeContent, `BACKEND_URL=${expectedBackendUrl}`);
assertIncludes("CONTRIBUTING backend URL example", contributingContent, `BACKEND_URL=${expectedBackendUrl}`);
assertIncludes("Frontend .env.local example", frontendEnvExample, `BACKEND_URL=${expectedBackendUrl}`);
assertIncludes("CI frontend build BACKEND_URL", ciContent, `BACKEND_URL: ${expectedBackendUrl}`);
assertIncludes("README API URL", readmeContent, expectedBackendUrl);
assertIncludes("CONTRIBUTING API URL", contributingContent, expectedBackendUrl);

if (issues.length > 0) {
  console.error("Boundary drift checks failed:");
  issues.forEach((issue) => {
    console.error(`- ${issue}`);
  });
  process.exit(1);
}

console.log("Boundary drift checks passed.");
console.log(`- API port: ${apiPort}`);
console.log(`- Sort values: ${frontendSortValues.join(", ")}`);
console.log(`- Query params: ${backendQueryParams.join(", ")}`);

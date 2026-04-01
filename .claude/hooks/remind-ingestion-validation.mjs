#!/usr/bin/env node

import path from "node:path";

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();

function readStdin() {
  return new Promise((resolve) => {
    let data = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => {
      data += chunk;
    });
    process.stdin.on("end", () => resolve(data));
  });
}

function toRelative(filePath) {
  if (typeof filePath !== "string" || filePath.length === 0) return null;

  const absolute = path.isAbsolute(filePath) ? filePath : path.join(projectDir, filePath);
  return path.relative(projectDir, absolute).replace(/\\/g, "/");
}

function collectEditedPaths(payload) {
  const paths = new Set();
  const add = (value) => {
    const relative = toRelative(value);
    if (relative) {
      paths.add(relative);
    }
  };

  add(payload?.tool_input?.file_path);
  add(payload?.tool_input?.path);
  add(payload?.tool_response?.filePath);

  return [...paths];
}

function isIngestionFile(filePath) {
  return (
    filePath.startsWith("src/backend/Jobs.Infrastructure/Ingestion/") ||
    filePath.startsWith("src/backend/Jobs.Worker/") ||
    filePath.startsWith("src/backend/Jobs.Tests/Ingestion/") ||
    filePath.startsWith("src/backend/tests/fixtures/") ||
    filePath === "docs/04_ingestion_sources.md"
  );
}

const rawInput = await readStdin();
if (!rawInput.trim()) {
  process.exit(0);
}

let payload;
try {
  payload = JSON.parse(rawInput);
} catch {
  process.exit(0);
}

const editedFiles = collectEditedPaths(payload).filter(isIngestionFile);
if (editedFiles.length === 0) {
  process.exit(0);
}

const summary = editedFiles.slice(0, 4).join(", ");

console.log(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext:
        `Ingestion reminder: you edited ${summary}. ` +
        "Check whether the source should reuse an existing family, keep fixtures and parser tests aligned, and make sure worker-facing config changes stay synchronized between `Jobs.Api` and `Jobs.Worker`. " +
        "Before finishing, run the relevant xUnit coverage and, when practical, validate with `dotnet run --project src/backend/Jobs.Worker -- --run-once --source=<Name>`.",
    },
  }),
);

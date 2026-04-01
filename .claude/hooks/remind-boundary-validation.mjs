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

function isBoundaryFile(filePath) {
  return (
    filePath === "src/backend/Jobs.Api/Program.cs" ||
    filePath.startsWith("src/frontend/app/api/") ||
    filePath === "src/frontend/lib/api-proxy.ts" ||
    filePath === "src/frontend/lib/normalizers.ts" ||
    filePath === "src/frontend/lib/types.ts" ||
    filePath === "src/frontend/lib/constants.ts" ||
    filePath === "src/frontend/lib/utils.ts" ||
    filePath === "docs/07_api_contracts.md"
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

const editedFiles = collectEditedPaths(payload).filter(isBoundaryFile);
if (editedFiles.length === 0) {
  process.exit(0);
}

const summary = editedFiles.slice(0, 4).join(", ");

console.log(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext:
        `Boundary reminder: you edited ${summary}. ` +
        "Review `Jobs.Api/Program.cs`, the Next.js route handlers, `api-proxy.ts`, `normalizers.ts`, `types.ts`, and `constants.ts` together. " +
        "Before finishing, run `node scripts/check-boundary-drift.mjs` and, when frontend code is involved, `cd src/frontend && npm run lint && npm run build`.",
    },
  }),
);

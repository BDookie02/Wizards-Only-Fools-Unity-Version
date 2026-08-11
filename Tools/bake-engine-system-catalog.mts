import { createHash } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const unityRoot = "D:\\CodexProjects\\Wizards-Only-Fools-Unity";
const sourcePath = path.join(reactRoot, "src", "game", "systems", "systemCatalog.ts");
const outputPath = path.join(
  unityRoot,
  "Assets",
  "WOF",
  "Resources",
  "WOF",
  "EngineSystemCatalog.json",
);

const sourceBytes = await readFile(sourcePath);
const sourceSha256 = createHash("sha256").update(sourceBytes).digest("hex");
const sourceModule = await import(pathToFileURL(sourcePath).href);
const systems = sourceModule.GAME_SYSTEM_CATALOG;

if (!Array.isArray(systems) || systems.length !== 18) {
  throw new Error(`Expected the React oracle to expose 18 game systems; received ${systems?.length ?? "none"}.`);
}

for (const [index, system] of systems.entries()) {
  for (const field of ["id", "name", "category", "owner", "responsibility", "extractionTarget"] as const) {
    if (typeof system?.[field] !== "string" || !system[field].trim()) {
      throw new Error(`System ${index} has an invalid ${field}.`);
    }
  }
  if (!Array.isArray(system.currentEntrypoints) || system.currentEntrypoints.length === 0) {
    throw new Error(`System ${system.id} has no current entrypoints.`);
  }
}

const document = `${JSON.stringify({
  version: 1,
  sourceModule: "src/game/systems/systemCatalog.ts",
  sourceSha256,
  systems,
}, null, 2)}\n`;

let previous = "";
try {
  previous = await readFile(outputPath, "utf8");
} catch {
  // The first bake creates the versioned Unity resource.
}

const changed = previous !== document;
if (changed) {
  await mkdir(path.dirname(outputPath), { recursive: true });
  const temporaryPath = `${outputPath}.tmp`;
  await writeFile(temporaryPath, document, "utf8");
  await rm(outputPath, { force: true });
  await rename(temporaryPath, outputPath);
}

process.stdout.write(`${JSON.stringify({
  status: "complete",
  outputPath,
  changed,
  systemCount: systems.length,
  sourceSha256,
}, null, 2)}\n`);

import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const unityRoot = "D:\\CodexProjects\\Wizards-Only-Fools-Unity";
const outputRoot = path.join(unityRoot, "Assets", "WOF", "Art", "Generated", "React", "SurvivalTerrain");
const outputPath = path.join(outputRoot, "base-region.json");
const sourceRoot = path.join(reactRoot, "src", "game", "systems", "world");
const terrainSurfacePath = path.join(sourceRoot, "survival", "survivalTerrainSurface.ts");
const villageRegistryPath = path.join(sourceRoot, "villages", "survivalVillageRegistry.ts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");
const sourcePaths = [
  terrainSurfacePath,
  villageRegistryPath,
  storePath,
  path.join(sourceRoot, "survival", "survivalBiome.ts"),
  path.join(sourceRoot, "survival", "survivalRivers.ts"),
  path.join(sourceRoot, "survival", "survivalRoutes.ts"),
  path.join(sourceRoot, "survival", "survivalMath.ts"),
  path.join(sourceRoot, "survival", "survivalWorldConfig.ts"),
  path.join(sourceRoot, "terrain", "survivalTerrainGeometry.ts"),
  path.join(sourceRoot, "villages", "survivalGraveyardVillageTerrain.ts"),
  path.join(sourceRoot, "villages", "survivalVillagePad.ts"),
];

const terrainSurface = await import(pathToFileURL(terrainSurfacePath).href);
const villageRegistry = await import(pathToFileURL(villageRegistryPath).href);
const gameStore = await import(pathToFileURL(storePath).href);
const blockSize = Number(gameStore.SURVIVAL_BLOCK_SIZE);
const radius = 3;
const segments = 32;
const positions: number[] = [];
const colors: number[] = [];
const uvs: number[] = [];
const indices: number[] = [];
const includedChunks: string[] = [];
const skippedChunks: string[] = [];
const reactRequire = createRequire(path.join(reactRoot, "package.json"));
const THREE = reactRequire("three") as typeof import("three");
const colorScratch = new THREE.Color();

for (let cz = -radius; cz <= radius; cz += 1) {
  for (let cx = -radius; cx <= radius; cx += 1) {
    const villageKind = villageRegistry.getSpecialSurvivalVillageKind(cx, cz);
    if ((cx === 0 && cz === 0) || villageKind !== null) {
      skippedChunks.push(`${cx}:${cz}${villageKind ? `:${villageKind}` : ":base-village"}`);
      continue;
    }

    const chunk = {
      key: `${cx}:${cz}`,
      cx,
      cz,
      x: cx * blockSize,
      z: cz * blockSize,
      distance: 0,
      biome: "plains",
      hasVillage: false,
      villageKind: null,
      hasRiver: false,
      riverVertical: false,
      lod: "near",
    };
    const firstVertex = positions.length / 3;
    const gridSize = segments + 1;
    const step = blockSize / segments;
    const half = blockSize / 2;
    for (let zIndex = 0; zIndex <= segments; zIndex += 1) {
      const localZ = -half + zIndex * step;
      for (let xIndex = 0; xIndex <= segments; xIndex += 1) {
        const localX = -half + xIndex * step;
        const worldX = chunk.x + localX;
        const worldZ = chunk.z + localZ;
        const y = terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ);
        terrainSurface.getSurvivalRenderedTerrainColorInto(worldX, worldZ, y, colorScratch);
        positions.push(worldX, y, worldZ);
        colors.push(colorScratch.r, colorScratch.g, colorScratch.b);
        uvs.push(worldX / (blockSize * 0.93), worldZ / (blockSize * 0.93));
      }
    }
    for (let zIndex = 0; zIndex < segments; zIndex += 1) {
      for (let xIndex = 0; xIndex < segments; xIndex += 1) {
        const a = firstVertex + zIndex * gridSize + xIndex;
        const b = a + 1;
        const c = a + gridSize;
        const d = c + 1;
        indices.push(a, c, b, b, c, d);
      }
    }
    includedChunks.push(chunk.key);
  }
}

const sourceHash = createHash("sha256");
for (const sourcePath of sourcePaths) {
  sourceHash.update(sourcePath.replace(reactRoot, "").replaceAll("\\", "/"));
  sourceHash.update(await readFile(sourcePath));
}

const document = {
  schemaVersion: 1,
  generator: "Tools/bake-survival-terrain-assets.mts",
  reactOracle: reactRoot,
  sourceSignature: sourceHash.digest("hex"),
  blockSize,
  radius,
  segments,
  includedChunks,
  skippedChunks,
  mesh: {
    vertexCount: positions.length / 3,
    indexCount: indices.length,
    positions,
    colors,
    uvs,
    indices,
  },
};
const text = `${JSON.stringify(document)}\n`;
await mkdir(outputRoot, { recursive: true });
let previous = "";
try { previous = await readFile(outputPath, "utf8"); } catch {}
if (previous !== text) {
  const temporaryPath = `${outputPath}.${process.pid}.tmp`;
  await writeFile(temporaryPath, text);
  await rm(outputPath, { force: true });
  await rename(temporaryPath, outputPath);
}
console.log(JSON.stringify({
  status: "complete",
  outputPath,
  changed: previous !== text,
  vertexCount: document.mesh.vertexCount,
  indexCount: document.mesh.indexCount,
  includedChunkCount: includedChunks.length,
  skippedChunks,
  sourceSignature: document.sourceSignature,
}, null, 2));

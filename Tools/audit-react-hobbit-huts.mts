import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const worldRoot = path.join(reactRoot, "src", "game", "systems", "world");
const sourcePath = path.join(worldRoot, "villages", "survivalHobbitHutRendering.tsx");
const scatterPath = path.join(worldRoot, "vegetation", "survivalScatterRendering.tsx");
const treeVisualsPath = path.join(worldRoot, "vegetation", "survivalTreeVisuals.ts");
const chunksPath = path.join(worldRoot, "survival", "survivalChunks.ts");
const grassSurfacePath = path.join(worldRoot, "survival", "survivalGrassSurface.ts");
const terrainSurfacePath = path.join(worldRoot, "survival", "survivalTerrainSurface.ts");
const biomePath = path.join(worldRoot, "survival", "survivalBiome.ts");
const mathPath = path.join(worldRoot, "survival", "survivalMath.ts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");

const chunks = await import(pathToFileURL(chunksPath).href);
const grassSurface = await import(pathToFileURL(grassSurfacePath).href);
const terrainSurface = await import(pathToFileURL(terrainSurfacePath).href);
const survivalBiome = await import(pathToFileURL(biomePath).href);
const survivalMath = await import(pathToFileURL(mathPath).href);
const gameStore = await import(pathToFileURL(storePath).href);

type HobbitHutRecord = {
  sourceIndex: number;
  localX: number;
  localZ: number;
  worldX: number;
  worldZ: number;
  y: number;
  yaw: number;
  scale: number;
  variant: number;
};

const sha256 = async (filePath: string) => createHash("sha256")
  .update(await readFile(filePath))
  .digest("hex");

function supportsRoofForest(biome: string) {
  return biome === "plains" || biome === "jungle" || biome === "mushroom";
}

function makeHobbitHut(chunkX: number, chunkZ: number): HobbitHutRecord[] {
  const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near");
  if (!supportsRoofForest(chunk.biome) || chunk.hasVillage) return [];
  const spawnRoll = survivalMath.survivalHash01(chunk.cx, chunk.cz, 7310);
  const shouldSpawn = chunk.biome === "jungle"
    ? spawnRoll > 0.68
    : chunk.biome === "mushroom"
      ? spawnRoll > 0.72
      : spawnRoll > 0.74;
  if (!shouldSpawn) return [];

  const generated: HobbitHutRecord[] = [];
  for (let index = 0; index < 10 && generated.length < 1; index += 1) {
    const localX = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 7360 + index) - 0.5) *
      gameStore.SURVIVAL_BLOCK_SIZE * 0.72;
    const localZ = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 7410 + index) - 0.5) *
      gameStore.SURVIVAL_BLOCK_SIZE * 0.72;
    if (Math.min(Math.abs(localX), Math.abs(localZ)) < 54) continue;

    const worldX = chunk.x + localX;
    const worldZ = chunk.z + localZ;
    const surface = grassSurface.getSurvivalDecorationSurfaceQuality(chunk, localX, localZ, 13.5, 7.2);
    if (surface.normal.y < 0.82 || surface.heightRange > 5.8) continue;
    if (surface.y < survivalBiome.getSurvivalWaterLevelAtWorld(worldX, worldZ) + 0.42) continue;

    const heights = [
      terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ - 7),
      terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ + 7),
      terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX + 7, localZ),
      terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX - 7, localZ),
    ];
    if (Math.max(...heights) - Math.min(...heights) > 7.5) continue;

    generated.push({
      sourceIndex: index,
      localX,
      localZ,
      worldX,
      worldZ,
      y: surface.y,
      yaw: survivalMath.survivalHash01(chunk.cx, chunk.cz, 7460 + index) * Math.PI * 2,
      scale: 1.12 + survivalMath.survivalHash01(chunk.cx, chunk.cz, 7510 + index) * 0.38,
      variant: survivalMath.survivalHash01(chunk.cx, chunk.cz, 7560 + index),
    });
  }
  return generated;
}

const requested = process.argv.slice(2);
let coordinates: [number, number][];
if (requested.length > 0) {
  coordinates = requested.map((value) => value.split(":").map(Number) as [number, number]);
} else {
  const examples = new Map<string, [number, number][]>();
  for (const biome of ["plains", "jungle", "mushroom"]) examples.set(biome, []);
  for (let chunkZ = -12; chunkZ <= 12; chunkZ += 1) {
    for (let chunkX = -12; chunkX <= 12; chunkX += 1) {
      const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near");
      const selected = examples.get(chunk.biome);
      if (!selected || selected.length >= 2 || makeHobbitHut(chunkX, chunkZ).length === 0) continue;
      selected.push([chunkX, chunkZ]);
    }
  }
  coordinates = [...examples.values()].flat();
}

const output = {
  sourceHashes: {
    survivalHobbitHutRendering: await sha256(sourcePath),
    survivalScatterRendering: await sha256(scatterPath),
    survivalTreeVisuals: await sha256(treeVisualsPath),
  },
  chunks: coordinates.map(([chunkX, chunkZ]) => {
    const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near");
    return {
      chunkX,
      chunkZ,
      biome: chunk.biome,
      hasVillage: chunk.hasVillage,
      records: makeHobbitHut(chunkX, chunkZ),
    };
  }),
};

process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);

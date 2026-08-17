import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const worldRoot = path.join(reactRoot, "src", "game", "systems", "world");
const sourcePath = path.join(worldRoot, "survival", "survivalDesertLandmarkRendering.tsx");
const scatterPath = path.join(worldRoot, "vegetation", "survivalScatterRendering.tsx");
const loadStagePath = path.join(worldRoot, "survival", "survivalLoadStage.ts");
const chunksPath = path.join(worldRoot, "survival", "survivalChunks.ts");
const terrainSurfacePath = path.join(worldRoot, "survival", "survivalTerrainSurface.ts");
const biomePath = path.join(worldRoot, "survival", "survivalBiome.ts");
const mathPath = path.join(worldRoot, "survival", "survivalMath.ts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");

const chunks = await import(pathToFileURL(chunksPath).href);
const terrainSurface = await import(pathToFileURL(terrainSurfacePath).href);
const survivalBiome = await import(pathToFileURL(biomePath).href);
const survivalMath = await import(pathToFileURL(mathPath).href);
const gameStore = await import(pathToFileURL(storePath).href);

type LandmarkKind = "pyramid" | "obelisk";
type Landmark = {
  sourceIndex: number;
  localX: number;
  localZ: number;
  worldX: number;
  worldZ: number;
  y: number;
  scale: number;
  yaw: number;
  variant: number;
  type: LandmarkKind;
  stepCount: number;
  baseSize: number;
  height: number;
  villagerCount: number;
};

const samples = [-1, -0.5, 0, 0.5, 1] as const;
const sha256 = async (filePath: string) => createHash("sha256")
  .update(await readFile(filePath))
  .digest("hex");

function metrics(scale: number, variant: number) {
  const stepCount = variant > 0.72 ? 7 : 6;
  const stepHeight = 2.1 * scale;
  const baseSize = 31 * scale;
  return {
    stepCount,
    stepHeight,
    baseSize,
    height: stepHeight * stepCount + 4.2 * scale,
  };
}

function footprintRange(chunk: any, localX: number, localZ: number, halfSize: number, yaw: number) {
  const cos = Math.cos(yaw);
  const sin = Math.sin(yaw);
  let min = Infinity;
  let max = -Infinity;
  let sum = 0;
  let count = 0;
  for (const sampleX of samples) {
    for (const sampleZ of samples) {
      const offsetX = sampleX * halfSize;
      const offsetZ = sampleZ * halfSize;
      const rotatedX = offsetX * cos - offsetZ * sin;
      const rotatedZ = offsetX * sin + offsetZ * cos;
      const height = terrainSurface.getSurvivalTerrainHeightForChunk(
        chunk,
        localX + rotatedX,
        localZ + rotatedZ,
      );
      min = Math.min(min, height);
      max = Math.max(max, height);
      sum += height;
      count += 1;
    }
  }
  return { min, max, average: sum / count, range: max - min };
}

function makeLandmarks(chunkX: number, chunkZ: number, distance: number): Landmark[] {
  const lod = distance === 0 ? "near" : distance <= 1 ? "mid" : "far";
  const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, distance, lod);
  if (chunk.biome !== "desert" || chunk.lod === "far" || chunk.hasVillage) return [];
  const targetCount = chunk.lod === "near" ? 2 : 1;
  const generated: Landmark[] = [];
  for (let index = 0; index < targetCount * 14 && generated.length < targetCount; index += 1) {
    const localX = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 510 + index) - 0.5) *
      gameStore.SURVIVAL_BLOCK_SIZE * 0.76;
    const localZ = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 540 + index) - 0.5) *
      gameStore.SURVIVAL_BLOCK_SIZE * 0.76;
    if (Math.min(Math.abs(localX), Math.abs(localZ)) < 62) continue;
    const worldX = chunk.x + localX;
    const worldZ = chunk.z + localZ;
    const terrainY = terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ);
    if (terrainY < survivalBiome.getSurvivalWaterLevelAtWorld(worldX, worldZ) + 0.5) continue;
    const variant = survivalMath.survivalHash01(chunk.cx, chunk.cz, 570 + index);
    const scale = 0.86 + survivalMath.survivalHash01(chunk.cx, chunk.cz, 600 + index) * 0.72;
    const yaw = survivalMath.survivalHash01(chunk.cx, chunk.cz, 630 + index) * Math.PI * 2;
    const type: LandmarkKind = variant > 0.32 ? "pyramid" : "obelisk";
    const pyramid = metrics(scale, variant);
    let y = terrainY;
    if (type === "pyramid") {
      const footprint = footprintRange(
        chunk,
        localX,
        localZ,
        pyramid.baseSize * 0.58,
        yaw + Math.PI * 0.25,
      );
      if (footprint.range > Math.max(4.75, pyramid.baseSize * 0.12)) continue;
      y = Math.max(terrainY, footprint.max + 0.08);
    }
    generated.push({
      sourceIndex: index,
      localX,
      localZ,
      worldX,
      worldZ,
      y,
      scale,
      yaw,
      variant,
      type,
      stepCount: pyramid.stepCount,
      baseSize: pyramid.baseSize,
      height: type === "pyramid" ? pyramid.height : 28 * scale + 7.95 * scale,
      villagerCount: type === "pyramid" ? (pyramid.baseSize > 43 ? 3 : 2) : 0,
    });
  }
  return generated;
}

const requested = process.argv.slice(2);
let coordinates: [number, number][] = requested.map((value) => value.split(":").map(Number) as [number, number]);
if (coordinates.length === 0) {
  const selected: [number, number][] = [];
  let hasPyramid = false;
  let hasObelisk = false;
  for (let chunkZ = -12; chunkZ <= 12 && selected.length < 4; chunkZ += 1) {
    for (let chunkX = -12; chunkX <= 12 && selected.length < 4; chunkX += 1) {
      const records = makeLandmarks(chunkX, chunkZ, 0);
      if (records.length === 0) continue;
      const addsPyramid = !hasPyramid && records.some((record) => record.type === "pyramid");
      const addsObelisk = !hasObelisk && records.some((record) => record.type === "obelisk");
      if (!addsPyramid && !addsObelisk && selected.length >= 2) continue;
      selected.push([chunkX, chunkZ]);
      hasPyramid ||= records.some((record) => record.type === "pyramid");
      hasObelisk ||= records.some((record) => record.type === "obelisk");
    }
  }
  coordinates = selected;
}

const output = {
  sourceHashes: {
    survivalDesertLandmarkRendering: await sha256(sourcePath),
    survivalScatterRendering: await sha256(scatterPath),
    survivalLoadStage: await sha256(loadStagePath),
  },
  chunks: coordinates.map(([chunkX, chunkZ]) => ({
    chunkX,
    chunkZ,
    biome: chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near").biome,
    hasVillage: chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near").hasVillage,
    near: makeLandmarks(chunkX, chunkZ, 0),
    mid: makeLandmarks(chunkX, chunkZ, 1),
  })),
};

process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);

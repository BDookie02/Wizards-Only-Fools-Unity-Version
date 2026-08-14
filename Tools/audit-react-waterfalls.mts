import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const survivalRoot = path.join(reactRoot, "src", "game", "systems", "world", "survival");
const rulesPath = path.join(survivalRoot, "survivalWaterfalls.ts");
const renderingPath = path.join(survivalRoot, "survivalWaterfallRendering.tsx");
const featuresPath = path.join(survivalRoot, "survivalWaterFeatureRendering.tsx");
const chunksPath = path.join(survivalRoot, "survivalChunks.ts");
const terrainPath = path.join(survivalRoot, "survivalTerrainSurface.ts");
const biomePath = path.join(survivalRoot, "survivalBiome.ts");

const waterfalls = await import(pathToFileURL(rulesPath).href);
const chunks = await import(pathToFileURL(chunksPath).href);
const terrain = await import(pathToFileURL(terrainPath).href);
const biome = await import(pathToFileURL(biomePath).href);

const sha256 = async (filePath: string) => createHash("sha256")
  .update(await readFile(filePath))
  .digest("hex");

const resolvers = {
  getTerrainHeightForChunk: terrain.getSurvivalTerrainHeightForChunk,
  getWaterLevelAtWorld: biome.getSurvivalWaterLevelAtWorld,
  isRestoredMeadowWaterSuppressed: biome.isSurvivalRestoredMeadowWaterSuppressed,
};

function make(chunkX: number, chunkZ: number, distance: number) {
  const lod = distance === 0 ? "near" : distance <= 1 ? "mid" : "far";
  const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, distance, lod);
  return {
    chunk: {
      cx: chunk.cx,
      cz: chunk.cz,
      biome: chunk.biome,
      hasVillage: chunk.hasVillage,
      lod: chunk.lod,
    },
    waterfalls: waterfalls.makeSurvivalWaterfalls(chunk, resolvers),
  };
}

const requested = process.argv.slice(2);
let coordinates = requested.map((value) => value.split(":").map(Number) as [number, number]);
if (coordinates.length === 0) {
  coordinates = [];
  const seenBiomes = new Set<string>();
  for (let chunkZ = -4; chunkZ <= 4 && coordinates.length < 8; chunkZ += 1) {
    for (let chunkX = -4; chunkX <= 4 && coordinates.length < 8; chunkX += 1) {
      const result = make(chunkX, chunkZ, 0);
      if (result.waterfalls.length === 0) continue;
      if (seenBiomes.has(result.chunk.biome) && coordinates.length >= 4) continue;
      coordinates.push([chunkX, chunkZ]);
      seenBiomes.add(result.chunk.biome);
    }
  }
}

const output = {
  sourceHashes: {
    survivalWaterfalls: await sha256(rulesPath),
    survivalWaterfallRendering: await sha256(renderingPath),
    survivalWaterFeatureRendering: await sha256(featuresPath),
  },
  samples: coordinates.map(([chunkX, chunkZ]) => ({
    near: make(chunkX, chunkZ, 0),
    mid: make(chunkX, chunkZ, 1),
    far: make(chunkX, chunkZ, 2),
  })),
};

process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);

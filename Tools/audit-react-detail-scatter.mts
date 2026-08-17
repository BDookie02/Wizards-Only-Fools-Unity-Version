import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const worldRoot = path.join(reactRoot, "src", "game", "systems", "world");
const scatterPath = path.join(worldRoot, "vegetation", "survivalScatterRendering.tsx");
const detailPath = path.join(worldRoot, "vegetation", "survivalDetailScatterRendering.tsx");
const chunksPath = path.join(worldRoot, "survival", "survivalChunks.ts");
const grassSurfacePath = path.join(worldRoot, "survival", "survivalGrassSurface.ts");
const biomePath = path.join(worldRoot, "survival", "survivalBiome.ts");
const mathPath = path.join(worldRoot, "survival", "survivalMath.ts");
const configPath = path.join(worldRoot, "survival", "survivalWorldConfig.ts");
const grassConfigPath = path.join(worldRoot, "vegetation", "survivalBotwGrassConfig.ts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");

const chunks = await import(pathToFileURL(chunksPath).href);
const grassSurface = await import(pathToFileURL(grassSurfacePath).href);
const survivalBiome = await import(pathToFileURL(biomePath).href);
const survivalMath = await import(pathToFileURL(mathPath).href);
const worldConfig = await import(pathToFileURL(configPath).href);
const grassConfig = await import(pathToFileURL(grassConfigPath).href);
const gameStore = await import(pathToFileURL(storePath).href);

type DetailRecord = {
  sourceIndex: number;
  localX: number;
  localZ: number;
  worldX: number;
  worldZ: number;
  y: number;
  scale: number;
  variant: number;
  kind: "tree" | "cactus" | "tumbleweed";
};

const sha256 = async (filePath: string) => createHash("sha256")
  .update(await readFile(filePath))
  .digest("hex");

function makeDetailScatter(chunkX: number, chunkZ: number): DetailRecord[] {
  const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near");
  const baseCount = chunk.biome === "desert"
    ? 9
    : chunk.biome === "jungle"
      ? 5
      : chunk.biome === "swamp"
        ? 4
        : chunk.biome === "mushroom"
          ? 3
          : 4;
  const count = Math.max(3, Math.round(baseCount));
  const generated: DetailRecord[] = [];
  const attempts = count * 8;

  for (let index = 0; index < attempts && generated.length < count; index += 1) {
    const localX = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 20 + index) - 0.5) *
      (gameStore.SURVIVAL_BLOCK_SIZE * 0.78);
    const localZ = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 60 + index) - 0.5) *
      (gameStore.SURVIVAL_BLOCK_SIZE * 0.78);
    if (chunk.hasVillage && Math.max(Math.abs(localX), Math.abs(localZ)) < worldConfig.BASE_VILLAGE_HALF_SIZE + 28) continue;
    if (chunk.biome !== "desert" && Math.min(Math.abs(localX), Math.abs(localZ)) < 34) continue;

    const worldX = chunk.x + localX;
    const worldZ = chunk.z + localZ;
    const surface = grassSurface.getSurvivalDecorationSurfaceQuality(chunk, localX, localZ, 8.8, 5.2);
    if (
      surface.normal.y < grassConfig.SURVIVAL_BOTW_DECORATION_MIN_NORMAL_Y ||
      surface.heightRange > grassConfig.SURVIVAL_BOTW_DECORATION_MAX_FOOTPRINT_RANGE
    ) continue;
    if (surface.y < survivalBiome.getSurvivalWaterLevelAtWorld(worldX, worldZ) + 0.18) continue;

    const scale = 1.35 + survivalMath.survivalHash01(chunk.cx, chunk.cz, 90 + index) * (
      chunk.biome === "jungle" ? 2.95 : chunk.biome === "swamp" ? 2.45 : 2.1
    );
    const variant = survivalMath.survivalHash01(chunk.cx, chunk.cz, 120 + index);
    if (chunk.biome !== "desert") {
      const minSpacing = chunk.biome === "jungle"
        ? 118
        : chunk.biome === "swamp"
          ? 96
          : chunk.biome === "mushroom"
            ? 82
            : 88;
      const minSpacingSq = minSpacing * minSpacing;
      if (generated.some((prop) => {
        const dx = prop.localX - localX;
        const dz = prop.localZ - localZ;
        return dx * dx + dz * dz < minSpacingSq;
      })) continue;
    }

    generated.push({
      sourceIndex: index,
      localX,
      localZ,
      worldX,
      worldZ,
      y: surface.y,
      scale,
      variant,
      kind: chunk.biome === "desert" ? (variant > 0.56 ? "tumbleweed" : "cactus") : "tree",
    });
  }
  return generated;
}

const requested = process.argv.slice(2);
const coordinates = requested.length > 0
  ? requested.map((value) => value.split(":").map(Number) as [number, number])
  : [[-1, -1], [-4, 0], [7, 4], [-2, 2], [4, -3]] as [number, number][];

const output = {
  sourceHashes: {
    survivalScatterRendering: await sha256(scatterPath),
    survivalDetailScatterRendering: await sha256(detailPath),
  },
  chunks: coordinates.map(([chunkX, chunkZ]) => {
    const chunk = chunks.makeSurvivalChunkInfoForCoords(chunkX, chunkZ, 0, "near");
    return {
      chunkX,
      chunkZ,
      biome: chunk.biome,
      hasVillage: chunk.hasVillage,
      records: makeDetailScatter(chunkX, chunkZ),
    };
  }),
};

process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);

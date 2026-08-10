import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import {
  getUnityMountainBandedColor,
  getUnityMountainPerimeterLift,
} from "./wof-unity-mountain-profile.mts";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const unityRoot = "D:\\CodexProjects\\Wizards-Only-Fools-Unity";
const outputRoot = path.join(unityRoot, "Assets", "WOF", "Art", "Generated", "React", "SurvivalTerrain");
const outputPath = path.join(outputRoot, "base-region.json");
const sourceRoot = path.join(reactRoot, "src", "game", "systems", "world");
const terrainSurfacePath = path.join(sourceRoot, "survival", "survivalTerrainSurface.ts");
const villageRegistryPath = path.join(sourceRoot, "villages", "survivalVillageRegistry.ts");
const biomePath = path.join(sourceRoot, "survival", "survivalBiome.ts");
const riversPath = path.join(sourceRoot, "survival", "survivalRivers.ts");
const mathPath = path.join(sourceRoot, "survival", "survivalMath.ts");
const chunksPath = path.join(sourceRoot, "survival", "survivalChunks.ts");
const positionPath = path.join(sourceRoot, "survival", "survivalPosition.ts");
const terrainGeometryPath = path.join(sourceRoot, "terrain", "survivalTerrainGeometry.ts");
const grassSurfacePath = path.join(sourceRoot, "survival", "survivalGrassSurface.ts");
const treeVisualsPath = path.join(sourceRoot, "vegetation", "survivalTreeVisuals.ts");
const foliagePalettesPath = path.join(sourceRoot, "vegetation", "survivalFoliagePalettes.ts");
const grassConfigPath = path.join(sourceRoot, "vegetation", "survivalBotwGrassConfig.ts");
const grassFootprintsPath = path.join(sourceRoot, "vegetation", "survivalBotwGrassFootprints.ts");
const grassResolversPath = path.join(sourceRoot, "vegetation", "survivalBotwGrassResolvers.ts");
const solidTreeGrovePath = path.join(sourceRoot, "vegetation", "survivalSolidTreeGroveRendering.tsx");
const waterFeaturesPath = path.join(sourceRoot, "survival", "survivalWaterFeatures.ts");
const unityMountainProfilePath = path.join(unityRoot, "Tools", "wof-unity-mountain-profile.mts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");
const sourcePaths = [
  terrainSurfacePath,
  villageRegistryPath,
  storePath,
  biomePath,
  riversPath,
  path.join(sourceRoot, "survival", "survivalRoutes.ts"),
  mathPath,
  chunksPath,
  positionPath,
  path.join(sourceRoot, "survival", "survivalWorldConfig.ts"),
  terrainGeometryPath,
  path.join(sourceRoot, "villages", "survivalGraveyardVillageTerrain.ts"),
  path.join(sourceRoot, "villages", "survivalVillagePad.ts"),
  grassSurfacePath,
  treeVisualsPath,
  foliagePalettesPath,
  grassConfigPath,
  grassFootprintsPath,
  grassResolversPath,
  solidTreeGrovePath,
  waterFeaturesPath,
  unityMountainProfilePath,
];

const terrainSurface = await import(pathToFileURL(terrainSurfacePath).href);
const villageRegistry = await import(pathToFileURL(villageRegistryPath).href);
const survivalBiome = await import(pathToFileURL(biomePath).href);
const survivalRivers = await import(pathToFileURL(riversPath).href);
const survivalMath = await import(pathToFileURL(mathPath).href);
const survivalChunks = await import(pathToFileURL(chunksPath).href);
const survivalPosition = await import(pathToFileURL(positionPath).href);
const survivalTerrainGeometry = await import(pathToFileURL(terrainGeometryPath).href);
const survivalGrassSurface = await import(pathToFileURL(grassSurfacePath).href);
const survivalTreeVisuals = await import(pathToFileURL(treeVisualsPath).href);
const survivalWaterFeatures = await import(pathToFileURL(waterFeaturesPath).href);
const gameStore = await import(pathToFileURL(storePath).href);
const blockSize = Number(gameStore.SURVIVAL_BLOCK_SIZE);
const minimumChunkX = -4;
const maximumChunkX = 6;
const minimumChunkZ = -4;
const maximumChunkZ = 3;
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
const survivalBiomes = ["plains", "jungle", "desert", "swamp", "mushroom", "tallgrass"] as const;
type SurvivalBiomeName = typeof survivalBiomes[number];
type SurvivalChunk = {
  key: string;
  cx: number;
  cz: number;
  x: number;
  z: number;
  distance: number;
  biome: SurvivalBiomeName;
  hasVillage: boolean;
  villageKind: null;
  hasRiver: boolean;
  riverVertical: boolean;
  lod: "near" | "mid" | "far";
};
type SurvivalFoliagePlacement = {
  meshIndex: number;
  biome: SurvivalBiomeName;
  x: number;
  y: number;
  z: number;
  pitch: number;
  yaw: number;
  roll: number;
  scaleX: number;
  scaleY: number;
  scaleZ: number;
};
type SurvivalStreamingTreeFixture = {
  cx: number;
  cz: number;
  distance: number;
  lod: "near" | "mid";
  trees: SurvivalFoliagePlacement[];
};
type SurvivalStreamingWaterFixture = {
  cx: number;
  cz: number;
  distance: number;
  lod: "near" | "mid";
  riverVertexCount: number;
  riverIndexCount: number;
  riverPositionSamples: Array<{ x: number; y: number; z: number }>;
  ponds: Array<{ localX: number; localZ: number; radiusX: number; radiusZ: number; y: number }>;
  lilies: Array<{ localX: number; localZ: number; scale: number }>;
};
type SurvivalStreamingSample = {
  localX: number;
  localZ: number;
  height: number;
  colorR: number;
  colorG: number;
  colorB: number;
};
type SurvivalStreamingChunkFixture = {
  cx: number;
  cz: number;
  biome: SurvivalBiomeName;
  hasRiver: boolean;
  riverVertical: boolean;
  samples: SurvivalStreamingSample[];
};
const makeSurvivalChunk = (cx: number, cz: number): SurvivalChunk => ({
  key: `${cx}:${cz}`,
  cx,
  cz,
  x: cx * blockSize,
  z: cz * blockSize,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(cx, cz) as SurvivalBiomeName,
  hasVillage: false,
  villageKind: null,
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(cx, cz),
  riverVertical: survivalMath.survivalHash01(cx, cz, 5) > 0.5,
  lod: "near",
});
const streamingFixtureCoords: ReadonlyArray<readonly [number, number]> = [
  [7, 0],
  [7, 4],
  [-5, -5],
  [12, -12],
  [-17, 9],
  [23, 19],
];
const streamingFixtureLocalSamples: ReadonlyArray<readonly [number, number]> = [
  [-256, -256],
  [-128.5, 64.25],
  [0, 0],
  [127.75, -193.5],
  [256, 256],
];
const streamingChunkFixtures: SurvivalStreamingChunkFixture[] = streamingFixtureCoords.map(([cx, cz]) => {
  const chunk = makeSurvivalChunk(cx, cz);
  const samples = streamingFixtureLocalSamples.map(([localX, localZ]) => {
    const worldX = chunk.x + localX;
    const worldZ = chunk.z + localZ;
    const height = terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ);
    terrainSurface.getSurvivalRenderedTerrainColorInto(worldX, worldZ, height, colorScratch);
    return {
      localX,
      localZ,
      height,
      colorR: colorScratch.r,
      colorG: colorScratch.g,
      colorB: colorScratch.b,
    };
  });
  return {
    cx,
    cz,
    biome: chunk.biome,
    hasRiver: chunk.hasRiver,
    riverVertical: chunk.riverVertical,
    samples,
  };
});
const streamingWindow = survivalChunks.makeSurvivalChunks(12, -12, true, 3).map((chunk: any) => ({
  dx: chunk.cx - 12,
  dz: chunk.cz + 12,
  distance: chunk.distance,
  lod: chunk.lod,
  renderSegments: survivalTerrainGeometry.getSurvivalTerrainRenderSegments(chunk),
  collisionSegments: chunk.distance <= 2
    ? survivalTerrainGeometry.getSurvivalTerrainCollisionSegments(chunk)
    : 0,
}));
const streamingChunkCoordinateFixtures = [-1536.01, -1536, -1280.01, -1280, -0.01, 0, 255.99, 256, 767.99, 768]
  .map(value => ({ value, chunk: survivalPosition.getSurvivalChunkCoord(value) }));
const serializeFoliageGeometry = (source: THREE.BufferGeometry) => {
  const position = source.getAttribute("position") as THREE.BufferAttribute;
  const normal = source.getAttribute("normal") as THREE.BufferAttribute | undefined;
  const uv = source.getAttribute("uv") as THREE.BufferAttribute | undefined;
  const color = source.getAttribute("color") as THREE.BufferAttribute | undefined;
  const positions: number[] = [];
  const normals: number[] = [];
  const uvs: number[] = [];
  const colors: number[] = [];
  for (let index = 0; index < position.count; index += 1) {
    positions.push(position.getX(index), position.getY(index), position.getZ(index));
    normals.push(normal?.getX(index) ?? 0, normal?.getY(index) ?? 1, normal?.getZ(index) ?? 0);
    uvs.push(uv?.getX(index) ?? 0, uv?.getY(index) ?? 0);
    colors.push(color?.getX(index) ?? 1, color?.getY(index) ?? 1, color?.getZ(index) ?? 1);
  }
  const sourceIndex = source.getIndex();
  const indices = sourceIndex
    ? Array.from(sourceIndex.array, value => Number(value))
    : Array.from({ length: position.count }, (_, index) => index);
  return {
    vertexCount: position.count,
    positions,
    normals,
    uvs,
    colors,
    indices,
  };
};

const foliageMeshes = survivalBiomes.flatMap((biome) =>
  Array.from({ length: 4 }, (_, variant) => ({
    biome,
    variant,
    mesh: serializeFoliageGeometry(survivalTreeVisuals.makeSurvivalSolidTreeGeometry(biome, variant)),
  })),
);
const foliagePlacements: SurvivalFoliagePlacement[] = [];
const foliageCounts: Record<string, number> = {};
const makeExactFoliageForChunk = (
  chunk: SurvivalChunk,
  dense: boolean,
  mobilePerformanceMode = false,
): SurvivalFoliagePlacement[] => {
  if (chunk.lod === "far") return [];
  const isMidDistanceLod = chunk.lod === "mid";
  const density = (isMidDistanceLod ? 0.055 : dense ? 0.92 : 0.62) *
    (mobilePerformanceMode ? 0.54 : 1);
  const baseCount = chunk.biome === "jungle"
    ? 44
    : chunk.biome === "swamp"
      ? 38
      : chunk.biome === "mushroom"
        ? 34
        : chunk.biome === "desert"
          ? 22
          : 36;
  const count = Math.max(isMidDistanceLod ? 1 : 8, Math.round(baseCount * density));
  const attempts = count * 12;
  const generated: Array<{ localX: number; localZ: number }> = [];
  const placements: SurvivalFoliagePlacement[] = [];
  const footprintRadius = isMidDistanceLod ? 11.5 : 8.5;
  const sampleDistance = isMidDistanceLod ? 7.2 : 5.2;
  const minNormalY = isMidDistanceLod ? 0.86 : 0.72;
  const maxHeightRange = isMidDistanceLod ? 3.2 : 7.4;
  const midDistanceTreeScale = isMidDistanceLod ? 0.58 : 1;
  for (let index = 0; index < attempts && generated.length < count; index += 1) {
    const localX = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 2310 + index) - 0.5) * blockSize * 0.94;
    const localZ = (survivalMath.survivalHash01(chunk.cx, chunk.cz, 2350 + index) - 0.5) * blockSize * 0.94;
    if (chunk.biome !== "desert" && Math.min(Math.abs(localX), Math.abs(localZ)) < 22) continue;
    const worldX = chunk.x + localX;
    const worldZ = chunk.z + localZ;
    const surface = survivalGrassSurface.getSurvivalDecorationSurfaceQuality(
      chunk,
      localX,
      localZ,
      footprintRadius,
      sampleDistance,
    );
    if (surface.normal.y < minNormalY || surface.heightRange > maxHeightRange) continue;
    const y = surface.y + getUnityMountainPerimeterLift(worldX, worldZ, blockSize);
    const waterY = survivalBiome.getSurvivalWaterLevelAtWorld(worldX, worldZ);
    if (y < waterY + 0.2) continue;

    const baseSpacing = chunk.biome === "jungle"
      ? 22
      : chunk.biome === "swamp"
        ? 20
        : chunk.biome === "desert"
          ? 28
          : 23;
    const spacing = isMidDistanceLod ? baseSpacing * 2.35 : baseSpacing;
    const spacingSquared = spacing * spacing;
    let tooClose = false;
    for (const tree of generated) {
      const dx = tree.localX - localX;
      const dz = tree.localZ - localZ;
      if (dx * dx + dz * dz < spacingSquared) {
        tooClose = true;
        break;
      }
    }
    if (tooClose) continue;

    const variant = survivalMath.survivalHash01(chunk.cx, chunk.cz, 2390 + index);
    const profile = survivalTreeVisuals.getFastGroveTreeProfile(chunk.biome, variant);
    const geometryVariant = Math.floor(
      survivalMath.survivalHash01(chunk.cx, chunk.cz, 2465 + index) * 4,
    ) % 4;
    const treeIndex = generated.length;
    const leanScale = isMidDistanceLod ? 0.36 : 1;
    const lean = (variant - 0.5) * (0.08 + geometryVariant * 0.018) * leanScale;
    const heightStretch = isMidDistanceLod
      ? 0.72 + survivalMath.survivalHash01(chunk.cx + treeIndex, chunk.cz - treeIndex, 8220) * 0.22
      : 0.82 + survivalMath.survivalHash01(chunk.cx + treeIndex, chunk.cz - treeIndex, 8220) * 0.46;
    const radiusStretch = isMidDistanceLod
      ? 0.82 + survivalMath.survivalHash01(chunk.cx - treeIndex, chunk.cz + treeIndex, 8230) * 0.2
      : 1.12 + survivalMath.survivalHash01(chunk.cx - treeIndex, chunk.cz + treeIndex, 8230) * 0.52;
    const depthStretch = isMidDistanceLod
      ? 0.74 + survivalMath.survivalHash01(chunk.cx + geometryVariant, chunk.cz - geometryVariant, 8240 + treeIndex) * 0.18
      : 0.82 + survivalMath.survivalHash01(chunk.cx + geometryVariant, chunk.cz - geometryVariant, 8240 + treeIndex) * 0.42;
    const radiusScale = profile.canopyRadius * midDistanceTreeScale * radiusStretch;
    placements.push({
      meshIndex: survivalBiomes.indexOf(chunk.biome) * 4 + geometryVariant,
      biome: chunk.biome,
      x: worldX,
      y: y + 0.04,
      z: worldZ,
      pitch: lean,
      yaw: survivalMath.survivalHash01(chunk.cx, chunk.cz, 2430 + index) * Math.PI * 2,
      roll: -lean * 0.62,
      scaleX: radiusScale,
      scaleY: profile.trunkHeight * midDistanceTreeScale * heightStretch,
      scaleZ: radiusScale * depthStretch,
    });
    generated.push({ localX, localZ });
  }
  return placements;
};

const addExactDenseFoliageForChunk = (chunk: SurvivalChunk) => {
  const next = makeExactFoliageForChunk(chunk, true);
  foliagePlacements.push(...next);
  foliageCounts[chunk.biome] = (foliageCounts[chunk.biome] ?? 0) + next.length;
};

const makeStreamingChunk = (cx: number, cz: number, distance: 0 | 1): SurvivalChunk => ({
  ...makeSurvivalChunk(cx, cz),
  distance,
  lod: distance === 0 ? "near" : "mid",
});
const streamingDecorationCoords: ReadonlyArray<readonly [number, number, 0 | 1]> = [
  [7, 4, 0],
  [8, 4, 1],
  [-17, 9, 0],
];
const streamingTreeFixtures: SurvivalStreamingTreeFixture[] = streamingDecorationCoords.map(([cx, cz, distance]) => {
  const chunk = makeStreamingChunk(cx, cz, distance);
  return {
    cx,
    cz,
    distance,
    lod: chunk.lod as "near" | "mid",
    trees: makeExactFoliageForChunk(chunk, true),
  };
});
const streamingWaterFixtures: SurvivalStreamingWaterFixture[] = streamingDecorationCoords.map(([cx, cz, distance]) => {
  const chunk = makeStreamingChunk(cx, cz, distance);
  const suppressWholeChunk = survivalBiome.isSurvivalRestoredMeadowWaterSuppressed(
    chunk.x,
    chunk.z,
    blockSize * 1.36,
  );
  const riverGeometry = chunk.hasRiver && !suppressWholeChunk
    ? survivalRivers.makeSurvivalRiverSurfaceGeometry(
      chunk,
      terrainSurface.getSurvivalTerrainHeightForChunk,
    )
    : null;
  const riverPosition = riverGeometry?.getAttribute("position") as import("three").BufferAttribute | undefined;
  const riverSampleIndices = riverPosition && riverPosition.count > 0
    ? [0, Math.floor(riverPosition.count / 2), riverPosition.count - 1]
    : [];
  return {
    cx,
    cz,
    distance,
    lod: chunk.lod as "near" | "mid",
    riverVertexCount: riverPosition?.count ?? 0,
    riverIndexCount: riverGeometry?.getIndex()?.count ?? 0,
    riverPositionSamples: riverSampleIndices.map(index => ({
      x: riverPosition!.getX(index) - chunk.x,
      y: riverPosition!.getY(index),
      z: riverPosition!.getZ(index) - chunk.z,
    })),
    ponds: suppressWholeChunk ? [] : survivalWaterFeatures.makeSurvivalPonds(chunk),
    lilies: survivalWaterFeatures.makeSurvivalLilyPads(chunk),
  };
});

for (let cz = minimumChunkZ; cz <= maximumChunkZ; cz += 1) {
  for (let cx = minimumChunkX; cx <= maximumChunkX; cx += 1) {
    const villageKind = villageRegistry.getSpecialSurvivalVillageKind(cx, cz);
    if ((cx === 0 && cz === 0) || villageKind !== null) {
      skippedChunks.push(`${cx}:${cz}${villageKind ? `:${villageKind}` : ":base-village"}`);
      continue;
    }

    const chunk = makeSurvivalChunk(cx, cz);
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
        const mountainLift = getUnityMountainPerimeterLift(worldX, worldZ, blockSize);
        const y = terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ) + mountainLift;
        terrainSurface.getSurvivalRenderedTerrainColorInto(worldX, worldZ, y, colorScratch);
        const mountainColor = getUnityMountainBandedColor(
          worldX,
          worldZ,
          mountainLift,
          blockSize,
          colorScratch,
        );
        positions.push(worldX, y, worldZ);
        colors.push(mountainColor.r, mountainColor.g, mountainColor.b);
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
    addExactDenseFoliageForChunk(chunk);
  }
}

const sourceHash = createHash("sha256");
sourceHash.update("unity-mountain-caldera-perimeter-v3-banded");
for (const sourcePath of sourcePaths) {
  sourceHash.update(sourcePath.replace(reactRoot, "").replaceAll("\\", "/"));
  sourceHash.update(await readFile(sourcePath));
}

const document = {
  schemaVersion: 2,
  generator: "Tools/bake-survival-terrain-assets.mts",
  reactOracle: reactRoot,
  sourceSignature: sourceHash.digest("hex"),
  blockSize,
  bounds: {
    minimumChunkX,
    maximumChunkX,
    minimumChunkZ,
    maximumChunkZ,
  },
  segments,
  includedChunks,
  skippedChunks,
  streamingOracle: {
    source: "survivalChunks.ts + survivalTerrainGeometry.ts + survivalTerrainSurface.ts",
    renderRadius: 3,
    nearRadius: 1,
    collisionRadius: 2,
    centerHysteresis: blockSize * 0.72,
    chunkCoordinates: streamingChunkCoordinateFixtures,
    window: streamingWindow,
    chunks: streamingChunkFixtures,
    trees: streamingTreeFixtures,
    waters: streamingWaterFixtures,
  },
  foliage: {
    source: "survivalSolidTreeGroveRendering.tsx dense desktop + survivalTreeVisuals.ts",
    meshCount: foliageMeshes.length,
    placementCount: foliagePlacements.length,
    countsByBiome: foliageCounts,
    meshes: foliageMeshes,
    placements: foliagePlacements,
  },
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
  foliagePlacementCount: foliagePlacements.length,
  streamingFixtureChunkCount: streamingChunkFixtures.length,
  streamingWindowCount: streamingWindow.length,
  foliageCounts,
  skippedChunks,
  sourceSignature: document.sourceSignature,
}, null, 2));

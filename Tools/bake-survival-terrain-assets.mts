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
const biomePath = path.join(sourceRoot, "survival", "survivalBiome.ts");
const riversPath = path.join(sourceRoot, "survival", "survivalRivers.ts");
const mathPath = path.join(sourceRoot, "survival", "survivalMath.ts");
const grassSurfacePath = path.join(sourceRoot, "survival", "survivalGrassSurface.ts");
const treeVisualsPath = path.join(sourceRoot, "vegetation", "survivalTreeVisuals.ts");
const foliagePalettesPath = path.join(sourceRoot, "vegetation", "survivalFoliagePalettes.ts");
const grassConfigPath = path.join(sourceRoot, "vegetation", "survivalBotwGrassConfig.ts");
const storePath = path.join(reactRoot, "src", "store", "gameStore.ts");
const sourcePaths = [
  terrainSurfacePath,
  villageRegistryPath,
  storePath,
  biomePath,
  riversPath,
  path.join(sourceRoot, "survival", "survivalRoutes.ts"),
  mathPath,
  path.join(sourceRoot, "survival", "survivalWorldConfig.ts"),
  path.join(sourceRoot, "terrain", "survivalTerrainGeometry.ts"),
  path.join(sourceRoot, "villages", "survivalGraveyardVillageTerrain.ts"),
  path.join(sourceRoot, "villages", "survivalVillagePad.ts"),
  grassSurfacePath,
  treeVisualsPath,
  foliagePalettesPath,
  grassConfigPath,
];

const terrainSurface = await import(pathToFileURL(terrainSurfacePath).href);
const villageRegistry = await import(pathToFileURL(villageRegistryPath).href);
const survivalBiome = await import(pathToFileURL(biomePath).href);
const survivalRivers = await import(pathToFileURL(riversPath).href);
const survivalMath = await import(pathToFileURL(mathPath).href);
const survivalGrassSurface = await import(pathToFileURL(grassSurfacePath).href);
const survivalTreeVisuals = await import(pathToFileURL(treeVisualsPath).href);
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
  lod: "near";
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
const unityMountainCenterX = 3 * blockSize;
const unityMountainCenterZ = 0;
const unityMountainProtectedRadius = 96;
const unityMountainRimPeakRadius = 116;
const unityMountainRimOuterRadius = 142;
const unitySmoothstep = (value: number) => {
  const clamped = Math.max(0, Math.min(1, value));
  return clamped * clamped * (3 - 2 * clamped);
};
const getUnityMountainPerimeterLift = (worldX: number, worldZ: number) => {
  const localX = worldX - unityMountainCenterX;
  const localZ = worldZ - unityMountainCenterZ;
  const radius = Math.hypot(localX, localZ);
  if (radius <= unityMountainProtectedRadius) return 0;
  const angle = Math.atan2(localX, localZ);
  if (radius <= unityMountainRimPeakRadius) {
    const progress = unitySmoothstep((radius - unityMountainProtectedRadius) /
      (unityMountainRimPeakRadius - unityMountainProtectedRadius));
    const irregularRim = Math.sin(angle * 5 + 0.8) * 3.4 + Math.cos(angle * 9 - 0.35) * 1.8;
    return 214 + (232 + irregularRim - 214) * progress;
  }
  if (radius <= unityMountainRimOuterRadius) {
    const progress = unitySmoothstep((radius - unityMountainRimPeakRadius) /
      (unityMountainRimOuterRadius - unityMountainRimPeakRadius));
    const irregularRim = Math.sin(angle * 5 + 0.8) * 3.4 + Math.cos(angle * 9 - 0.35) * 1.8;
    return (232 + irregularRim) + (196 - (232 + irregularRim)) * progress;
  }
  const irregularOuterRadius = 500 + Math.sin(angle * 3 + 0.45) * 32 + Math.cos(angle * 7 - 0.2) * 16;
  if (radius >= irregularOuterRadius) return 0;
  const progress = (radius - unityMountainRimOuterRadius) /
    (irregularOuterRadius - unityMountainRimOuterRadius);
  const shoulder = 196 * Math.pow(Math.max(0, 1 - progress), 1.28);
  const ridge = (Math.sin(angle * 4 + radius * 0.018) * 3.8 +
    Math.cos(angle * 8 - radius * 0.011) * 2.2) *
    Math.pow(Math.max(0, 1 - progress), 1.6);
  return Math.max(0, shoulder + ridge);
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
const addExactDenseFoliageForChunk = (chunk: SurvivalChunk) => {
  const density = 0.92;
  const baseCount = chunk.biome === "jungle"
    ? 44
    : chunk.biome === "swamp"
      ? 38
      : chunk.biome === "mushroom"
        ? 34
        : chunk.biome === "desert"
          ? 22
          : 36;
  const count = Math.max(8, Math.round(baseCount * density));
  const attempts = count * 12;
  const generated: Array<{ localX: number; localZ: number }> = [];
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
      8.5,
      5.2,
    );
    if (surface.normal.y < 0.72 || surface.heightRange > 7.4) continue;
    const y = surface.y + getUnityMountainPerimeterLift(worldX, worldZ);
    const waterY = survivalBiome.getSurvivalWaterLevelAtWorld(worldX, worldZ);
    if (y < waterY + 0.2) continue;

    const baseSpacing = chunk.biome === "jungle"
      ? 22
      : chunk.biome === "swamp"
        ? 20
        : chunk.biome === "desert"
          ? 28
          : 23;
    const spacingSquared = baseSpacing * baseSpacing;
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
    const lean = (variant - 0.5) * (0.08 + geometryVariant * 0.018);
    const heightStretch = 0.82 +
      survivalMath.survivalHash01(chunk.cx + treeIndex, chunk.cz - treeIndex, 8220) * 0.46;
    const radiusStretch = 1.12 +
      survivalMath.survivalHash01(chunk.cx - treeIndex, chunk.cz + treeIndex, 8230) * 0.52;
    const depthStretch = 0.82 +
      survivalMath.survivalHash01(chunk.cx + geometryVariant, chunk.cz - geometryVariant, 8240 + treeIndex) * 0.42;
    const radiusScale = profile.canopyRadius * radiusStretch;
    foliagePlacements.push({
      meshIndex: survivalBiomes.indexOf(chunk.biome) * 4 + geometryVariant,
      biome: chunk.biome,
      x: worldX,
      y: y + 0.04,
      z: worldZ,
      pitch: lean,
      yaw: survivalMath.survivalHash01(chunk.cx, chunk.cz, 2430 + index) * Math.PI * 2,
      roll: -lean * 0.62,
      scaleX: radiusScale,
      scaleY: profile.trunkHeight * heightStretch,
      scaleZ: radiusScale * depthStretch,
    });
    generated.push({ localX, localZ });
  }
  foliageCounts[chunk.biome] = (foliageCounts[chunk.biome] ?? 0) + generated.length;
};

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
        const y = terrainSurface.getSurvivalTerrainHeightForChunk(chunk, localX, localZ) +
          getUnityMountainPerimeterLift(worldX, worldZ);
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
    addExactDenseFoliageForChunk(chunk);
  }
}

const sourceHash = createHash("sha256");
sourceHash.update("unity-mountain-caldera-perimeter-v1");
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
  foliageCounts,
  skippedChunks,
  sourceSignature: document.sourceSignature,
}, null, 2));

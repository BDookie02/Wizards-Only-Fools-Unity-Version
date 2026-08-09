import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const unityRoot = "D:\\CodexProjects\\Wizards-Only-Fools-Unity";
const outputRoot = path.join(unityRoot, "Assets", "WOF", "Art", "Generated", "React");
const avatarFactoryPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "rendering",
  "avatar",
  "avatarTextureFactory.ts",
);
const hutVisualsPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "baseVillageHutVisuals.ts",
);
const treeHouseTexturesPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "treeHouseVillageTextures.ts",
);
const launchMenuPath = path.join(reactRoot, "src", "game", "ui", "launch", "LaunchMenu.tsx");
const mobileSpellbookIconPath = path.join(
  reactRoot,
  "public",
  "sprites",
  "misc",
  "spellbook_icon.png",
);
const spellThumbnailSourcePath = path.join(
  reactRoot,
  "src",
  "game",
  "ui",
  "hud",
  "SpellThumbnail.tsx",
);
const bushesPath = path.join(reactRoot, "src", "game", "Bushes.tsx");
const baseVillageHutLayoutPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "baseVillageHutLayout.ts",
);
const villagerCharacterRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "villagerCharacterRuntime.ts",
);
const darrelGroveRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "darrelGroveRuntime.ts",
);
const darrelGroveTexturesPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "darrelGroveTextures.ts",
);
const darrelDragonRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "darrelDragonRuntime.ts",
);
const darrelDragonSourceRoot = path.join(reactRoot, "public", "sprites", "darrel-dragon");
const darrelDragonManifestPath = path.join(darrelDragonSourceRoot, "manifest.json");
const desertVillageRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalDesertVillageRuntime.ts",
);
const desertVillageRenderingPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalDesertVillageRendering.tsx",
);
const desertVillageTerrainPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalDesertVillageTerrain.ts",
);
const chicagoCityLayoutPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityLayout.ts",
);
const chicagoCityRenderingPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityRendering.tsx",
);
const chicagoCityCollidersPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityColliders.tsx",
);
const chicagoCityStreetRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityStreetRuntime.ts",
);
const chicagoCityTrafficRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityTrafficRuntime.ts",
);
const chicagoCityTexturesPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalChicagoCityTextures.ts",
);
const swampVillageRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalSwampVillageRuntime.ts",
);
const swampVillageRenderingPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalSwampVillageRendering.tsx",
);
const swampVillageTerrainPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
  "survivalSwampVillageTerrain.ts",
);
const mountainVillageSourceRoot = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "villages",
);
const mountainVillageSourceNames = [
  "survivalMountainVillageRendering.tsx",
  "mountainVillageCabinView.tsx",
  "mountainVillageCliffBreakup.tsx",
  "mountainVillageColliderGeometry.ts",
  "mountainVillageColliders.tsx",
  "mountainVillageDetailPhase.ts",
  "mountainVillageLayoutRuntime.ts",
  "mountainVillageMineshaftAccessRuntime.ts",
  "mountainVillageMineshaftBanquet.tsx",
  "mountainVillageMineshaftBanquetRuntime.ts",
  "mountainVillageMineshaftCatwalk.tsx",
  "mountainVillageMineshaftCatwalkRuntime.ts",
  "mountainVillageMineshaftExitBridge.tsx",
  "mountainVillageMineshaftHut.tsx",
  "mountainVillageMineshaftInterior.tsx",
  "mountainVillageMineshaftLadder.tsx",
  "mountainVillageMineshaftLighting.tsx",
  "mountainVillageMineshaftOpening.tsx",
  "mountainVillageMineshaftOpeningRuntime.ts",
  "mountainVillageMineshaftRuntime.ts",
  "mountainVillageMineshaftWallDecor.tsx",
  "mountainVillageMineshaftWallDecorRuntime.ts",
  "mountainVillageRetroWoodTexture.tsx",
  "mountainVillageSceneLayout.ts",
  "mountainVillageSlopeGrassView.tsx",
  "mountainVillageSnowCap.tsx",
  "mountainVillageTerrain.ts",
  "mountainVillageTerrainGeometry.ts",
  "mountainVillageTrailGeometry.ts",
  "mountainVillageTrailView.tsx",
  "mountainVillageWaterfallRuntime.ts",
  "mountainVillageWaterfallView.tsx",
  "mountainVillageWoodDetails.tsx",
] as const;
const mountainVillageSourcePaths = mountainVillageSourceNames.map((name) => path.join(mountainVillageSourceRoot, name));
const mountainVillageSceneLayoutPath = path.join(mountainVillageSourceRoot, "mountainVillageSceneLayout.ts");
const mountainVillageTerrainPath = path.join(mountainVillageSourceRoot, "mountainVillageTerrain.ts");
const mountainVillageTerrainGeometryPath = path.join(mountainVillageSourceRoot, "mountainVillageTerrainGeometry.ts");
const mountainVillageColliderGeometryPath = path.join(mountainVillageSourceRoot, "mountainVillageColliderGeometry.ts");
const mountainVillageMineshaftAccessRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageMineshaftAccessRuntime.ts");
const mountainVillageMineshaftBanquetRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageMineshaftBanquetRuntime.ts");
const mountainVillageMineshaftCatwalkRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageMineshaftCatwalkRuntime.ts");
const mountainVillageMineshaftOpeningRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageMineshaftOpeningRuntime.ts");
const mountainVillageMineshaftWallDecorRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageMineshaftWallDecorRuntime.ts");
const mountainVillageWaterfallRuntimePath = path.join(mountainVillageSourceRoot, "mountainVillageWaterfallRuntime.ts");
const graveyardVillageSourceNames = [
  "survivalGraveyardChapelCharacters.ts",
  "survivalGraveyardChapelDetails.tsx",
  "survivalGraveyardChapelExteriorParts.tsx",
  "survivalGraveyardChapelInterior.tsx",
  "survivalGraveyardChapelLayout.ts",
  "survivalGraveyardChapelPews.tsx",
  "survivalGraveyardChapelStructure.ts",
  "survivalGraveyardChapelView.tsx",
  "survivalGraveyardVillageColliders.tsx",
  "survivalGraveyardVillageGeometry.ts",
  "survivalGraveyardVillageGroundProps.tsx",
  "survivalGraveyardVillageLayout.ts",
  "survivalGraveyardVillageRendering.tsx",
  "survivalGraveyardVillageTerrain.ts",
  "survivalGraveyardVillageTombs.tsx",
] as const;
const graveyardVillageSourcePaths = graveyardVillageSourceNames.map((name) => path.join(mountainVillageSourceRoot, name));
const graveyardVillageLayoutPath = path.join(mountainVillageSourceRoot, "survivalGraveyardVillageLayout.ts");
const graveyardVillageTerrainPath = path.join(mountainVillageSourceRoot, "survivalGraveyardVillageTerrain.ts");
const graveyardVillageGeometryPath = path.join(mountainVillageSourceRoot, "survivalGraveyardVillageGeometry.ts");
const graveyardVillageCollidersPath = path.join(mountainVillageSourceRoot, "survivalGraveyardVillageColliders.tsx");
const graveyardVillageTombsPath = path.join(mountainVillageSourceRoot, "survivalGraveyardVillageTombs.tsx");
const graveyardChapelStructurePath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelStructure.ts");
const graveyardChapelLayoutPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelLayout.ts");
const graveyardChapelCharactersPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelCharacters.ts");
const graveyardChapelDetailsPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelDetails.tsx");
const graveyardChapelPewsPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelPews.tsx");
const graveyardChapelInteriorPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelInterior.tsx");
const graveyardChapelExteriorPartsPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelExteriorParts.tsx");
const graveyardChapelViewPath = path.join(mountainVillageSourceRoot, "survivalGraveyardChapelView.tsx");
const mountainSlopeGrassRuntimePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "vegetation",
  "survivalMountainSlopeGrass.ts",
);
const survivalGrassGeometryPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "vegetation",
  "survivalGrassGeometry.ts",
);
const swampToadSourceRoot = path.join(reactRoot, "public", "sprites", "swamp", "toad");
const swampToadManifestPath = path.join(swampToadSourceRoot, "manifest.json");
const survivalTerrainSurfacePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "survival",
  "survivalTerrainSurface.ts",
);
const survivalBiomePath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "survival",
  "survivalBiome.ts",
);
const survivalRiversPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "survival",
  "survivalRivers.ts",
);
const survivalMathPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "survival",
  "survivalMath.ts",
);
const survivalTerrainTexturesPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "terrain",
  "survivalTerrainTextures.ts",
);
const survivalGrassTexturesPath = path.join(
  reactRoot,
  "src",
  "game",
  "systems",
  "world",
  "vegetation",
  "survivalGrassTextures.ts",
);
const streamingAssetsRoot = path.join(
  unityRoot,
  "Assets",
  "StreamingAssets",
  "WOF",
  "Villagers",
  "Base",
);
const reactRequire = createRequire(path.join(reactRoot, "package.json"));
const { createCanvas, loadImage } = reactRequire("canvas") as typeof import("canvas");
const THREE = reactRequire("three") as typeof import("three");
type Canvas = ReturnType<typeof createCanvas>;

function assertDDrive(targetPath: string) {
  const resolved = path.resolve(targetPath);
  if (!/^D:\\/i.test(resolved)) {
    throw new Error(`Refusing non-D path: ${resolved}`);
  }
  return resolved;
}

assertDDrive(reactRoot);
assertDDrive(unityRoot);
assertDDrive(outputRoot);
assertDDrive(streamingAssetsRoot);

type CanvasDocument = {
  createElement(tagName: string): Canvas;
};

const canvasDocument: CanvasDocument = {
  createElement(tagName: string) {
    if (tagName.toLowerCase() !== "canvas") {
      throw new Error(`The React visual baker only supports canvas elements, not ${tagName}.`);
    }
    return createCanvas(1, 1);
  },
};

Object.defineProperty(globalThis, "document", {
  configurable: true,
  value: canvasDocument,
  writable: false,
});

function sha256(bytes: Uint8Array | string) {
  return createHash("sha256").update(bytes).digest("hex");
}

async function writeIfChanged(targetPath: string, bytes: Uint8Array) {
  assertDDrive(targetPath);
  await mkdir(path.dirname(targetPath), { recursive: true });
  try {
    const current = await readFile(targetPath);
    if (Buffer.compare(current, bytes) === 0) {
      return false;
    }
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
  }

  const temporaryPath = `${targetPath}.wof-bake-${process.pid}.tmp`;
  assertDDrive(temporaryPath);
  await writeFile(temporaryPath, bytes);
  try {
    await rename(temporaryPath, targetPath);
  } finally {
    await rm(temporaryPath, { force: true });
  }
  return true;
}

const avatarFactory = await import(pathToFileURL(avatarFactoryPath).href);
const hutVisuals = await import(pathToFileURL(hutVisualsPath).href);
const treeHouseTextures = await import(pathToFileURL(treeHouseTexturesPath).href);
const baseVillageHutLayout = await import(pathToFileURL(baseVillageHutLayoutPath).href);
const villagerCharacterRuntime = await import(pathToFileURL(villagerCharacterRuntimePath).href);
const darrelGroveRuntime = await import(pathToFileURL(darrelGroveRuntimePath).href);
const darrelGroveTextures = await import(pathToFileURL(darrelGroveTexturesPath).href);
const desertVillageRuntime = await import(pathToFileURL(desertVillageRuntimePath).href);
const chicagoCityLayout = await import(pathToFileURL(chicagoCityLayoutPath).href);
const chicagoCityStreetRuntime = await import(pathToFileURL(chicagoCityStreetRuntimePath).href);
const chicagoCityTrafficRuntime = await import(pathToFileURL(chicagoCityTrafficRuntimePath).href);
const chicagoCityTextures = await import(pathToFileURL(chicagoCityTexturesPath).href);
const swampVillageRuntime = await import(pathToFileURL(swampVillageRuntimePath).href);
const mountainVillageSceneLayout = await import(pathToFileURL(mountainVillageSceneLayoutPath).href);
const mountainVillageTerrain = await import(pathToFileURL(mountainVillageTerrainPath).href);
const mountainVillageTerrainGeometry = await import(pathToFileURL(mountainVillageTerrainGeometryPath).href);
const mountainVillageColliderGeometry = await import(pathToFileURL(mountainVillageColliderGeometryPath).href);
const mountainVillageMineshaftAccessRuntime = await import(pathToFileURL(mountainVillageMineshaftAccessRuntimePath).href);
const mountainVillageMineshaftBanquetRuntime = await import(pathToFileURL(mountainVillageMineshaftBanquetRuntimePath).href);
const mountainVillageMineshaftCatwalkRuntime = await import(pathToFileURL(mountainVillageMineshaftCatwalkRuntimePath).href);
const mountainVillageMineshaftOpeningRuntime = await import(pathToFileURL(mountainVillageMineshaftOpeningRuntimePath).href);
const mountainVillageMineshaftWallDecorRuntime = await import(pathToFileURL(mountainVillageMineshaftWallDecorRuntimePath).href);
const mountainVillageWaterfallRuntime = await import(pathToFileURL(mountainVillageWaterfallRuntimePath).href);
const graveyardVillageLayout = await import(pathToFileURL(graveyardVillageLayoutPath).href);
const graveyardVillageTerrain = await import(pathToFileURL(graveyardVillageTerrainPath).href);
const graveyardVillageGeometry = await import(pathToFileURL(graveyardVillageGeometryPath).href);
const graveyardVillageColliders = await import(pathToFileURL(graveyardVillageCollidersPath).href);
const graveyardVillageTombs = await import(pathToFileURL(graveyardVillageTombsPath).href);
const graveyardChapelStructure = await import(pathToFileURL(graveyardChapelStructurePath).href);
const graveyardChapelLayout = await import(pathToFileURL(graveyardChapelLayoutPath).href);
const graveyardChapelCharacters = await import(pathToFileURL(graveyardChapelCharactersPath).href);
const graveyardChapelDetails = await import(pathToFileURL(graveyardChapelDetailsPath).href);
const graveyardChapelPews = await import(pathToFileURL(graveyardChapelPewsPath).href);
const graveyardChapelInterior = await import(pathToFileURL(graveyardChapelInteriorPath).href);
const graveyardChapelExteriorParts = await import(pathToFileURL(graveyardChapelExteriorPartsPath).href);
const graveyardChapelView = await import(pathToFileURL(graveyardChapelViewPath).href);
const mountainSlopeGrassRuntime = await import(pathToFileURL(mountainSlopeGrassRuntimePath).href);
const survivalGrassGeometry = await import(pathToFileURL(survivalGrassGeometryPath).href);
const survivalTerrainSurface = await import(pathToFileURL(survivalTerrainSurfacePath).href);
const survivalBiome = await import(pathToFileURL(survivalBiomePath).href);
const survivalRivers = await import(pathToFileURL(survivalRiversPath).href);
const survivalMath = await import(pathToFileURL(survivalMathPath).href);
const survivalTerrainTextures = await import(pathToFileURL(survivalTerrainTexturesPath).href);
const survivalGrassTextures = await import(pathToFileURL(survivalGrassTexturesPath).href);

const outputs: Array<{ path: string; bytes: number; sha256: string }> = [];
let changedCount = 0;

async function emitCanvas(relativePath: string, canvas: Canvas) {
  const targetPath = path.join(outputRoot, relativePath);
  const bytes = canvas.toBuffer("image/png");
  if (await writeIfChanged(targetPath, bytes)) changedCount += 1;
  outputs.push({
    path: relativePath.replaceAll("\\", "/"),
    bytes: bytes.length,
    sha256: sha256(bytes),
  });
}

type GraveyardStoneTextureRole = "body" | "dark" | "accent" | "foundation";

function mixGraveyardStoneColor(baseHex: string, targetHex: string, amount: number) {
  return new THREE.Color(baseHex).lerp(new THREE.Color(targetHex), survivalMath.clamp01(amount)).getStyle();
}

function makeExactGraveyardStoneCanvas(baseHex: string, variant: number, role: GraveyardStoneTextureRole) {
  const seed = Math.round(variant * 10000);
  const canvas = createCanvas(128, 128);
  const ctx = canvas.getContext("2d");
  ctx.imageSmoothingEnabled = false;
  const highlight = mixGraveyardStoneColor(baseHex, role === "foundation" ? "#62705b" : "#efe6d2", role === "dark" ? 0.14 : 0.28);
  const midtone = mixGraveyardStoneColor(baseHex, role === "foundation" ? "#2f3a29" : "#8a8378", role === "accent" ? 0.22 : 0.16);
  const shadow = mixGraveyardStoneColor(baseHex, role === "foundation" ? "#0b0d09" : "#171512", role === "dark" ? 0.42 : 0.32);
  const moss = role === "foundation" ? "#4a5c33" : "#4f6741";
  const lichen = role === "dark" ? "#74776b" : "#cad0b1";

  ctx.fillStyle = baseHex;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  for (let y = 0; y < canvas.height; y += 2) {
    for (let x = 0; x < canvas.width; x += 2) {
      const grain = survivalMath.survivalHash01(x + seed, y - seed, 12480);
      const vein = Math.sin((x + seed * 0.017) * 0.16 + y * 0.045) + Math.cos((y - seed * 0.013) * 0.12 - x * 0.055);
      const damp = survivalMath.smoothstepRange(54, 126, y) * survivalMath.survivalHash01(x - seed, y + seed, 12481);
      if (vein > 1.18) {
        ctx.fillStyle = midtone;
        ctx.fillRect(x, y, 2, 2);
      } else if (grain > 0.91) {
        ctx.fillStyle = highlight;
        ctx.fillRect(x, y, 2, 2);
      } else if (grain < 0.1) {
        ctx.fillStyle = shadow;
        ctx.fillRect(x, y, 2, 2);
      } else if (damp > 0.78) {
        ctx.fillStyle = moss;
        ctx.fillRect(x, y, 2, 2);
      }
    }
  }

  for (let y = 10; y < canvas.height; y += 14 + Math.floor(survivalMath.survivalHash01(seed, y, 12482) * 7)) {
    ctx.fillStyle = "rgba(18, 16, 13, 0.18)";
    ctx.fillRect(0, y, canvas.width, 2);
    if (survivalMath.survivalHash01(seed, y, 12483) > 0.44) {
      ctx.fillStyle = "rgba(238, 232, 216, 0.14)";
      ctx.fillRect(0, y + 2, canvas.width, 2);
    }
  }

  for (let crack = 0; crack < 4; crack += 1) {
    let x = Math.floor(survivalMath.survivalHash01(seed, crack, 12484) * canvas.width);
    let y = Math.floor(survivalMath.survivalHash01(crack, seed, 12485) * 56) + 8;
    const length = 18 + Math.floor(survivalMath.survivalHash01(seed + crack, seed - crack, 12486) * 42);
    const drift = survivalMath.survivalHash01(crack, seed, 12487) > 0.5 ? 2 : -2;
    for (let step = 0; step < length; step += 4) {
      ctx.fillStyle = shadow;
      ctx.fillRect(Math.max(0, Math.min(canvas.width - 2, x)), Math.max(0, Math.min(canvas.height - 4, y)), 2, 4);
      if (step % 12 === 0) {
        ctx.fillStyle = "rgba(236, 228, 207, 0.18)";
        ctx.fillRect(Math.max(0, Math.min(canvas.width - 2, x + 2)), Math.max(0, Math.min(canvas.height - 2, y)), 2, 2);
      }
      x += survivalMath.survivalHash01(x + seed, y - seed, 12488) > 0.52 ? drift : 0;
      y += 4;
    }
  }

  for (let chip = 0; chip < 12; chip += 1) {
    const side = survivalMath.survivalHash01(seed, chip, 12489);
    const width = 4 + Math.floor(survivalMath.survivalHash01(chip, seed, 12490) * 12);
    const height = 2 + Math.floor(survivalMath.survivalHash01(seed + chip, chip, 12491) * 7);
    const x = side < 0.34 ? 0 : side < 0.68 ? canvas.width - width : Math.floor(survivalMath.survivalHash01(chip, seed, 12492) * (canvas.width - width));
    const y = side >= 0.68 ? 0 : Math.floor(survivalMath.survivalHash01(seed, chip, 12493) * (canvas.height - height));
    ctx.fillStyle = shadow;
    ctx.fillRect(x, y, width, height);
    ctx.fillStyle = "rgba(244, 238, 221, 0.2)";
    ctx.fillRect(Math.min(canvas.width - 2, x + 1), Math.min(canvas.height - 2, y + height), Math.max(2, width - 2), 2);
  }

  for (let spot = 0; spot < 14; spot += 1) {
    const x = Math.floor(survivalMath.survivalHash01(seed + spot, spot, 12494) * 120);
    const y = 58 + Math.floor(survivalMath.survivalHash01(spot, seed - spot, 12495) * 66);
    const size = 2 + Math.floor(survivalMath.survivalHash01(seed, spot, 12496) * 7);
    ctx.fillStyle = survivalMath.survivalHash01(spot, seed, 12497) > 0.5 ? moss : lichen;
    ctx.fillRect(x, y, size, 2);
    ctx.fillRect(x + 2, y + 2, Math.max(2, size - 2), 2);
  }
  return canvas;
}

function makeExactGraveyardInscriptionCanvas(name: string, joke: string, variant: number) {
  const canvas = createCanvas(256, 160);
  const ctx = canvas.getContext("2d");
  ctx.imageSmoothingEnabled = false;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = variant % 2 === 0 ? "#cfc8b8" : "#b9b2a3";
  ctx.fillRect(10, 14, 236, 132);
  ctx.fillStyle = "#81796d";
  ctx.fillRect(10, 138, 236, 8);
  ctx.fillRect(238, 22, 8, 124);
  ctx.fillStyle = "#f1ead9";
  ctx.fillRect(18, 22, 214, 6);
  ctx.fillStyle = "rgba(47, 43, 36, 0.22)";
  ctx.fillRect(22, 130, 202, 2);
  ctx.fillRect(28, 72, 172, 2);
  for (let speckle = 0; speckle < 95; speckle += 1) {
    const x = 18 + Math.floor(survivalMath.survivalHash01(variant + speckle, speckle, 12510) * 214);
    const y = 30 + Math.floor(survivalMath.survivalHash01(speckle, variant - speckle, 12511) * 104);
    ctx.fillStyle = survivalMath.survivalHash01(variant, speckle, 12512) > 0.58
      ? "rgba(245, 238, 219, 0.48)"
      : "rgba(43, 38, 30, 0.32)";
    ctx.fillRect(x, y, 2, 2);
  }
  for (let crack = 0; crack < 3; crack += 1) {
    let x = 38 + Math.floor(survivalMath.survivalHash01(variant, crack, 12513) * 156);
    let y = 34 + Math.floor(survivalMath.survivalHash01(crack, variant, 12514) * 76);
    const length = 16 + Math.floor(survivalMath.survivalHash01(variant + crack, crack, 12515) * 34);
    const drift = survivalMath.survivalHash01(crack, variant, 12516) > 0.5 ? 2 : -2;
    for (let step = 0; step < length; step += 4) {
      ctx.fillStyle = "rgba(32, 27, 20, 0.42)";
      ctx.fillRect(x, y, 2, 4);
      if (step % 12 === 0) {
        ctx.fillStyle = "rgba(242, 235, 217, 0.24)";
        ctx.fillRect(x + 2, y, 2, 2);
      }
      x += survivalMath.survivalHash01(x + variant, y, 12517) > 0.54 ? drift : 0;
      y += 4;
    }
  }
  const chipRects: Array<[number, number, number, number]> = [
    [10, 14, 24, 8], [214, 14, 32, 10], [10, 118, 18, 20], [220, 124, 26, 14],
  ];
  for (const [x, y, width, height] of chipRects) {
    ctx.fillStyle = "rgba(67, 59, 48, 0.38)";
    ctx.fillRect(x, y, width, height);
    ctx.fillStyle = "rgba(247, 239, 221, 0.22)";
    ctx.fillRect(x + 2, y + height, Math.max(2, width - 4), 2);
  }
  ctx.fillStyle = "#171512";
  ctx.font = "bold 18px monospace";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(name, 128, 50);
  ctx.fillStyle = "#2b2720";
  ctx.font = "bold 14px monospace";
  const words = joke.split(" ");
  const lines: string[] = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (next.length > 22 && current) {
      lines.push(current);
      current = word;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  for (let index = 0; index < Math.min(3, lines.length); index += 1) {
    ctx.fillText(lines[index], 128, 86 + index * 19);
  }
  return canvas;
}

async function emitBytes(relativePath: string, bytes: Uint8Array) {
  const targetPath = path.join(outputRoot, relativePath);
  if (await writeIfChanged(targetPath, bytes)) changedCount += 1;
  outputs.push({
    path: relativePath.replaceAll("\\", "/"),
    bytes: bytes.length,
    sha256: sha256(bytes),
  });
}

async function emitStreamingBytes(relativePath: string, bytes: Uint8Array) {
  const targetPath = path.join(streamingAssetsRoot, relativePath);
  if (await writeIfChanged(targetPath, bytes)) changedCount += 1;
  outputs.push({
    path: `StreamingAssets/WOF/Villagers/Base/${relativePath.replaceAll("\\", "/")}`,
    bytes: bytes.length,
    sha256: sha256(bytes),
  });
}

function recordStreamingBytes(relativePath: string, bytes: Uint8Array) {
  outputs.push({
    path: `StreamingAssets/WOF/Villagers/Base/${relativePath.replaceAll("\\", "/")}`,
    bytes: bytes.length,
    sha256: sha256(bytes),
  });
}

function buildVillagerFrameArchive(entries: Array<{ key: string; bytes: Buffer }>) {
  const magic = Buffer.from("WOFAV01\0", "ascii");
  const encodedKeys = entries.map((entry) => Buffer.from(entry.key, "utf8"));
  const headerBytes = magic.length + 4 + encodedKeys.reduce((total, key) => total + 1 + key.length + 4 + 4, 0);
  const payloadBytes = entries.reduce((total, entry) => total + entry.bytes.length, 0);
  const archive = Buffer.allocUnsafe(headerBytes + payloadBytes);
  magic.copy(archive, 0);
  archive.writeUInt32LE(entries.length, magic.length);

  let headerOffset = magic.length + 4;
  let payloadOffset = headerBytes;
  for (let index = 0; index < entries.length; index += 1) {
    const entry = entries[index];
    const key = encodedKeys[index];
    if (key.length > 255) throw new Error(`Villager frame key is too long: ${entry.key}`);
    archive.writeUInt8(key.length, headerOffset);
    headerOffset += 1;
    key.copy(archive, headerOffset);
    headerOffset += key.length;
    archive.writeUInt32LE(payloadOffset, headerOffset);
    headerOffset += 4;
    archive.writeUInt32LE(entry.bytes.length, headerOffset);
    headerOffset += 4;
    entry.bytes.copy(archive, payloadOffset);
    payloadOffset += entry.bytes.length;
  }

  return archive;
}

function serializeThreeGeometry(geometry: InstanceType<typeof THREE.BufferGeometry>) {
  const positions = geometry.getAttribute("position");
  const normals = geometry.getAttribute("normal");
  const colors = geometry.getAttribute("color");
  const uvs = geometry.getAttribute("uv");
  const index = geometry.getIndex();
  if (!positions || !normals || !index) {
    throw new Error("Exact React geometry is missing a required position, normal, or index buffer.");
  }
  return {
    vertexCount: positions.count,
    positions: Array.from(positions.array),
    normals: Array.from(normals.array),
    colors: colors ? Array.from(colors.array) : [],
    uvs: uvs ? Array.from(uvs.array) : [],
    indices: Array.from(index.array),
  };
}

function serializeThreeBasicGeometry(geometry: InstanceType<typeof THREE.BufferGeometry>) {
  const positions = geometry.getAttribute("position");
  const normals = geometry.getAttribute("normal");
  const colors = geometry.getAttribute("color");
  const uvs = geometry.getAttribute("uv");
  const index = geometry.getIndex();
  if (!positions || (!index && positions.count % 3 !== 0)) {
    throw new Error("Exact React basic geometry has an unexpected position/index contract.");
  }
  return {
    vertexCount: positions.count,
    positions: Array.from(positions.array),
    normals: normals ? Array.from(normals.array) : [],
    colors: colors ? Array.from(colors.array) : [],
    uvs: uvs ? Array.from(uvs.array) : [],
    indices: index ? Array.from(index.array) : Array.from({ length: positions.count }, (_, itemIndex) => itemIndex),
  };
}

function makeExactMountainSlopeGrassGeometry(tufts: Array<Record<string, number>>) {
  const source = survivalGrassGeometry.getSurvivalTutorialGrassTuftGeometry(6);
  const sourcePositions = source.getAttribute("position");
  const sourceColors = source.getAttribute("color");
  const sourceIndices = source.getIndex();
  if (!sourcePositions || !sourceColors || !sourceIndices) {
    throw new Error("Exact React mountain slope-grass source geometry is incomplete.");
  }

  const sourceVertexCount = sourcePositions.count;
  const sourceIndexCount = sourceIndices.count;
  const positions = new Float32Array(tufts.length * sourceVertexCount * 3);
  const colors = new Float32Array(tufts.length * sourceVertexCount * 3);
  const uvs = new Float32Array(tufts.length * sourceVertexCount * 2);
  const indices = new Uint32Array(tufts.length * sourceIndexCount);
  const sourceUp = new THREE.Vector3(0, 1, 0);
  const normal = new THREE.Vector3();
  const vertex = new THREE.Vector3();
  const dummy = new THREE.Object3D();

  for (let tuftIndex = 0; tuftIndex < tufts.length; tuftIndex += 1) {
    const tuft = tufts[tuftIndex];
    normal.set(tuft.normalX, tuft.normalY, tuft.normalZ).normalize();
    dummy.position.set(tuft.localX, tuft.y, tuft.localZ).addScaledVector(normal, 0.052);
    dummy.quaternion.setFromUnitVectors(sourceUp, normal);
    dummy.rotateY(tuft.yaw);
    dummy.scale.set(tuft.width, tuft.height, tuft.width);
    dummy.updateMatrix();

    const vertexBase = tuftIndex * sourceVertexCount;
    for (let sourceVertex = 0; sourceVertex < sourceVertexCount; sourceVertex += 1) {
      vertex.fromBufferAttribute(sourcePositions, sourceVertex).applyMatrix4(dummy.matrix);
      const targetOffset = (vertexBase + sourceVertex) * 3;
      positions[targetOffset] = vertex.x;
      positions[targetOffset + 1] = vertex.y;
      positions[targetOffset + 2] = vertex.z;
      colors[targetOffset] = sourceColors.getX(sourceVertex) * tuft.colorR;
      colors[targetOffset + 1] = sourceColors.getY(sourceVertex) * tuft.colorG;
      colors[targetOffset + 2] = sourceColors.getZ(sourceVertex) * tuft.colorB;
    }

    const indexBase = tuftIndex * sourceIndexCount;
    for (let sourceIndex = 0; sourceIndex < sourceIndexCount; sourceIndex += 1) {
      indices[indexBase + sourceIndex] = vertexBase + sourceIndices.getX(sourceIndex);
    }
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
  geometry.setAttribute("uv", new THREE.BufferAttribute(uvs, 2));
  geometry.setIndex(new THREE.BufferAttribute(indices, 1));
  geometry.computeVertexNormals();
  return geometry;
}

function makeExactSwampToadSleepZCanvas() {
  const canvas = createCanvas(160, 96);
  const ctx = canvas.getContext("2d");
  ctx.imageSmoothingEnabled = false;
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  const pixelRect = (x: number, y: number, width: number, height: number, color: string) => {
    ctx.fillStyle = color;
    ctx.fillRect(Math.round(x), Math.round(y), Math.round(width), Math.round(height));
  };
  const drawPixelZ = (x: number, y: number, unit: number, color: string, shadowColor: string) => {
    const rows = ["11111", "00010", "00100", "01000", "11111"];
    const drawRows = (offsetX: number, offsetY: number, fill: string) => {
      for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const row = rows[rowIndex];
        for (let colIndex = 0; colIndex < row.length; colIndex += 1) {
          if (row[colIndex] !== "1") continue;
          pixelRect(x + offsetX + colIndex * unit, y + offsetY + rowIndex * unit, unit, unit, fill);
        }
      }
    };
    drawRows(unit * 0.7, unit * 0.7, shadowColor);
    drawRows(0, 0, color);
  };

  drawPixelZ(8, 48, 8, "#e0f2fe", "rgba(30,64,175,0.72)");
  drawPixelZ(58, 30, 6, "#bfdbfe", "rgba(30,64,175,0.62)");
  drawPixelZ(96, 16, 5, "#93c5fd", "rgba(30,64,175,0.52)");
  drawPixelZ(126, 6, 4, "#dbeafe", "rgba(30,64,175,0.44)");
  return canvas;
}

function makeExactSwampLilyPadGeometry() {
  const shape = new THREE.Shape();
  const cutAngle = 0.44;
  shape.moveTo(0, 0);
  for (let step = 0; step <= 28; step += 1) {
    const t = step / 28;
    const angle = cutAngle + t * (Math.PI * 2 - cutAngle * 2);
    shape.lineTo(Math.cos(angle), Math.sin(angle));
  }
  shape.lineTo(0, 0);
  const geometry = new THREE.ShapeGeometry(shape);
  geometry.rotateX(-Math.PI / 2);
  geometry.computeVertexNormals();
  return geometry;
}

function makeExactDesertSurfaceStripGeometry(
  chunk: Record<string, unknown>,
  width: number,
  length: number,
  rotation: number,
  yOffset: number,
  lateralOffset = 0,
  lengthOffset = 0,
  lateralSegments = 2,
  lengthSegments = 18,
) {
  const baseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(chunk);
  const geometry = new THREE.PlaneGeometry(
    width,
    length,
    Math.max(1, lateralSegments),
    Math.max(1, lengthSegments),
  );
  geometry.rotateX(-Math.PI / 2);
  const positions = geometry.getAttribute("position");
  const cos = Math.cos(rotation);
  const sin = Math.sin(rotation);
  for (let index = 0; index < positions.count; index += 1) {
    const stripX = positions.getX(index) + lateralOffset;
    const stripZ = positions.getZ(index) + lengthOffset;
    const localX = stripX * cos + stripZ * sin;
    const localZ = -stripX * sin + stripZ * cos;
    const height = survivalTerrainSurface.getSurvivalVillagePadHeight(chunk, localX, localZ, baseHeight);
    positions.setXYZ(index, localX, height + yOffset, localZ);
  }
  geometry.computeVertexNormals();
  return geometry;
}

const bushSourceGeometry = new THREE.DodecahedronGeometry(0.5, 0);
const bushGeometry = bushSourceGeometry.index ? bushSourceGeometry.toNonIndexed() : bushSourceGeometry;
const bushPositions = bushGeometry.getAttribute("position");
const bushNormals = bushGeometry.getAttribute("normal");
const bushBarycentric: number[] = [];
for (let index = 0; index < bushPositions.count; index += 3) {
  bushBarycentric.push(1, 0, 0, 0, 1, 0, 0, 0, 1);
}
const bushGeometryBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "new THREE.DodecahedronGeometry(0.5, 0).toNonIndexed()",
  vertexCount: bushPositions.count,
  positions: Array.from(bushPositions.array),
  normals: Array.from(bushNormals.array),
  barycentric: bushBarycentric,
}, null, 2)}\n`, "utf8");
await emitBytes("Geometry/bush-dodecahedron.json", bushGeometryBytes);
if (bushGeometry !== bushSourceGeometry) bushGeometry.dispose();
bushSourceGeometry.dispose();

const hutTextureEntries: Array<[string, () => { image: Canvas }]> = [
  ...hutVisuals.createMushroomCapTextures().map(
    (texture: { image: Canvas }, index: number) => [`Huts/mushroom_cap_${index}.png`, () => texture] as const,
  ),
  ["Huts/stem_wall.png", hutVisuals.createStemWallTexture],
  ["Huts/dirt_door.png", hutVisuals.createDirtDoorTexture],
  ["Huts/grass.png", hutVisuals.createGrassTexture],
  ["Huts/log.png", hutVisuals.createLogTexture],
  ["Huts/dirt_grass.png", hutVisuals.createDirtGrassTexture],
  ["Huts/wood_plank.png", hutVisuals.createWoodPlankTexture],
  ["Huts/dirt_wall.png", hutVisuals.createDirtWallTexture],
];

for (const [relativePath, createTexture] of hutTextureEntries) {
  await emitCanvas(relativePath, createTexture().image);
}

await emitCanvas("TreeHouse/bark.png", treeHouseTextures.getTreeHouseBarkTexture().image);
await emitCanvas("TreeHouse/plank.png", treeHouseTextures.getTreeHousePlankTexture().image);
await emitCanvas(
  "Vegetation/botw-grass.png",
  survivalGrassTextures.getSurvivalBotwGrassTexture().image as Canvas,
);

// Mirrors LaunchMenu.tsx's press-stage CSS radial gradient at the Unity UI's
// 1920x1080 reference resolution. The browser composites the translucent
// gradient over the app's black frame, so bake that final opaque result.
const pressBackground = createCanvas(1920, 1080);
const pressBackgroundContext = pressBackground.getContext("2d");
pressBackgroundContext.fillStyle = "#000000";
pressBackgroundContext.fillRect(0, 0, pressBackground.width, pressBackground.height);
const pressGradientRadius = Math.hypot(pressBackground.width / 2, pressBackground.height / 2);
const pressGradient = pressBackgroundContext.createRadialGradient(
  pressBackground.width / 2,
  pressBackground.height / 2,
  0,
  pressBackground.width / 2,
  pressBackground.height / 2,
  pressGradientRadius,
);
pressGradient.addColorStop(0, "rgba(88,28,135,0.35)");
pressGradient.addColorStop(0.62, "rgba(5,2,7,0.96)");
pressGradient.addColorStop(1, "rgba(5,2,7,0.96)");
pressBackgroundContext.fillStyle = pressGradient;
pressBackgroundContext.fillRect(0, 0, pressBackground.width, pressBackground.height);
await emitCanvas("Launch/press-background.png", pressBackground);

for (let frame = 1; frame <= 4; frame += 1) {
  const sourcePath = path.join(reactRoot, "public", "sprites", "misc", `idle_${frame}.png`);
  const source = await loadImage(sourcePath);
  const handCanvas = createCanvas(source.width, source.height);
  const handContext = handCanvas.getContext("2d", { willReadFrequently: true });
  handContext.drawImage(source, 0, 0);
  const imageData = handContext.getImageData(0, 0, handCanvas.width, handCanvas.height);
  for (let index = 0; index < imageData.data.length; index += 4) {
    if (
      imageData.data[index] < 20 &&
      imageData.data[index + 1] < 20 &&
      imageData.data[index + 2] < 20
    ) {
      imageData.data[index + 3] = 0;
    }
  }
  handContext.putImageData(imageData, 0, 0);

  const scale = Math.min(1, 220 / Math.max(handCanvas.width, handCanvas.height));
  const pixelWidth = Math.max(1, Math.round(handCanvas.width * scale));
  const pixelHeight = Math.max(1, Math.round(handCanvas.height * scale));
  const tiny = createCanvas(pixelWidth, pixelHeight);
  const tinyContext = tiny.getContext("2d");
  tinyContext.imageSmoothingEnabled = false;
  tinyContext.drawImage(handCanvas, 0, 0, pixelWidth, pixelHeight);
  handContext.imageSmoothingEnabled = false;
  handContext.clearRect(0, 0, handCanvas.width, handCanvas.height);
  handContext.drawImage(tiny, 0, 0, pixelWidth, pixelHeight, 0, 0, handCanvas.width, handCanvas.height);
  handContext.clearRect(0, 0, handCanvas.width, 15);
  handContext.clearRect(0, handCanvas.height - 2, handCanvas.width, 2);
  handContext.clearRect(0, 0, 2, handCanvas.height);
  handContext.clearRect(handCanvas.width - 2, 0, 2, handCanvas.height);
  await emitCanvas(`HUD/Hands/idle_${frame}.png`, handCanvas);

  const equippedLeft = createCanvas(handCanvas.width, handCanvas.height);
  const equippedLeftContext = equippedLeft.getContext("2d");
  equippedLeftContext.drawImage(handCanvas, 0, 0);
  equippedLeftContext.globalCompositeOperation = "destination-in";
  equippedLeftContext.beginPath();
  equippedLeftContext.moveTo(0, 0);
  equippedLeftContext.lineTo(handCanvas.width * 0.42, 0);
  equippedLeftContext.lineTo(handCanvas.width * 0.42, handCanvas.height * 0.60);
  equippedLeftContext.lineTo(handCanvas.width * 0.50, handCanvas.height * 0.60);
  equippedLeftContext.lineTo(handCanvas.width * 0.50, handCanvas.height);
  equippedLeftContext.lineTo(0, handCanvas.height);
  equippedLeftContext.closePath();
  equippedLeftContext.fill();
  equippedLeftContext.globalCompositeOperation = "source-over";
  await emitCanvas(`HUD/Hands/Equipped/left_idle_${frame}.png`, equippedLeft);

  const equippedRight = createCanvas(handCanvas.width, handCanvas.height);
  const equippedRightContext = equippedRight.getContext("2d");
  equippedRightContext.translate(handCanvas.width, 0);
  equippedRightContext.scale(-1, 1);
  equippedRightContext.drawImage(handCanvas, 0, 0);
  equippedRightContext.setTransform(1, 0, 0, 1, 0, 0);
  equippedRightContext.clearRect(0, 0, handCanvas.width * 0.53, handCanvas.height);
  await emitCanvas(`HUD/Hands/Equipped/right_idle_${frame}.png`, equippedRight);

  // React firing pose: the right upright hand is used directly on the right;
  // the same upright hand is mirrored onto the left. The UI applies the 0.76
  // firing scale at runtime so the source pixels remain identical.
  const firingRight = createCanvas(handCanvas.width, handCanvas.height);
  const firingRightContext = firingRight.getContext("2d");
  firingRightContext.drawImage(handCanvas, 0, 0);
  firingRightContext.clearRect(0, 0, handCanvas.width * 0.47, handCanvas.height);
  await emitCanvas(`HUD/Hands/Firing/right_idle_${frame}.png`, firingRight);

  const firingLeft = createCanvas(handCanvas.width, handCanvas.height);
  const firingLeftContext = firingLeft.getContext("2d");
  firingLeftContext.translate(handCanvas.width, 0);
  firingLeftContext.scale(-1, 1);
  firingLeftContext.drawImage(handCanvas, 0, 0);
  firingLeftContext.setTransform(1, 0, 0, 1, 0, 0);
  firingLeftContext.clearRect(handCanvas.width * 0.535, 0, handCanvas.width * 0.465, handCanvas.height);
  await emitCanvas(`HUD/Hands/Firing/left_idle_${frame}.png`, firingLeft);
}

async function emitProcessedSpellFrame(sourcePath: string, relativePath: string) {
  const source = await loadImage(sourcePath);
  const spellCanvas = createCanvas(48, 48);
  const spellContext = spellCanvas.getContext("2d", { willReadFrequently: true });
  const cropX = source.width * 0.02;
  const cropY = source.height * 0.02;
  const cropWidth = source.width * 0.96;
  const cropHeight = source.height * 0.96;
  spellContext.drawImage(source, cropX, cropY, cropWidth, cropHeight, 0, 0, 48, 48);
  const imageData = spellContext.getImageData(0, 0, 48, 48);
  for (let index = 0; index < imageData.data.length; index += 4) {
    const red = imageData.data[index];
    const green = imageData.data[index + 1];
    const blue = imageData.data[index + 2];
    const distanceSquared = red * red + green * green + blue * blue;
    if (distanceSquared >= 2500) continue;
    if (distanceSquared < 900) {
      imageData.data[index + 3] = 0;
    } else {
      const alpha = Math.floor(255 * ((Math.sqrt(distanceSquared) - 30) / 20));
      imageData.data[index + 3] = Math.min(imageData.data[index + 3], alpha);
    }
  }
  spellContext.putImageData(imageData, 0, 0);
  await emitCanvas(relativePath, spellCanvas);
  return spellCanvas;
}

for (let frame = 1; frame <= 5; frame += 1) {
  await emitProcessedSpellFrame(
    path.join(reactRoot, "public", "sprites", "fireball", `fireball_${frame}.png`),
    `HUD/Fireball/fireball_${frame}.png`,
  );
}
for (let frame = 1; frame <= 10; frame += 1) {
  const processedFrame = await emitProcessedSpellFrame(
    path.join(reactRoot, "public", "sprites", "fireball", `fireballidle_${frame}.png`),
    `HUD/Fireball/fireballidle_${frame}.png`,
  );

  const fireballSize = 160 * 0.92;
  const leftPalm = createCanvas(859, 495);
  leftPalm.getContext("2d").drawImage(
    processedFrame,
    338 - fireballSize / 2,
    218 - fireballSize,
    fireballSize,
    fireballSize,
  );
  await emitCanvas(`HUD/Fireball/Equipped/left_fireballidle_${frame}.png`, leftPalm);

  const rightPalmBeforeMirror = createCanvas(859, 495);
  rightPalmBeforeMirror.getContext("2d").drawImage(
    processedFrame,
    358 - fireballSize / 2,
    224 - fireballSize,
    fireballSize,
    fireballSize,
  );
  const rightPalm = createCanvas(859, 495);
  const rightPalmContext = rightPalm.getContext("2d");
  rightPalmContext.translate(859, 0);
  rightPalmContext.scale(-1, 1);
  rightPalmContext.drawImage(rightPalmBeforeMirror, 0, 0);
  await emitCanvas(`HUD/Fireball/Equipped/right_fireballidle_${frame}.png`, rightPalm);
}

const animations = [
  "idle",
  "walk",
  "sprint",
  "jump",
  "holding",
  "casting",
  "sleep",
  "damaged",
  "slide",
  "crouch",
  "crouchwalk",
  "grabbed",
  "startled",
  "angry",
  "meditate",
] as const;

for (const animation of animations) {
  for (let direction = 0; direction < 8; direction += 1) {
    for (let frame = 0; frame < 4; frame += 1) {
      const texture = avatarFactory.createAvatarTexture(undefined, animation, direction, frame);
      await emitCanvas(`Avatar/Default/${animation}/d${direction}_f${frame}.png`, texture.image as Canvas);
    }
  }
}

for (let direction = 0; direction < 8; direction += 1) {
  for (let frame = 0; frame < 4; frame += 1) {
    const texture = avatarFactory.createAvatarTexture(undefined, "idle", direction, frame, false, true);
    await emitCanvas(`Avatar/Default/idle-blink/d${direction}_f${frame}.png`, texture.image as Canvas);
  }
}

const baseVillageHuts = baseVillageHutLayout.getHutList();
if (baseVillageHuts.length !== 307) {
  throw new Error(`Expected 307 canonical base-village huts; found ${baseVillageHuts.length}.`);
}

const villagerBakeContract = "react-base-village-avatars-v2-52-unique-frames";
const villagerSourceSignature = sha256(Buffer.concat([
  Buffer.from(villagerBakeContract, "utf8"),
  await readFile(avatarFactoryPath),
  await readFile(baseVillageHutLayoutPath),
  await readFile(villagerCharacterRuntimePath),
]));
const existingVillagerLayoutPath = path.join(outputRoot, "Villagers", "base-village.json");
let bakedVillagers: Array<Record<string, unknown>> = [];
let reusableVillagerArchives: Array<{ file: string; bytes: Buffer }> | null = null;
try {
  const cachedDocument = JSON.parse(await readFile(existingVillagerLayoutPath, "utf8")) as {
    sourceSignature?: string;
    count?: number;
    frameContract?: { archiveEntriesPerVillager?: number };
    villagers?: Array<Record<string, unknown> & { archiveFile?: string; archiveSha256?: string }>;
  };
  if (
    cachedDocument.sourceSignature === villagerSourceSignature &&
    cachedDocument.count === 307 &&
    cachedDocument.frameContract?.archiveEntriesPerVillager === 52 &&
    cachedDocument.villagers?.length === 307
  ) {
    const candidateArchives: Array<{ file: string; bytes: Buffer }> = [];
    for (const record of cachedDocument.villagers) {
      if (!record.archiveFile || !record.archiveSha256) throw new Error("Cached villager record is incomplete.");
      const bytes = await readFile(path.join(streamingAssetsRoot, record.archiveFile));
      if (sha256(bytes) !== record.archiveSha256) throw new Error(`Cached archive hash mismatch: ${record.archiveFile}`);
      candidateArchives.push({ file: record.archiveFile, bytes });
    }
    bakedVillagers = cachedDocument.villagers;
    reusableVillagerArchives = candidateArchives;
  }
} catch {
  reusableVillagerArchives = null;
}

function buildVillagerCharacterArchive(character: Record<string, unknown>) {
  const startledCharacter = { ...character, eyeStyle: "terrified", mouthStyle: "open" };
  const angryCharacter = { ...character, eyeStyle: "angry", mouthStyle: "frown" };
  const entries: Array<{ key: string; bytes: Buffer }> = [];
  const addFrame = (
    key: string,
    frameCharacter: Record<string, unknown>,
    animation: string,
    direction: number,
    frame: number,
    isBlinking = false,
  ) => {
    const texture = avatarFactory.createAvatarTexture(
      frameCharacter,
      animation,
      direction,
      frame,
      false,
      isBlinking,
    );
    entries.push({ key, bytes: (texture.image as Canvas).toBuffer("image/png") });
  };

  for (let direction = 0; direction < 8; direction += 1) {
    addFrame(`idle/d${direction}`, character, "idle", direction, 0);
  }
  for (const direction of [0, 1, 2, 6, 7]) {
    addFrame(`idle-blink/d${direction}`, character, "idle", direction, 0, true);
  }
  for (let direction = 0; direction < 8; direction += 1) {
    addFrame(`startled/d${direction}/f0`, startledCharacter, "startled", direction, 0);
    addFrame(`startled/d${direction}/f1`, startledCharacter, "startled", direction, 1);
  }
  for (const direction of [0, 1, 2, 6, 7]) {
    addFrame(`startled-blink/d${direction}/f0`, startledCharacter, "startled", direction, 0, true);
    addFrame(`startled-blink/d${direction}/f1`, startledCharacter, "startled", direction, 1, true);
  }
  for (let direction = 0; direction < 8; direction += 1) {
    addFrame(`angry/d${direction}`, angryCharacter, "angry", direction, 0);
  }
  for (const direction of [0, 1, 2, 6, 7]) {
    addFrame(`angry-blink/d${direction}`, angryCharacter, "angry", direction, 0, true);
  }

  if (entries.length !== 52) {
    throw new Error(`Expected 52 villager frames; generated ${entries.length}.`);
  }
  return buildVillagerFrameArchive(entries);
}

if (reusableVillagerArchives) {
  for (const archive of reusableVillagerArchives) {
    recordStreamingBytes(archive.file, archive.bytes);
  }
} else {
for (let index = 0; index < baseVillageHuts.length; index += 1) {
  const villager = villagerCharacterRuntime.makeVillager(baseVillageHuts[index], index);
  const archive = buildVillagerCharacterArchive(villager.character);
  const archiveFile = `${villager.id}.wofavatar`;
  await emitStreamingBytes(archiveFile, archive);
  bakedVillagers.push({
    id: villager.id,
    index,
    archiveFile,
    archiveBytes: archive.length,
    archiveSha256: sha256(archive),
    x: villager.x,
    y: villager.y,
    z: villager.z,
    baseYaw: villager.baseYaw,
    lookUpdateDesktopMs: 140 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
    lookUpdateMobileMs: 220 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
    hut: {
      x: villager.hut.x,
      y: villager.hut.y,
      z: villager.hut.z,
      hutType: villager.hut.hutType,
      isMushroom: villager.hut.isMushroom,
      rotation: villager.hut.rotation,
      interiorWidth: villager.hut.interiorWidth ?? 0,
      interiorDepth: villager.hut.interiorDepth ?? 0,
      interiorHeight: villager.hut.interiorHeight ?? 0,
    },
    character: villager.character,
  });
}
}

const darrelArchiveFile = "-64--48-darrel.wofavatar";
const darrelArchive = buildVillagerCharacterArchive(villagerCharacterRuntime.DARREL_CHARACTER);
await emitStreamingBytes(darrelArchiveFile, darrelArchive);

const villagerLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "getHutList().map(makeVillager)",
  sourceSignature: villagerSourceSignature,
  count: bakedVillagers.length,
  renderDistanceDesktop: 90,
  renderDistanceMobile: 58,
  visibilityUpdateMs: 350,
  runtimeTickMs: 50,
  eyeLockRadius: 18,
  avatarWorldHeight: avatarFactory.AVATAR_WORLD_HEIGHT,
  avatarWorldWidth: avatarFactory.AVATAR_WORLD_WIDTH,
  avatarWorldCenterY: avatarFactory.AVATAR_WORLD_CENTER_Y,
  avatarScale: avatarFactory.NPC_AVATAR_SCALE,
  avatarGroundLift: avatarFactory.NPC_AVATAR_GROUND_LIFT,
  darrelArchiveFile,
  darrelArchiveBytes: darrelArchive.length,
  darrelArchiveSha256: sha256(darrelArchive),
  frameContract: {
    idleDirections: 8,
    blinkDirections: [0, 1, 2, 6, 7],
    startledDirections: 8,
    startledUniqueFrames: 2,
    angryDirections: 8,
    reactionBlinkDirections: [0, 1, 2, 6, 7],
    archiveEntriesPerVillager: 52,
  },
  villagers: bakedVillagers,
}, null, 2)}\n`, "utf8");
await emitBytes("Villagers/base-village.json", villagerLayoutBytes);

const desertChunkCx = 4;
const desertChunkCz = -4;
const desertChunk = {
  key: `${desertChunkCx}:${desertChunkCz}`,
  cx: desertChunkCx,
  cz: desertChunkCz,
  x: desertChunkCx * 512,
  z: desertChunkCz * 512,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(desertChunkCx, desertChunkCz),
  hasVillage: true,
  villageKind: "desert",
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(desertChunkCx, desertChunkCz),
  riverVertical: survivalMath.survivalHash01(desertChunkCx, desertChunkCz, 5) > 0.5,
  lod: "near",
};
const desertBaseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(desertChunk);
const desertLayout = desertVillageRuntime.makeDesertVillageLayout(
  desertChunk,
  desertBaseHeight,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
);
const expectedDesertCounts = {
  buildings: 55,
  huts: 55,
  wallSegments: 52,
  marketStalls: 10,
  palms: 22,
  ladders: 37,
  fences: 41,
  clothesLines: 15,
  streetProps: 94,
};
for (const [key, expected] of Object.entries(expectedDesertCounts)) {
  const actual = desertLayout[key]?.length;
  if (actual !== expected) {
    throw new Error(`Expected ${expected} exact desert ${key}; found ${actual}.`);
  }
}
if (desertChunk.biome !== "desert" || Math.abs(desertBaseHeight - 17.885722662941443) > 1e-9) {
  throw new Error(`Unexpected exact desert chunk contract: biome=${desertChunk.biome} baseHeight=${desertBaseHeight}.`);
}

const desertSourceSignature = sha256(Buffer.concat([
  Buffer.from("react-desert-village-v1-55-villagers-52-frames", "utf8"),
  await readFile(avatarFactoryPath),
  await readFile(villagerCharacterRuntimePath),
  await readFile(desertVillageRuntimePath),
  await readFile(desertVillageTerrainPath),
  await readFile(survivalTerrainSurfacePath),
  await readFile(survivalBiomePath),
  await readFile(survivalRiversPath),
  await readFile(survivalMathPath),
]));
const existingDesertLayoutPath = path.join(outputRoot, "DesertVillage", "runtime-layout.json");
let bakedDesertVillagers: Array<Record<string, unknown>> = [];
let reusableDesertArchives: Array<{ file: string; bytes: Buffer }> | null = null;
try {
  const cachedDocument = JSON.parse(await readFile(existingDesertLayoutPath, "utf8")) as {
    sourceSignature?: string;
    counts?: { villagers?: number };
    villagers?: Array<Record<string, unknown> & { archiveFile?: string; archiveSha256?: string }>;
  };
  if (
    cachedDocument.sourceSignature === desertSourceSignature &&
    cachedDocument.counts?.villagers === expectedDesertCounts.huts &&
    cachedDocument.villagers?.length === expectedDesertCounts.huts
  ) {
    const candidateArchives: Array<{ file: string; bytes: Buffer }> = [];
    for (const record of cachedDocument.villagers) {
      if (!record.archiveFile || !record.archiveSha256) throw new Error("Cached desert villager record is incomplete.");
      const bytes = await readFile(path.join(streamingAssetsRoot, record.archiveFile));
      if (sha256(bytes) !== record.archiveSha256) throw new Error(`Cached desert archive hash mismatch: ${record.archiveFile}`);
      candidateArchives.push({ file: record.archiveFile, bytes });
    }
    bakedDesertVillagers = cachedDocument.villagers;
    reusableDesertArchives = candidateArchives;
  }
} catch {
  reusableDesertArchives = null;
}

if (reusableDesertArchives) {
  for (const archive of reusableDesertArchives) {
    recordStreamingBytes(archive.file, archive.bytes);
  }
} else {
  for (let index = 0; index < desertLayout.huts.length; index += 1) {
    const villager = villagerCharacterRuntime.makeVillager(desertLayout.huts[index], index);
    const archive = buildVillagerCharacterArchive(villager.character);
    const archiveFile = `desert-${index.toString().padStart(2, "0")}.wofavatar`;
    await emitStreamingBytes(archiveFile, archive);
    bakedDesertVillagers.push({
      id: villager.id,
      index,
      displayName: `Town Villager ${index + 1}`,
      townId: `survival-desert-villagers-${desertChunk.key}`,
      archiveFile,
      archiveBytes: archive.length,
      archiveSha256: sha256(archive),
      x: villager.x,
      y: villager.y,
      z: villager.z,
      baseYaw: villager.baseYaw,
      lookUpdateDesktopMs: 140 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      lookUpdateMobileMs: 220 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      hut: {
        x: villager.hut.x,
        y: villager.hut.y,
        z: villager.hut.z,
        hutType: villager.hut.hutType,
        isMushroom: villager.hut.isMushroom,
        rotation: villager.hut.rotation,
        interiorWidth: villager.hut.interiorWidth ?? 0,
        interiorDepth: villager.hut.interiorDepth ?? 0,
        interiorHeight: villager.hut.interiorHeight ?? 0,
      },
      character: villager.character,
    });
  }
}

const desertPadGeometry = survivalTerrainSurface.makeSurvivalVillagePadGeometry(desertChunk);
const serializedDesertPadGeometry = serializeThreeGeometry(desertPadGeometry);
desertPadGeometry.dispose();
const desertSurfaceGeometryDefinitions = [
  ["northSouthRoad", 48, 508, 0, 0.18, 0],
  ["eastWestRoad", 48, 508, Math.PI / 2, 0.18, 0],
  ["diagonalRoadA", 26, 360, Math.PI / 4, 0.17, 0],
  ["diagonalRoadB", 26, 360, -Math.PI / 4, 0.17, 0],
  ["northSouthLeft", 7, 508, 0, 0.22, -30],
  ["northSouthRight", 7, 508, 0, 0.22, 30],
  ["eastWestLeft", 7, 508, Math.PI / 2, 0.22, -30],
  ["eastWestRight", 7, 508, Math.PI / 2, 0.22, 30],
  ["diagonalALeft", 5, 360, Math.PI / 4, 0.2, -18],
  ["diagonalARight", 5, 360, Math.PI / 4, 0.2, 18],
  ["diagonalBLeft", 5, 360, -Math.PI / 4, 0.2, -18],
  ["diagonalBRight", 5, 360, -Math.PI / 4, 0.2, 18],
] as const;
const desertSurfaceGeometries: Record<string, ReturnType<typeof serializeThreeGeometry>> = {};
for (const [key, width, length, rotation, yOffset, lateralOffset] of desertSurfaceGeometryDefinitions) {
  const geometry = makeExactDesertSurfaceStripGeometry(
    desertChunk,
    width,
    length,
    rotation,
    yOffset,
    lateralOffset,
  );
  desertSurfaceGeometries[key] = serializeThreeGeometry(geometry);
  geometry.dispose();
}
const desertLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "survivalDesertVillageRuntime.makeDesertVillageLayout(4,-4,near)",
  sourceSignature: desertSourceSignature,
  chunk: desertChunk,
  baseHeight: desertBaseHeight,
  counts: {
    ...expectedDesertCounts,
    villagers: bakedDesertVillagers.length,
  },
  layout: desertLayout,
  villagers: bakedDesertVillagers,
  padGeometry: serializedDesertPadGeometry,
  surfaceGeometries: desertSurfaceGeometries,
}, null, 2)}\n`, "utf8");
await emitBytes("DesertVillage/runtime-layout.json", desertLayoutBytes);
await emitCanvas(
  "DesertVillage/Textures/desert-sand.png",
  survivalTerrainTextures.getDesertSandTexture().image as Canvas,
);
await emitCanvas(
  "DesertVillage/Textures/desert-adobe-wall.png",
  survivalTerrainTextures.getDesertAdobeWallTexture().image as Canvas,
);

const chicagoChunkCx = -3;
const chicagoChunkCz = -3;
const chicagoChunk = {
  key: `${chicagoChunkCx}:${chicagoChunkCz}`,
  cx: chicagoChunkCx,
  cz: chicagoChunkCz,
  x: chicagoChunkCx * 512,
  z: chicagoChunkCz * 512,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(chicagoChunkCx, chicagoChunkCz),
  hasVillage: true,
  villageKind: "chicago",
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(chicagoChunkCx, chicagoChunkCz),
  riverVertical: survivalMath.survivalHash01(chicagoChunkCx, chicagoChunkCz, 5) > 0.5,
  lod: "near",
};
const chicagoBaseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(chicagoChunk);
const chicagoLayout = chicagoCityLayout.makeChicagoLayout(chicagoChunk);
const chicagoStreet = {
  trafficLightIntersections: chicagoCityLayout.makeChicagoTrafficLightIntersections(),
  lamps: chicagoCityLayout.makeChicagoLampLayout(),
  streetTrees: chicagoCityLayout.makeChicagoStreetTreeLayout(),
  sidewalkSegments: chicagoCityLayout.makeChicagoSidewalkSegments(),
  hydrants: chicagoCityStreetRuntime.makeChicagoHydrantLayout(),
  trashCans: chicagoCityStreetRuntime.makeChicagoTrashCanLayout(),
  benches: chicagoCityStreetRuntime.makeChicagoBenchLayout(),
  grassPatches: chicagoCityStreetRuntime.makeChicagoGrassPatches(),
  crosswalks: chicagoCityStreetRuntime.makeChicagoCrosswalkStripes(),
  sidewalkPlanes: [] as Array<Record<string, unknown>>,
  parkingLines: chicagoCityStreetRuntime.makeChicagoParkingLines(),
};
chicagoStreet.sidewalkPlanes = chicagoCityStreetRuntime.makeChicagoSidewalkPlanes(chicagoStreet.sidewalkSegments);
const expectedChicagoCounts = {
  buildings: 35,
  operators: 35,
  pedestrians: 220,
  cars: 46,
  trafficLightIntersections: 16,
  lamps: 48,
  streetTrees: 40,
  sidewalkSegments: 5,
  hydrants: 16,
  trashCans: 36,
  benches: 34,
  grassPatches: 40,
  crosswalks: 576,
  sidewalkPlanes: 80,
  parkingLines: 64,
};
const chicagoActualCounts: Record<string, number> = {
  buildings: chicagoLayout.buildings.length,
  operators: chicagoLayout.buildings.filter((building: { enterable: boolean }) => building.enterable).length,
  pedestrians: chicagoLayout.pedestrians.length,
  cars: chicagoLayout.cars.length,
  trafficLightIntersections: chicagoStreet.trafficLightIntersections.length,
  lamps: chicagoStreet.lamps.length,
  streetTrees: chicagoStreet.streetTrees.length,
  sidewalkSegments: chicagoStreet.sidewalkSegments.length,
  hydrants: chicagoStreet.hydrants.length,
  trashCans: chicagoStreet.trashCans.length,
  benches: chicagoStreet.benches.length,
  grassPatches: chicagoStreet.grassPatches.length,
  crosswalks: chicagoStreet.crosswalks.length,
  sidewalkPlanes: chicagoStreet.sidewalkPlanes.length,
  parkingLines: chicagoStreet.parkingLines.length,
};
for (const [key, expected] of Object.entries(expectedChicagoCounts)) {
  const actual = chicagoActualCounts[key];
  if (actual !== expected) {
    throw new Error(`Expected ${expected} exact Chicago ${key}; found ${actual}.`);
  }
}
if (
  chicagoChunk.biome !== "jungle" || chicagoChunk.hasRiver ||
  Math.abs(chicagoBaseHeight - 21.912045982731858) > 1e-9
) {
  throw new Error(
    `Unexpected exact Chicago chunk contract: biome=${chicagoChunk.biome} ` +
    `hasRiver=${chicagoChunk.hasRiver} baseHeight=${chicagoBaseHeight}.`,
  );
}

const chicagoSourceSignature = sha256(Buffer.concat([
  Buffer.from("react-chicago-city-v1-exact-near-chunk", "utf8"),
  await readFile(avatarFactoryPath),
  await readFile(chicagoCityLayoutPath),
  await readFile(chicagoCityRenderingPath),
  await readFile(chicagoCityCollidersPath),
  await readFile(chicagoCityStreetRuntimePath),
  await readFile(chicagoCityTrafficRuntimePath),
  await readFile(chicagoCityTexturesPath),
  await readFile(survivalTerrainSurfacePath),
  await readFile(survivalBiomePath),
  await readFile(survivalRiversPath),
  await readFile(survivalMathPath),
]));
const chicagoOperators: Array<Record<string, unknown>> = [];
for (let index = 0; index < chicagoLayout.buildings.length; index += 1) {
  const building = chicagoLayout.buildings[index];
  if (!building.enterable) continue;
  const spritePath = `ChicagoCity/Operators/operator-${index.toString().padStart(2, "0")}.png`;
  const texture = avatarFactory.createAvatarTexture(
    building.operatorCharacter,
    "idle",
    0,
    0,
  );
  await emitCanvas(spritePath, texture.image as Canvas);
  chicagoOperators.push({
    index,
    buildingKey: building.key,
    spritePath,
    character: building.operatorCharacter,
  });
}
if (chicagoOperators.length !== expectedChicagoCounts.operators) {
  throw new Error(`Expected ${expectedChicagoCounts.operators} exact Chicago operators; found ${chicagoOperators.length}.`);
}

const initialCarTransforms = chicagoLayout.cars.map((car: Record<string, unknown>) => {
  const target = { x: 0, z: 0, yaw: 0 };
  chicagoCityTrafficRuntime.writeChicagoVehicleTransform(target, car, 0);
  return target;
});
const initialPedestrianTransforms = chicagoLayout.pedestrians.map((pedestrian: Record<string, unknown>) =>
  chicagoCityTrafficRuntime.getChicagoPedestrianTransform(pedestrian, 0));
const chicagoPadGeometry = survivalTerrainSurface.makeSurvivalVillagePadGeometry(chicagoChunk);
const serializedChicagoPadGeometry = serializeThreeGeometry(chicagoPadGeometry);
chicagoPadGeometry.dispose();
const chicagoLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "survivalChicagoCityLayout.makeChicagoLayout(-3,-3,near)",
  sourceSignature: chicagoSourceSignature,
  chunk: chicagoChunk,
  baseHeight: chicagoBaseHeight,
  counts: chicagoActualCounts,
  constants: {
    cityHalfSize: chicagoCityLayout.CHICAGO_CITY_HALF_SIZE,
    roadPositions: chicagoCityLayout.CHICAGO_ROAD_POSITIONS,
    beanParkX: chicagoCityLayout.CHICAGO_BEAN_PARK_X,
    beanParkZ: chicagoCityLayout.CHICAGO_BEAN_PARK_Z,
    ledSignUpdateIntervalSeconds: chicagoCityLayout.CHICAGO_LED_SIGN_UPDATE_INTERVAL_SECONDS,
    trafficUpdateIntervalSeconds: chicagoCityLayout.CHICAGO_TRAFFIC_UPDATE_INTERVAL_SECONDS,
    pedestrianUpdateIntervalSeconds: chicagoCityLayout.CHICAGO_PEDESTRIAN_UPDATE_INTERVAL_SECONDS,
  },
  textureContract: {
    windowRepeat: [1.35, 5.6],
    facadeRepeats: [[0.56, 1.7], [0.56, 1.7], [0.5, 1.7], [0.56, 1.95], [0.44, 1.45], [0.56, 1.7]],
  },
  layout: chicagoLayout,
  street: chicagoStreet,
  operators: chicagoOperators,
  initialTraffic: {
    cars: initialCarTransforms,
    pedestrians: initialPedestrianTransforms,
  },
  padGeometry: serializedChicagoPadGeometry,
}, null, 2)}\n`, "utf8");
await emitBytes("ChicagoCity/runtime-layout.json", chicagoLayoutBytes);
await emitCanvas(
  "ChicagoCity/Textures/window.png",
  chicagoCityTextures.getChicagoWindowTexture().image as Canvas,
);
const chicagoFacadeTextures = chicagoCityTextures.getChicagoFacadeTextures();
for (let index = 0; index < chicagoFacadeTextures.length; index += 1) {
  await emitCanvas(
    `ChicagoCity/Textures/facade-${index}.png`,
    chicagoFacadeTextures[index].image as Canvas,
  );
}
await emitCanvas(
  "ChicagoCity/Textures/chicago-sign.png",
  chicagoCityTextures.getChicagoSignTexture().image as Canvas,
);
await emitCanvas(
  "ChicagoCity/Textures/led-sign.png",
  chicagoCityTextures.getChicagoLedSignTexture().image as Canvas,
);
const chicagoStoreSignTextures = chicagoCityTextures.getChicagoStoreSignTextures();
for (let index = 0; index < chicagoStoreSignTextures.length; index += 1) {
  await emitCanvas(
    `ChicagoCity/Textures/store-sign-${index}.png`,
    chicagoStoreSignTextures[index].image as Canvas,
  );
}
const chicagoAdTextures = chicagoCityTextures.getChicagoAdTextures();
for (let index = 0; index < chicagoAdTextures.length; index += 1) {
  await emitCanvas(
    `ChicagoCity/Textures/ad-${index}.png`,
    chicagoAdTextures[index].image as Canvas,
  );
}

const swampChunkCx = 0;
const swampChunkCz = -3;
const swampChunk = {
  key: `${swampChunkCx}:${swampChunkCz}`,
  cx: swampChunkCx,
  cz: swampChunkCz,
  x: swampChunkCx * 512,
  z: swampChunkCz * 512,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(swampChunkCx, swampChunkCz),
  hasVillage: true,
  villageKind: "swamp",
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(swampChunkCx, swampChunkCz),
  riverVertical: survivalMath.survivalHash01(swampChunkCx, swampChunkCz, 5) > 0.5,
  lod: "near",
};
const swampBaseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(swampChunk);
const swampLayout = swampVillageRuntime.makeSwampVillageLayout(
  swampChunk,
  swampBaseHeight,
  survivalBiome.getSurvivalWaterLevelAtWorld,
);
const swampRopeSegments = swampVillageRuntime.getSwampVillageRopeLightSegments(swampLayout.ropes).map(
  (segment: { key: string; position: number[]; quaternion: InstanceType<typeof THREE.Quaternion>; length: number }) => ({
    key: segment.key,
    position: segment.position,
    quaternion: [segment.quaternion.x, segment.quaternion.y, segment.quaternion.z, segment.quaternion.w],
    length: segment.length,
  }),
);
const swampRopeBulbs = swampVillageRuntime.getSwampVillageRopeLightBulbs(swampLayout.ropes);
const expectedSwampCounts = {
  huts: 13,
  hutInfos: 13,
  walkways: 17,
  ramps: 4,
  lilyPads: 28,
  stumps: 18,
  reeds: 36,
  ropes: 13,
  ropeSegments: 91,
  ropeBulbs: 39,
  pointLights: 3,
};
const swampActualCounts: Record<string, number> = {
  huts: swampLayout.huts.length,
  hutInfos: swampLayout.hutInfos.length,
  walkways: swampLayout.walkways.length,
  ramps: swampLayout.ramps.length,
  lilyPads: swampLayout.lilyPads.length,
  stumps: swampLayout.stumps.length,
  reeds: swampLayout.reeds.length,
  ropes: swampLayout.ropes.length,
  ropeSegments: swampRopeSegments.length,
  ropeBulbs: swampRopeBulbs.length,
  pointLights: swampRopeBulbs.filter((bulb: { hasPointLight: boolean }) => bulb.hasPointLight).length,
};
for (const [key, expected] of Object.entries(expectedSwampCounts)) {
  const actual = swampActualCounts[key];
  if (actual !== expected) {
    throw new Error(`Expected ${expected} exact swamp ${key}; found ${actual}.`);
  }
}
if (
  swampChunk.biome !== "swamp" || !swampChunk.hasRiver || swampChunk.riverVertical ||
  Math.abs(swampBaseHeight - 2.7529895363497836) > 1e-9 ||
  Math.abs(swampLayout.waterY - 3.1729895363497835) > 1e-9 ||
  Math.abs(swampLayout.platformY - 9.072989536349784) > 1e-9
) {
  throw new Error(
    `Unexpected exact swamp chunk contract: biome=${swampChunk.biome} hasRiver=${swampChunk.hasRiver} ` +
    `riverVertical=${swampChunk.riverVertical} baseHeight=${swampBaseHeight} waterY=${swampLayout.waterY} ` +
    `platformY=${swampLayout.platformY}.`,
  );
}

const swampToadManifestBytes = await readFile(swampToadManifestPath);
const swampToadManifest = JSON.parse(swampToadManifestBytes.toString("utf8")) as {
  source: string;
  frameSize: [number, number];
  idleFrameMs: number;
  yawnFrameMs: number;
  idle: string[];
  yawn: string[];
  sleep: string;
};
if (
  swampToadManifest.frameSize[0] !== 288 || swampToadManifest.frameSize[1] !== 187 ||
  swampToadManifest.idle.length !== 28 || swampToadManifest.yawn.length !== 12 ||
  swampToadManifest.idleFrameMs !== 200 || swampToadManifest.yawnFrameMs !== 120
) {
  throw new Error("Unexpected exact swamp toad animation manifest contract.");
}
const swampToadSources = [...swampToadManifest.idle, ...swampToadManifest.yawn, swampToadManifest.sleep];
const swampToadFrames: Array<{ source: string; output: string; bytes: Buffer }> = [];
for (const source of swampToadSources) {
  const fileName = path.basename(source);
  const output = `SwampVillage/Toad/${fileName}`;
  const bytes = await readFile(path.join(swampToadSourceRoot, fileName));
  swampToadFrames.push({ source, output, bytes });
  await emitBytes(output, bytes);
}
await emitBytes("SwampVillage/Toad/manifest.json", swampToadManifestBytes);
await emitCanvas("SwampVillage/Toad/sleep-z.png", makeExactSwampToadSleepZCanvas());

const swampSourceSignature = sha256(Buffer.concat([
  Buffer.from("react-swamp-village-v1-exact-near-chunk", "utf8"),
  await readFile(avatarFactoryPath),
  await readFile(villagerCharacterRuntimePath),
  await readFile(swampVillageRuntimePath),
  await readFile(swampVillageRenderingPath),
  await readFile(swampVillageTerrainPath),
  await readFile(survivalTerrainSurfacePath),
  await readFile(survivalBiomePath),
  await readFile(survivalRiversPath),
  await readFile(survivalMathPath),
  await readFile(survivalTerrainTexturesPath),
  swampToadManifestBytes,
  ...swampToadFrames.map((frame) => frame.bytes),
]));
const existingSwampLayoutPath = path.join(outputRoot, "SwampVillage", "runtime-layout.json");
let bakedSwampVillagers: Array<Record<string, unknown>> = [];
let reusableSwampArchives: Array<{ file: string; bytes: Buffer }> | null = null;
try {
  const cachedDocument = JSON.parse(await readFile(existingSwampLayoutPath, "utf8")) as {
    sourceSignature?: string;
    counts?: { villagers?: number };
    villagers?: Array<Record<string, unknown> & { archiveFile?: string; archiveSha256?: string }>;
  };
  if (
    cachedDocument.sourceSignature === swampSourceSignature &&
    cachedDocument.counts?.villagers === expectedSwampCounts.hutInfos &&
    cachedDocument.villagers?.length === expectedSwampCounts.hutInfos
  ) {
    const candidateArchives: Array<{ file: string; bytes: Buffer }> = [];
    for (const record of cachedDocument.villagers) {
      if (!record.archiveFile || !record.archiveSha256) throw new Error("Cached swamp villager record is incomplete.");
      const bytes = await readFile(path.join(streamingAssetsRoot, record.archiveFile));
      if (sha256(bytes) !== record.archiveSha256) throw new Error(`Cached swamp archive hash mismatch: ${record.archiveFile}`);
      candidateArchives.push({ file: record.archiveFile, bytes });
    }
    bakedSwampVillagers = cachedDocument.villagers;
    reusableSwampArchives = candidateArchives;
  }
} catch {
  reusableSwampArchives = null;
}

if (reusableSwampArchives) {
  for (const archive of reusableSwampArchives) recordStreamingBytes(archive.file, archive.bytes);
} else {
  for (let index = 0; index < swampLayout.hutInfos.length; index += 1) {
    const villager = villagerCharacterRuntime.makeVillager(swampLayout.hutInfos[index], index);
    const archive = buildVillagerCharacterArchive(villager.character);
    const archiveFile = `swamp-${index.toString().padStart(2, "0")}.wofavatar`;
    await emitStreamingBytes(archiveFile, archive);
    bakedSwampVillagers.push({
      id: villager.id,
      index,
      displayName: `Town Villager ${index + 1}`,
      townId: `survival-swamp-villagers-${swampChunk.key}`,
      archiveFile,
      archiveBytes: archive.length,
      archiveSha256: sha256(archive),
      x: villager.x,
      y: villager.y,
      z: villager.z,
      baseYaw: villager.baseYaw,
      lookUpdateDesktopMs: 140 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      lookUpdateMobileMs: 220 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      hut: {
        x: villager.hut.x,
        y: villager.hut.y,
        z: villager.hut.z,
        hutType: villager.hut.hutType,
        isMushroom: villager.hut.isMushroom,
        rotation: villager.hut.rotation,
        interiorWidth: villager.hut.interiorWidth ?? 0,
        interiorDepth: villager.hut.interiorDepth ?? 0,
        interiorHeight: villager.hut.interiorHeight ?? 0,
      },
      character: villager.character,
    });
  }
}
if (bakedSwampVillagers.length !== expectedSwampCounts.hutInfos) {
  throw new Error(`Expected ${expectedSwampCounts.hutInfos} exact swamp villagers; found ${bakedSwampVillagers.length}.`);
}

const swampPadGeometry = survivalTerrainSurface.makeSurvivalVillagePadGeometry(swampChunk);
const serializedSwampPadGeometry = serializeThreeGeometry(swampPadGeometry);
swampPadGeometry.dispose();
const swampLilyPadGeometry = makeExactSwampLilyPadGeometry();
const serializedSwampLilyPadGeometry = serializeThreeGeometry(swampLilyPadGeometry);
swampLilyPadGeometry.dispose();
const toadOutputForSource = new Map(swampToadFrames.map((frame) => [frame.source, frame.output]));
const swampLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "survivalSwampVillageRuntime.makeSwampVillageLayout(0,-3,near)",
  sourceSignature: swampSourceSignature,
  chunk: swampChunk,
  baseHeight: swampBaseHeight,
  counts: {
    ...swampActualCounts,
    villagers: bakedSwampVillagers.length,
  },
  constants: {
    villageRadius: 214,
    platformSize: 76,
    toadUpdateIntervalSeconds: 1 / 24,
  },
  layout: swampLayout,
  ropeSegments: swampRopeSegments,
  ropeBulbs: swampRopeBulbs,
  villagers: bakedSwampVillagers,
  toad: {
    source: swampToadManifest.source,
    frameSize: swampToadManifest.frameSize,
    idleFrameMs: swampToadManifest.idleFrameMs,
    yawnFrameMs: swampToadManifest.yawnFrameMs,
    idle: swampToadManifest.idle.map((source) => toadOutputForSource.get(source)),
    yawn: swampToadManifest.yawn.map((source) => toadOutputForSource.get(source)),
    sleep: toadOutputForSource.get(swampToadManifest.sleep),
    sleepZ: "SwampVillage/Toad/sleep-z.png",
  },
  padGeometry: serializedSwampPadGeometry,
  lilyPadGeometry: serializedSwampLilyPadGeometry,
}, null, 2)}\n`, "utf8");
await emitBytes("SwampVillage/runtime-layout.json", swampLayoutBytes);
await emitCanvas(
  "SwampVillage/Textures/terrain-detail.png",
  survivalTerrainTextures.getSurvivalTerrainDetailTexture().image as Canvas,
);

const mountainChunkCx = 3;
const mountainChunkCz = 0;
const mountainChunk = {
  key: `${mountainChunkCx}:${mountainChunkCz}`,
  cx: mountainChunkCx,
  cz: mountainChunkCz,
  x: mountainChunkCx * 512,
  z: mountainChunkCz * 512,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(mountainChunkCx, mountainChunkCz),
  hasVillage: true,
  villageKind: "mountain",
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(mountainChunkCx, mountainChunkCz),
  riverVertical: survivalMath.survivalHash01(mountainChunkCx, mountainChunkCz, 5) > 0.5,
  lod: "near",
};
const mountainBaseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(mountainChunk);
const mountainLayout = mountainVillageSceneLayout.makeMountainVillageLayout(
  mountainChunk,
  mountainBaseHeight,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  { includeMineshaftLayout: true, includeVillagerHutInfos: true },
);
const mountainExpectedCounts = {
  trailPoints: 25,
  trailSegments: 24,
  cliffPatches: 48,
  cabins: 8,
  interiorHuts: 3,
  interiorLadders: 4,
  hutInfos: 11,
  slopeGrassTufts: 1793,
  summitSnowDrifts: 28,
  rimBeams: 12,
  supportFrames: 4,
  supportPosts: 8,
  supportSnowCaps: 8,
  bottomRocks: 14,
  wallLanterns: 9,
  wallPaintings: 6,
  wallRopeLights: 20,
  banquetBottomLights: 8,
  banquetChairs: 7,
  banquetTablePlanks: 9,
  banquetTableLegs: 6,
  banquetBreads: 4,
  banquetFruitBowls: 4,
  banquetFruits: 20,
  banquetPlates: 8,
  banquetCandles: 2,
};
const mountainBaseActualCounts: Record<string, number> = {
  trailPoints: mountainLayout.trailPoints.length,
  trailSegments: mountainLayout.trailSegments.length,
  cliffPatches: mountainLayout.cliffPatches.length,
  cabins: mountainLayout.cabins.length,
  interiorHuts: mountainLayout.interiorHuts.length,
  interiorLadders: mountainLayout.interiorLadders.length,
  hutInfos: mountainLayout.hutInfos.length,
};
for (const [key, expected] of Object.entries(mountainExpectedCounts).slice(0, 7)) {
  const actual = mountainBaseActualCounts[key];
  if (actual !== expected) {
    throw new Error(`Expected ${expected} exact mountain ${key}; found ${actual}.`);
  }
}
if (
  mountainChunk.biome !== "mushroom" || mountainChunk.hasRiver ||
  Math.abs(mountainBaseHeight - 3.364967894227928) > 1e-9 ||
  Math.abs(mountainLayout.summitY - 217.54496789422794) > 1e-9
) {
  throw new Error(
    `Unexpected exact mountain chunk contract: biome=${mountainChunk.biome} hasRiver=${mountainChunk.hasRiver} ` +
    `baseHeight=${mountainBaseHeight} summitY=${mountainLayout.summitY}.`,
  );
}

const mountainSlopeGrassTufts = mountainSlopeGrassRuntime.makeMountainVillageSlopeGrassTufts(
  mountainChunk,
  mountainBaseHeight,
  (
    sampleChunk: typeof mountainChunk,
    sampleLocalX: number,
    sampleLocalZ: number,
    sampleBaseHeight: number,
  ) => mountainVillageTerrain.getMountainVillageHeight(
    sampleChunk,
    sampleLocalX,
    sampleLocalZ,
    survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
    sampleBaseHeight,
  ),
  (
    sampleChunk: typeof mountainChunk,
    sampleLocalX: number,
    sampleLocalZ: number,
    sampleY: number,
    sampleBaseHeight: number,
    showTrailSurface: boolean,
  ) => mountainVillageTerrainGeometry.getMountainVillageTerrainColorInto(
    new THREE.Color(),
    sampleChunk,
    sampleLocalX,
    sampleLocalZ,
    sampleY,
    sampleBaseHeight,
    showTrailSurface,
    survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
    survivalTerrainSurface.getSurvivalSmoothedTerrainColor,
  ),
);
if (mountainSlopeGrassTufts.length !== mountainExpectedCounts.slopeGrassTufts) {
  throw new Error(
    `Expected ${mountainExpectedCounts.slopeGrassTufts} exact mountain slope-grass tufts; ` +
    `found ${mountainSlopeGrassTufts.length}.`,
  );
}

const mountainOpening = {
  summitSnowDrifts: mountainVillageMineshaftOpeningRuntime.getMountainMineshaftSummitSnowDrifts(),
  rimBeams: mountainVillageMineshaftOpeningRuntime.getMountainMineshaftRimBeams({
    count: 12,
    holeRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_HOLE_RADIUS,
    outerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_RIM_OUTER_RADIUS,
  }),
  supportFrames: mountainVillageMineshaftOpeningRuntime.getMountainMineshaftSupportFrames({ count: 4 }),
  bottomRocks: mountainVillageMineshaftOpeningRuntime.getMountainMineshaftBottomRocks({
    count: 14,
    bottomRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_BOTTOM_RADIUS,
  }),
};
const mountainBottomY = mountainBaseHeight + mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_BOTTOM_BASE_OFFSET;
const mountainWallDecor = mountainVillageMineshaftWallDecorRuntime.getMountainMineshaftWallDecorDescriptors({
  bottomY: mountainBottomY,
  summitY: mountainLayout.summitY,
});
const mountainBanquet = mountainVillageMineshaftBanquetRuntime.getMountainMineshaftRoyalBanquetDescriptors();
const mountainBanquetColliders = mountainVillageMineshaftBanquetRuntime.getMountainMineshaftBanquetColliderDetails();
const mountainCatwalk = mountainVillageMineshaftCatwalkRuntime.getMountainMineshaftCatwalkDescriptors({
  segments: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_SEGMENTS,
  innerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_INNER_RADIUS,
  outerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_OUTER_RADIUS,
});
const mountainCatwalkColliders = mountainVillageMineshaftCatwalkRuntime.getMountainMineshaftCatwalkColliderDetails({
  segments: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_SEGMENTS,
  innerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_INNER_RADIUS,
  outerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_OUTER_RADIUS,
});
const absoluteAngleDeltaRadians = (left: number, right: number) => {
  const fullTurn = Math.PI * 2;
  return Math.abs((((left - right + Math.PI) % fullTurn) + fullTurn) % fullTurn - Math.PI);
};
const mountainInteriorPlatforms = mountainLayout.interiorHuts.map((hut: Record<string, any>, index: number) => {
  const ladder = mountainLayout.interiorLadders[index] as Record<string, any> | undefined;
  const nextLadder = mountainLayout.interiorLadders[index + 1] as Record<string, any> | undefined;
  const landingLocalX = mountainVillageMineshaftAccessRuntime.getMountainMineshaftLadderLandingLocalX(hut, ladder);
  const pieces = mountainVillageMineshaftAccessRuntime.getMountainMineshaftPlatformPieces(
    hut.platformWidth,
    landingLocalX,
    mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_PLATFORM_GAP,
  );
  const topPieces = mountainVillageMineshaftAccessRuntime.getMountainMineshaftPlatformPieces(
    hut.platformWidth * 0.94,
    landingLocalX,
    mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_PLATFORM_GAP,
  );
  const platformZ = hut.depth / 2 + hut.platformDepth / 2 - 1.1;
  const poleSide: -1 | 1 = landingLocalX !== null && landingLocalX > 0 ? -1 : 1;
  const details = mountainVillageMineshaftAccessRuntime.getMountainMineshaftPlatformDetails({
    platformPieces: pieces,
    platformZ,
    platformDepth: hut.platformDepth,
    platformWidth: hut.platformWidth,
    poleSide,
    plankCount: 5,
  });
  const centerRadius = mountainCatwalk.centerGuardRailRadius;
  const balconyGapHalfAngle = Math.min(0.52, Math.max(0.34, (hut.platformWidth * 0.38) / centerRadius));
  const ladderGapHalfAngle = ladder
    ? Math.min(0.5, Math.max(0.34, (ladder.width * 1.35) / centerRadius))
    : 0;
  const nextLadderGapHalfAngle = nextLadder
    ? Math.min(0.5, Math.max(0.34, (nextLadder.width * 1.35) / centerRadius))
    : 0;
  const isGuardRailOpening = (angle: number) =>
    absoluteAngleDeltaRadians(hut.angle, angle) < balconyGapHalfAngle ||
    Boolean(ladder && absoluteAngleDeltaRadians(ladder.angle, angle) < ladderGapHalfAngle) ||
    Boolean(nextLadder && absoluteAngleDeltaRadians(nextLadder.angle, angle) < nextLadderGapHalfAngle);
  return {
    hutKey: hut.key,
    platformZ,
    landingLocalX,
    poleSide,
    pieces,
    topPieces,
    details,
    catwalkLightPoles: mountainVillageMineshaftCatwalkRuntime.getMountainMineshaftCatwalkLightPoles(
      hut.angle,
      mountainCatwalk.lightPoleRadius,
    ),
    guardRailOpenings: {
      hutAngle: hut.angle,
      ladderAngle: ladder?.angle ?? null,
      nextLadderAngle: nextLadder?.angle ?? null,
      balconyGapHalfAngle,
      ladderGapHalfAngle,
      nextLadderGapHalfAngle,
      visibleEdgeBlockIndices: mountainCatwalk.edgeBlocks
        .filter((entry: { angle: number }) => !isGuardRailOpening(entry.angle))
        .map((entry: { index: number }) => entry.index),
      visibleGuardPostIndices: mountainCatwalk.guardPosts
        .filter((entry: { angle: number }) => !isGuardRailOpening(entry.angle))
        .map((entry: { index: number }) => entry.index),
      visibleRailSegmentIndices: mountainCatwalk.railSegments
        .filter((entry: { angle: number }) => !isGuardRailOpening(entry.angle))
        .map((entry: { index: number }) => entry.index),
    },
  };
});
const mountainLadderDetails = mountainLayout.interiorLadders.map((ladder: Record<string, any>) => ({
  ladderKey: ladder.key,
  height: Math.max(4, ladder.endY - ladder.startY),
  details: mountainVillageMineshaftAccessRuntime.getMountainMineshaftLadderDetails({
    height: Math.max(4, ladder.endY - ladder.startY),
    width: ladder.width,
  }),
}));
const mountainExitLadder = mountainLayout.interiorLadders[mountainLayout.interiorLadders.length - 1];
if (!mountainExitLadder) throw new Error("Exact mountain layout is missing its top exit ladder.");
const mountainExitBridgeFrame = mountainVillageMineshaftAccessRuntime.getMountainMineshaftExitBridgeFrame(mountainExitLadder);
const mountainExitBridge = {
  frame: mountainExitBridgeFrame,
  y: mountainLayout.summitY + mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_EXIT_BRIDGE_Y_OFFSET,
  details: mountainVillageMineshaftAccessRuntime.getMountainMineshaftExitBridgeDetails({
    length: mountainExitBridgeFrame.length,
    width: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_EXIT_BRIDGE_WIDTH,
  }),
};
const mountainWaterfallVisuals = mountainVillageWaterfallRuntime.getMountainWaterfallVisualDescriptors({
  waterfall: mountainLayout.waterfall,
  summitY: mountainLayout.summitY,
});
const mountainRuntimeActualCounts: Record<string, number> = {
  slopeGrassTufts: mountainSlopeGrassTufts.length,
  summitSnowDrifts: mountainOpening.summitSnowDrifts.length,
  rimBeams: mountainOpening.rimBeams.length,
  supportFrames: mountainOpening.supportFrames.length,
  supportPosts: mountainOpening.supportFrames.reduce((sum: number, frame: { posts: unknown[] }) => sum + frame.posts.length, 0),
  supportSnowCaps: mountainOpening.supportFrames.reduce((sum: number, frame: { snowCaps: unknown[] }) => sum + frame.snowCaps.length, 0),
  bottomRocks: mountainOpening.bottomRocks.length,
  wallLanterns: mountainWallDecor.lanterns.length,
  wallPaintings: mountainWallDecor.paintings.length,
  wallRopeLights: mountainWallDecor.ropeLights.length,
  banquetBottomLights: mountainBanquet.bottomLights.length,
  banquetChairs: mountainBanquet.chairs.length,
  banquetTablePlanks: mountainBanquet.table.planks.length,
  banquetTableLegs: mountainBanquet.table.legs.length,
  banquetBreads: mountainBanquet.table.breads.length,
  banquetFruitBowls: mountainBanquet.table.fruitBowls.length,
  banquetFruits: mountainBanquet.table.fruitBowls.reduce(
    (sum: number, bowl: { fruits: unknown[] }) => sum + bowl.fruits.length,
    0,
  ),
  banquetPlates: mountainBanquet.table.plates.length,
  banquetCandles: mountainBanquet.table.candles.length,
};
for (const [key, actual] of Object.entries(mountainRuntimeActualCounts)) {
  const expected = mountainExpectedCounts[key as keyof typeof mountainExpectedCounts];
  if (actual !== expected) {
    throw new Error(`Expected ${expected} exact mountain ${key}; found ${actual}.`);
  }
}

const mountainSourceSignature = sha256(Buffer.concat([
  Buffer.from("react-mountain-village-v1-exact-near-chunk-11-villagers", "utf8"),
  await readFile(avatarFactoryPath),
  await readFile(villagerCharacterRuntimePath),
  await readFile(mountainSlopeGrassRuntimePath),
  ...await Promise.all(mountainVillageSourcePaths.map((sourcePath) => readFile(sourcePath))),
  await readFile(survivalTerrainSurfacePath),
  await readFile(survivalBiomePath),
  await readFile(survivalRiversPath),
  await readFile(survivalMathPath),
  await readFile(survivalTerrainTexturesPath),
]));
const existingMountainLayoutPath = path.join(outputRoot, "MountainVillage", "runtime-layout.json");
let bakedMountainVillagers: Array<Record<string, unknown>> = [];
let reusableMountainArchives: Array<{ file: string; bytes: Buffer }> | null = null;
try {
  const cachedDocument = JSON.parse(await readFile(existingMountainLayoutPath, "utf8")) as {
    sourceSignature?: string;
    counts?: { villagers?: number };
    villagers?: Array<Record<string, unknown> & { archiveFile?: string; archiveSha256?: string }>;
  };
  if (
    cachedDocument.sourceSignature === mountainSourceSignature &&
    cachedDocument.counts?.villagers === mountainExpectedCounts.hutInfos &&
    cachedDocument.villagers?.length === mountainExpectedCounts.hutInfos
  ) {
    const candidateArchives: Array<{ file: string; bytes: Buffer }> = [];
    for (const record of cachedDocument.villagers) {
      if (!record.archiveFile || !record.archiveSha256) throw new Error("Cached mountain villager record is incomplete.");
      const bytes = await readFile(path.join(streamingAssetsRoot, record.archiveFile));
      if (sha256(bytes) !== record.archiveSha256) throw new Error(`Cached archive hash mismatch: ${record.archiveFile}`);
      candidateArchives.push({ file: record.archiveFile, bytes });
    }
    bakedMountainVillagers = cachedDocument.villagers;
    reusableMountainArchives = candidateArchives;
  }
} catch {
  reusableMountainArchives = null;
}

if (reusableMountainArchives) {
  for (const archive of reusableMountainArchives) recordStreamingBytes(archive.file, archive.bytes);
} else {
  for (let index = 0; index < mountainLayout.hutInfos.length; index += 1) {
    const villager = villagerCharacterRuntime.makeVillager(mountainLayout.hutInfos[index], index);
    const archive = buildVillagerCharacterArchive(villager.character);
    const archiveFile = `mountain-${index.toString().padStart(2, "0")}.wofavatar`;
    await emitStreamingBytes(archiveFile, archive);
    bakedMountainVillagers.push({
      id: villager.id,
      index,
      displayName: `Town Villager ${index + 1}`,
      townId: `survival-mountain-villagers-${mountainChunk.key}`,
      archiveFile,
      archiveBytes: archive.length,
      archiveSha256: sha256(archive),
      x: villager.x,
      y: villager.y,
      z: villager.z,
      baseYaw: villager.baseYaw,
      lookUpdateDesktopMs: 140 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      lookUpdateMobileMs: 220 + villagerCharacterRuntime.hashValue(villager.id, 0x51a7) * 90,
      hut: {
        x: villager.hut.x,
        y: villager.hut.y,
        z: villager.hut.z,
        hutType: villager.hut.hutType,
        isMushroom: villager.hut.isMushroom,
        rotation: villager.hut.rotation,
        interiorWidth: villager.hut.interiorWidth ?? 0,
        interiorDepth: villager.hut.interiorDepth ?? 0,
        interiorHeight: villager.hut.interiorHeight ?? 0,
      },
      character: villager.character,
    });
  }
}
if (bakedMountainVillagers.length !== mountainExpectedCounts.hutInfos) {
  throw new Error(`Expected ${mountainExpectedCounts.hutInfos} exact mountain villagers; found ${bakedMountainVillagers.length}.`);
}

const mountainTerrainGeometry = mountainVillageTerrainGeometry.makeMountainVillageTerrainGeometry(
  mountainChunk,
  true,
  true,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  survivalTerrainSurface.getSurvivalSmoothedTerrainColor,
  survivalTerrainSurface.getSurvivalVillageBaseHeight,
);
const mountainTerrainColliderGeometry = mountainVillageColliderGeometry.makeMountainVillageTerrainColliderGeometry(
  mountainChunk,
  true,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  survivalTerrainSurface.getSurvivalVillageBaseHeight,
);
const mountainSlopeGrassGeometry = makeExactMountainSlopeGrassGeometry(mountainSlopeGrassTufts);
const mountainGeometries = {
  terrain: serializeThreeGeometry(mountainTerrainGeometry),
  terrainCollider: serializeThreeGeometry(mountainTerrainColliderGeometry),
  slopeGrass: serializeThreeGeometry(mountainSlopeGrassGeometry),
  trailDeck: serializeThreeGeometry(mountainLayout.trailDeckGeometry),
  trailTop: serializeThreeGeometry(mountainLayout.trailTopGeometry),
  trailCollider: serializeThreeGeometry(mountainLayout.trailColliderGeometry),
  summitCollider: serializeThreeGeometry(mountainLayout.summitColliderGeometry),
};
mountainTerrainGeometry.dispose();
mountainTerrainColliderGeometry.dispose();
mountainSlopeGrassGeometry.dispose();
mountainLayout.trailDeckGeometry.dispose();
mountainLayout.trailTopGeometry.dispose();
mountainLayout.trailColliderGeometry.dispose();
mountainLayout.summitColliderGeometry.dispose();
const {
  trailDeckGeometry: _mountainTrailDeckGeometry,
  trailTopGeometry: _mountainTrailTopGeometry,
  trailColliderGeometry: _mountainTrailColliderGeometry,
  summitColliderGeometry: _mountainSummitColliderGeometry,
  ...mountainSerializableLayout
} = mountainLayout;
const mountainLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "mountainVillageSceneLayout.makeMountainVillageLayout(3,0,near)",
  sourceSignature: mountainSourceSignature,
  chunk: mountainChunk,
  baseHeight: mountainBaseHeight,
  summitY: mountainLayout.summitY,
  counts: {
    ...mountainBaseActualCounts,
    ...mountainRuntimeActualCounts,
    villagers: bakedMountainVillagers.length,
  },
  constants: {
    radius: mountainVillageTerrain.MOUNTAIN_VILLAGE_RADIUS,
    height: mountainVillageTerrain.MOUNTAIN_VILLAGE_HEIGHT,
    plateauRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_PLATEAU_RADIUS,
    trailTurns: mountainVillageTerrain.MOUNTAIN_VILLAGE_TRAIL_TURNS,
    trailStartRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_TRAIL_START_RADIUS,
    trailEndRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_TRAIL_END_RADIUS,
    trailHeightOffset: mountainVillageTerrain.MOUNTAIN_VILLAGE_TRAIL_HEIGHT_OFFSET,
    summitColliderRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_SUMMIT_COLLIDER_RADIUS,
    mineshaftHoleRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_HOLE_RADIUS,
    mineshaftTerrainCutRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_TERRAIN_CUT_RADIUS,
    mineshaftRimMidRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_RIM_MID_RADIUS,
    mineshaftRimOuterRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_RIM_OUTER_RADIUS,
    mineshaftBottomBaseOffset: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_BOTTOM_BASE_OFFSET,
    mineshaftBottomRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_BOTTOM_RADIUS,
    mineshaftCatwalkInnerRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_INNER_RADIUS,
    mineshaftCatwalkOuterRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_OUTER_RADIUS,
    mineshaftCatwalkSegments: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_CATWALK_SEGMENTS,
    mineshaftLadderRingRadius: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_RING_RADIUS,
    mineshaftLadderWidth: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_WIDTH,
    mineshaftLadderSensorDepth: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_SENSOR_DEPTH,
    mineshaftLadderPlatformGap: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_LADDER_PLATFORM_GAP,
    mineshaftExitBridgeWidth: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_EXIT_BRIDGE_WIDTH,
    mineshaftExitBridgeYOffset: mountainVillageTerrain.MOUNTAIN_VILLAGE_MINESHAFT_EXIT_BRIDGE_Y_OFFSET,
    slopeGrassNearCount: mountainVillageTerrain.MOUNTAIN_VILLAGE_SLOPE_GRASS_NEAR_COUNT,
  },
  layout: mountainSerializableLayout,
  slopeGrassTufts: mountainSlopeGrassTufts,
  opening: mountainOpening,
  wallDecor: mountainWallDecor,
  banquet: mountainBanquet,
  banquetColliders: mountainBanquetColliders,
  catwalk: mountainCatwalk,
  catwalkColliders: mountainCatwalkColliders,
  interiorPlatforms: mountainInteriorPlatforms,
  ladderDetails: mountainLadderDetails,
  exitBridge: mountainExitBridge,
  waterfallVisuals: mountainWaterfallVisuals,
  villagers: bakedMountainVillagers,
  geometries: mountainGeometries,
}, null, 2)}\n`, "utf8");
await emitBytes("MountainVillage/runtime-layout.json", mountainLayoutBytes);
await emitCanvas(
  "MountainVillage/Textures/terrain-detail.png",
  survivalTerrainTextures.getMountainVillageTerrainDetailTexture().image as Canvas,
);

const graveyardChunkCx = 5;
const graveyardChunkCz = 2;
const graveyardChunk = {
  key: `${graveyardChunkCx}:${graveyardChunkCz}`,
  cx: graveyardChunkCx,
  cz: graveyardChunkCz,
  x: graveyardChunkCx * 512,
  z: graveyardChunkCz * 512,
  distance: 0,
  biome: survivalBiome.getSurvivalBiome(graveyardChunkCx, graveyardChunkCz),
  hasVillage: true,
  villageKind: "graveyard",
  hasRiver: survivalRivers.getSurvivalChunkHasRiver(graveyardChunkCx, graveyardChunkCz),
  riverVertical: survivalMath.survivalHash01(graveyardChunkCx, graveyardChunkCz, 5) > 0.5,
  lod: "near",
};
const graveyardBaseHeight = survivalTerrainSurface.getSurvivalVillageBaseHeight(graveyardChunk);
const resolveGraveyardHeight = (localX: number, localZ: number) => graveyardVillageGeometry.getGraveyardVillageHeight(
  graveyardChunk,
  localX,
  localZ,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  graveyardBaseHeight,
);
const graveyardLayout = graveyardVillageLayout.makeGraveyardLayout(
  graveyardChunk,
  graveyardBaseHeight,
  true,
  resolveGraveyardHeight,
);
const graveyardVisibleTombs = graveyardVillageTombs.getGraveyardVisibleTombsForView(graveyardLayout.tombs, true);
if (graveyardVisibleTombs.length !== graveyardLayout.tombs.length) {
  throw new Error("Exact near graveyard must expose every generated tomb.");
}

const graveyardTombRecords: Array<Record<string, unknown>> = [];
for (let index = 0; index < graveyardLayout.tombs.length; index += 1) {
  const tomb = graveyardLayout.tombs[index];
  const styleIndex = Math.floor(survivalMath.survivalHash01(tomb.localX, tomb.localZ, 12412) * 5) % 5;
  const stoneColor = tomb.variant > 0.66 ? "#9a9488" : tomb.variant > 0.33 ? "#b2ab9e" : "#7d7972";
  const darkStone = tomb.variant > 0.5 ? "#4a4640" : "#36332f";
  const accentStone = tomb.variant > 0.66 ? "#d2c9b7" : tomb.variant > 0.33 ? "#716b62" : "#bfb7a7";
  const foundationColor = tomb.variant > 0.5 ? "#1d241b" : "#252c21";
  const width = 11.6 + tomb.variant * 5.2 + (styleIndex === 3 ? 2.4 : 0);
  const height = 15.2 + survivalMath.survivalHash01(tomb.localX, tomb.localZ, 12400) * 7.6 + (styleIndex === 1 ? 4.6 : 0);
  const depth = 1.85 + tomb.variant * 0.72;
  const baseWidth = width + (styleIndex === 3 ? 5.8 : 4.1);
  const baseDepth = depth + 2.45;
  const labelY = styleIndex === 1 ? height * 0.42 + 1.25 : styleIndex === 3 ? height * 0.39 + 1.15 : height * 0.48 + 1.05;
  const labelHeight = styleIndex === 1 ? height * 0.42 : styleIndex === 4 ? height * 0.48 : height * 0.52;
  const labelWidth = styleIndex === 3 ? width * 0.43 : width * 0.78;
  const outputPrefix = `GraveyardVillage/Tombs/${index.toString().padStart(2, "0")}`;
  const bodyTexture = `${outputPrefix}-body.png`;
  const darkTexture = `${outputPrefix}-dark.png`;
  const accentTexture = `${outputPrefix}-accent.png`;
  const foundationTexture = `${outputPrefix}-foundation.png`;
  await emitCanvas(bodyTexture, makeExactGraveyardStoneCanvas(stoneColor, tomb.variant + styleIndex * 0.17, "body"));
  await emitCanvas(darkTexture, makeExactGraveyardStoneCanvas(darkStone, tomb.variant + styleIndex * 0.19, "dark"));
  await emitCanvas(accentTexture, makeExactGraveyardStoneCanvas(accentStone, tomb.variant + styleIndex * 0.23, "accent"));
  await emitCanvas(foundationTexture, makeExactGraveyardStoneCanvas(foundationColor, tomb.variant + styleIndex * 0.29, "foundation"));
  const inscriptionTexture = index % 8 === 0 ? `${outputPrefix}-inscription.png` : null;
  if (inscriptionTexture) {
    await emitCanvas(
      inscriptionTexture,
      makeExactGraveyardInscriptionCanvas(tomb.name, tomb.joke, styleIndex * 11 + Math.floor(tomb.variant * 9)),
    );
  }
  graveyardTombRecords.push({
    ...tomb,
    styleIndex,
    width,
    height,
    depth,
    baseWidth,
    baseDepth,
    labelY,
    labelHeight,
    labelWidth,
    frontZ: -depth / 2 - 0.035,
    colors: { stoneColor, darkStone, accentStone, foundationColor },
    textures: { bodyTexture, darkTexture, accentTexture, foundationTexture, inscriptionTexture },
  });
}

const graveyardTerrainGeometry = graveyardVillageGeometry.makeGraveyardVillageTerrainGeometry(
  graveyardChunk,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  survivalTerrainSurface.getSurvivalSmoothedTerrainColor,
  survivalTerrainSurface.getSurvivalVillageBaseHeight,
);
const graveyardTerrainSkirtGeometry = graveyardVillageGeometry.makeGraveyardVillageTerrainSkirtGeometry(
  graveyardChunk,
  survivalTerrainSurface.getSurvivalTerrainHeightForChunk,
  survivalTerrainSurface.getSurvivalSmoothedTerrainColor,
  survivalTerrainSurface.getSurvivalVillageBaseHeight,
);
const graveyardRampColliderGeometry = graveyardChapelStructure.makeChapelRampColliderGeometry(graveyardBaseHeight);
const graveyardGeometries = {
  terrain: serializeThreeGeometry(graveyardTerrainGeometry),
  terrainSkirt: serializeThreeBasicGeometry(graveyardTerrainSkirtGeometry),
  rampCollider: serializeThreeGeometry(graveyardRampColliderGeometry),
};
graveyardTerrainGeometry.dispose();
graveyardTerrainSkirtGeometry.dispose();
graveyardRampColliderGeometry.dispose();

const chapelCharacterArchives: Array<Record<string, unknown>> = [];
const chapelCharacters = [...graveyardChapelCharacters.CHAPEL_NPC_CHARACTERS, graveyardChapelCharacters.CHAPEL_POPE_CHARACTER];
for (let index = 0; index < chapelCharacters.length; index += 1) {
  const archive = buildVillagerCharacterArchive(chapelCharacters[index]);
  const archiveFile = index === chapelCharacters.length - 1
    ? "graveyard-chapel-pope.wofavatar"
    : `graveyard-chapel-npc-${index.toString().padStart(2, "0")}.wofavatar`;
  await emitStreamingBytes(archiveFile, archive);
  chapelCharacterArchives.push({
    index,
    role: index === chapelCharacters.length - 1 ? "pope" : "seated",
    archiveFile,
    archiveBytes: archive.length,
    archiveSha256: sha256(archive),
    character: chapelCharacters[index],
  });
}

const clampNumber = (value: number, minimum: number, maximum: number) => Math.max(minimum, Math.min(maximum, value));
const clampChapelNpcSeatPosition = (x: number, z: number): [number, number] => {
  const clearance = graveyardChapelStructure.CHAPEL_SEATED_NPC_WALL_CLEARANCE;
  if (Math.abs(x) > graveyardChapelStructure.CHAPEL_CENTER_HALF_WIDTH) {
    const side = x < 0 ? -1 : 1;
    return [
      side * clampNumber(
        Math.abs(x),
        graveyardChapelStructure.CHAPEL_CENTER_HALF_WIDTH + clearance,
        graveyardChapelStructure.CHAPEL_OUTER_HALF_WIDTH - clearance,
      ),
      clampNumber(
        z,
        -graveyardChapelStructure.CHAPEL_SIDE_WING_HALF_DEPTH + clearance,
        graveyardChapelStructure.CHAPEL_SIDE_WING_HALF_DEPTH - clearance,
      ),
    ];
  }
  return [
    clampNumber(
      x,
      -graveyardChapelStructure.CHAPEL_CENTER_HALF_WIDTH + clearance,
      graveyardChapelStructure.CHAPEL_CENTER_HALF_WIDTH - clearance,
    ),
    clampNumber(
      z,
      -graveyardChapelStructure.CHAPEL_CENTER_HALF_DEPTH + clearance,
      graveyardChapelStructure.CHAPEL_CENTER_HALF_DEPTH - clearance,
    ),
  ];
};
const graveyardCenterNpcPlacements: Array<Record<string, unknown>> = [];
for (let rowIndex = 0; rowIndex < graveyardChapelLayout.CHAPEL_CENTER_PEW_ROWS.length; rowIndex += 1) {
  const z = graveyardChapelLayout.CHAPEL_CENTER_PEW_ROWS[rowIndex];
  for (const side of graveyardChapelLayout.CHAPEL_SIDE_SIGNS) {
    for (let seatIndex = 0; seatIndex < graveyardChapelLayout.CHAPEL_CENTER_NPC_SEAT_OFFSETS.length; seatIndex += 1) {
      const seat = graveyardChapelLayout.CHAPEL_CENTER_NPC_SEAT_OFFSETS[seatIndex];
      const [seatX, seatZ] = clampChapelNpcSeatPosition(side * seat.x, z + seat.z);
      const characterIndex = (rowIndex * 4 + (side > 0 ? 2 : 0) + seatIndex) % graveyardChapelCharacters.CHAPEL_NPC_CHARACTERS.length;
      graveyardCenterNpcPlacements.push({
        key: `chapel-pew-npc-${rowIndex}-${side}-${seatIndex}`,
        position: [seatX, 2.98 + avatarFactory.NPC_AVATAR_GROUND_LIFT, seatZ],
        yaw: graveyardChapelLayout.getAvatarYawFacingTarget(
          seatX,
          seatZ,
          graveyardChapelLayout.CHAPEL_POPE_TARGET.x,
          graveyardChapelLayout.CHAPEL_POPE_TARGET.z,
        ),
        characterIndex,
      });
    }
  }
}
const graveyardSideNpcPlacements: Array<Record<string, unknown>> = [];
for (let pewIndex = 0; pewIndex < graveyardChapelLayout.CHAPEL_SIDE_WING_PEW_LAYOUT.length; pewIndex += 1) {
  const pew = graveyardChapelLayout.CHAPEL_SIDE_WING_PEW_LAYOUT[pewIndex];
  const yaw = graveyardChapelLayout.getYawForPewFacingTarget(
    pew.x,
    pew.z,
    graveyardChapelLayout.CHAPEL_POPE_TARGET.x,
    graveyardChapelLayout.CHAPEL_POPE_TARGET.z,
  );
  for (let seatIndex = 0; seatIndex < graveyardChapelLayout.CHAPEL_SIDE_NPC_SEAT_OFFSETS.length; seatIndex += 1) {
    const offset = graveyardChapelLayout.CHAPEL_SIDE_NPC_SEAT_OFFSETS[seatIndex];
    const cos = Math.cos(yaw);
    const sin = Math.sin(yaw);
    const localX = offset * pew.width;
    const localZ = -0.42;
    const [seatX, seatZ] = clampChapelNpcSeatPosition(
      pew.x + localX * cos + localZ * sin,
      pew.z - localX * sin + localZ * cos,
    );
    graveyardSideNpcPlacements.push({
      key: `chapel-side-pew-npc-${pew.key}-${seatIndex}`,
      position: [seatX, 2.78 + avatarFactory.NPC_AVATAR_GROUND_LIFT, seatZ],
      yaw: graveyardChapelLayout.getAvatarYawFacingTarget(
        seatX,
        seatZ,
        graveyardChapelLayout.CHAPEL_POPE_TARGET.x,
        graveyardChapelLayout.CHAPEL_POPE_TARGET.z,
      ),
      characterIndex: (pewIndex * 3 + seatIndex + 7) % graveyardChapelCharacters.CHAPEL_NPC_CHARACTERS.length,
    });
  }
}

const chapelStoneTexture = graveyardChapelDetails.createChapelStoneBrickTexture({
  base: "#46454d", mid: "#535159", light: "#5d5a63", mortar: "#1b1a20",
  highlight: "#8d8780", shadow: "#2b2a30", chip: "#706b67", repeatX: 3, repeatY: 2,
});
const chapelDarkStoneTexture = graveyardChapelDetails.createChapelStoneBrickTexture({
  base: "#292830", mid: "#33323a", light: "#3b3942", mortar: "#0f0e13",
  highlight: "#625e5a", shadow: "#16151b", chip: "#4a4744", repeatX: 2, repeatY: 3,
});
await emitCanvas("GraveyardVillage/Textures/chapel-stone.png", chapelStoneTexture.image as Canvas);
await emitCanvas("GraveyardVillage/Textures/chapel-dark-stone.png", chapelDarkStoneTexture.image as Canvas);
chapelStoneTexture.dispose();
chapelDarkStoneTexture.dispose();
const chapelPopeMiter = createCanvas(96, 64);
const chapelPopeMiterContext = chapelPopeMiter.getContext("2d");
chapelPopeMiterContext.imageSmoothingEnabled = false;
chapelPopeMiterContext.clearRect(0, 0, chapelPopeMiter.width, chapelPopeMiter.height);
chapelPopeMiterContext.fillStyle = "#f4f1e8";
for (const [x, y, width, height] of [[24, 6, 16, 6], [20, 12, 24, 8], [16, 20, 32, 8], [12, 28, 40, 10], [16, 38, 32, 8], [22, 46, 20, 6]]) {
  chapelPopeMiterContext.fillRect(x, y, width, height);
}
chapelPopeMiterContext.fillStyle = "#c8c1b4";
chapelPopeMiterContext.fillRect(12, 36, 40, 4);
chapelPopeMiterContext.fillRect(18, 44, 28, 4);
chapelPopeMiterContext.fillStyle = "#ffffff";
chapelPopeMiterContext.fillRect(18, 22, 24, 4);
chapelPopeMiterContext.fillRect(22, 14, 14, 4);
chapelPopeMiterContext.fillStyle = "#d4af37";
chapelPopeMiterContext.fillRect(30, 11, 4, 31);
chapelPopeMiterContext.fillRect(24, 22, 16, 4);
chapelPopeMiterContext.fillRect(28, 6, 8, 4);
chapelPopeMiterContext.fillStyle = "#7a5328";
chapelPopeMiterContext.fillRect(35, 15, 3, 25);
chapelPopeMiterContext.fillRect(26, 27, 15, 3);
await emitCanvas("GraveyardVillage/Textures/chapel-pope-miter.png", chapelPopeMiter);
await emitCanvas(
  "GraveyardVillage/Textures/terrain-detail.png",
  survivalTerrainTextures.getSurvivalTerrainDetailTexture().image as Canvas,
);

const graveyardSourceSignature = sha256(Buffer.concat([
  Buffer.from("react-graveyard-village-v1-exact-near-chunk", "utf8"),
  ...await Promise.all(graveyardVillageSourcePaths.map((sourcePath) => readFile(sourcePath))),
  await readFile(avatarFactoryPath),
  await readFile(survivalTerrainSurfacePath),
  await readFile(survivalBiomePath),
  await readFile(survivalRiversPath),
  await readFile(survivalMathPath),
  await readFile(survivalTerrainTexturesPath),
]));
const graveyardLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "survivalGraveyardVillageRendering.SurvivalGraveyardVillage(5,2,near)",
  sourceSignature: graveyardSourceSignature,
  chunk: graveyardChunk,
  baseHeight: graveyardBaseHeight,
  counts: {
    tombs: graveyardTombRecords.length,
    inscribedTombs: graveyardTombRecords.filter((record: any) => record.textures.inscriptionTexture).length,
    fenceSegments: graveyardLayout.fenceSegments.length,
    pathStones: graveyardLayout.pathStones.length,
    chapelCharacters: chapelCharacterArchives.length,
    centerNpcs: graveyardCenterNpcPlacements.length,
    sideWingNpcs: graveyardSideNpcPlacements.length,
  },
  constants: {
    villageRadius: graveyardVillageTerrain.GRAVEYARD_VILLAGE_RADIUS,
    ringPathRadius: graveyardVillageTerrain.GRAVEYARD_RING_PATH_RADIUS,
    fenceRadius: graveyardVillageTerrain.GRAVEYARD_FENCE_RADIUS,
    avatarScale: avatarFactory.NPC_AVATAR_SCALE,
    avatarGroundLift: avatarFactory.NPC_AVATAR_GROUND_LIFT,
  },
  layout: {
    ...graveyardLayout,
    tombs: graveyardTombRecords,
  },
  chapel: {
    viewSummary: graveyardChapelView.getGraveyardChapelViewSummary(),
    interiorSummary: graveyardChapelInterior.getChapelInteriorSummary(),
    exteriorSummary: graveyardChapelExteriorParts.getChapelExteriorPartsSummary(),
    seatingSummary: graveyardChapelPews.getChapelPewSeatingSummary(),
    colliderSummary: graveyardVillageColliders.getGraveyardVillageColliderSummary(graveyardLayout.fenceSegments.length),
    dimensions: {
      centerHalfWidth: graveyardChapelStructure.CHAPEL_CENTER_HALF_WIDTH,
      centerHalfDepth: graveyardChapelStructure.CHAPEL_CENTER_HALF_DEPTH,
      sideWingHalfWidth: graveyardChapelStructure.CHAPEL_SIDE_WING_HALF_WIDTH,
      sideWingHalfDepth: graveyardChapelStructure.CHAPEL_SIDE_WING_HALF_DEPTH,
      sideWingCenterX: graveyardChapelStructure.CHAPEL_SIDE_WING_CENTER_X,
      outerHalfWidth: graveyardChapelStructure.CHAPEL_OUTER_HALF_WIDTH,
      wallThickness: graveyardChapelStructure.CHAPEL_WALL_THICKNESS,
      wallHeight: graveyardChapelStructure.CHAPEL_WALL_HEIGHT,
      wallHalfHeight: graveyardChapelStructure.CHAPEL_WALL_HALF_HEIGHT,
      exitHalfWidth: graveyardChapelStructure.CHAPEL_EXIT_HALF_WIDTH,
      sideExitHalfWidth: graveyardChapelStructure.CHAPEL_SIDE_EXIT_HALF_WIDTH,
      rearExitCenterX: graveyardChapelStructure.CHAPEL_REAR_EXIT_CENTER_X,
      rearExitHalfWidth: graveyardChapelStructure.CHAPEL_REAR_EXIT_HALF_WIDTH,
      stairRampLength: graveyardChapelStructure.CHAPEL_STAIR_RAMP_LENGTH,
      stairRampThickness: graveyardChapelStructure.CHAPEL_STAIR_RAMP_THICKNESS,
      stairRampLowTop: graveyardChapelStructure.CHAPEL_STAIR_RAMP_LOW_TOP,
      stairRampCenterTop: graveyardChapelStructure.CHAPEL_STAIR_RAMP_CENTER_TOP,
      stairRampWingTop: graveyardChapelStructure.CHAPEL_STAIR_RAMP_WING_TOP,
      watchTowerHeight: graveyardChapelStructure.CHAPEL_WATCH_TOWER_HEIGHT,
      watchTowerRadius: graveyardChapelStructure.CHAPEL_WATCH_TOWER_RADIUS,
      watchTowerY: graveyardChapelStructure.CHAPEL_WATCH_TOWER_Y,
    },
    wallSegments: graveyardChapelStructure.getChapelWallSegments(),
    watchTowerPositions: graveyardChapelStructure.CHAPEL_WATCH_TOWER_POSITIONS.map(
      ([x, y, z]: [number, number, number]) => ({ x, y, z }),
    ),
    gargoyles: graveyardChapelStructure.CHAPEL_GARGOYLE_POSITIONS.map((gargoyle: any) => ({
      ...gargoyle,
      scale: gargoyle.scale ?? 1,
    })),
    exitRamps: graveyardChapelStructure.CHAPEL_EXIT_RAMP_DEFINITIONS,
    exitShadows: graveyardChapelStructure.CHAPEL_EXIT_SHADOW_DEFINITIONS,
    chandelierCandles: graveyardChapelStructure.CHAPEL_CHANDELIER_CANDLE_POSITIONS.map(
      ([x, y, z]: [number, number, number]) => ({ x, y, z }),
    ),
    interiorCandles: graveyardChapelStructure.CHAPEL_INTERIOR_CANDLE_SPOTS.map(
      ([x, y, z]: [number, number, number]) => ({ x, y, z }),
    ),
    centerPewRows: graveyardChapelLayout.CHAPEL_CENTER_PEW_ROWS,
    centerPewColliders: graveyardChapelLayout.CHAPEL_CENTER_PEW_COLLIDERS,
    sideWingPews: graveyardChapelLayout.CHAPEL_SIDE_WING_PEW_LAYOUT,
    wingCeilingBeams: graveyardChapelLayout.CHAPEL_WING_CEILING_BEAMS,
    centerNpcPlacements: graveyardCenterNpcPlacements,
    sideWingNpcPlacements: graveyardSideNpcPlacements,
    pope: {
      target: graveyardChapelLayout.CHAPEL_POPE_TARGET,
      position: [graveyardChapelLayout.CHAPEL_POPE_TARGET.x, 7.1, graveyardChapelLayout.CHAPEL_POPE_TARGET.z],
      yaw: Math.PI,
      characterIndex: chapelCharacters.length - 1,
      miterTexture: "GraveyardVillage/Textures/chapel-pope-miter.png",
    },
    characters: chapelCharacterArchives,
  },
  geometries: graveyardGeometries,
}, null, 2)}\n`, "utf8");
await emitBytes("GraveyardVillage/runtime-layout.json", graveyardLayoutBytes);

const darrelRepeatingTextureKinds = [
  "ground",
  "bark",
  "leaf",
  "wall",
  "roof",
  "tatami",
  "wood",
  "water",
  "stone",
  "dojo",
] as const;
for (const kind of darrelRepeatingTextureKinds) {
  const texture = darrelGroveTextures.getDarrelTexture(kind);
  await emitCanvas(`DarrelGrove/Textures/Repeating/${kind}.png`, texture.image as Canvas);
}
await emitCanvas(
  "DarrelGrove/Textures/Repeating/petal-carpet.png",
  darrelGroveTextures.getDarrelPetalCarpetTexture().image as Canvas,
);
await emitCanvas(
  "DarrelGrove/Textures/Clamped/blossom.png",
  darrelGroveTextures.getDarrelBlossomTexture().image as Canvas,
);
await emitCanvas(
  "DarrelGrove/Textures/Clamped/petal.png",
  darrelGroveTextures.getDarrelPetalTexture().image as Canvas,
);
await emitCanvas(
  "DarrelGrove/Textures/Clamped/fuji.png",
  darrelGroveTextures.getDarrelFujiTexture().image as Canvas,
);

// Exact 64x64 canvas authored by QuestNavigationBeacons.tsx.
const questBeacon = createCanvas(64, 64);
const questBeaconContext = questBeacon.getContext("2d");
questBeaconContext.clearRect(0, 0, 64, 64);
questBeaconContext.fillStyle = "rgba(0, 0, 0, 0.75)";
questBeaconContext.beginPath();
questBeaconContext.moveTo(32, 5);
questBeaconContext.lineTo(59, 32);
questBeaconContext.lineTo(32, 59);
questBeaconContext.lineTo(5, 32);
questBeaconContext.closePath();
questBeaconContext.fill();
questBeaconContext.fillStyle = "#f9a8d4";
questBeaconContext.fillRect(29, 16, 6, 24);
questBeaconContext.fillRect(28, 45, 8, 7);
questBeaconContext.fillStyle = "#fff7ad";
questBeaconContext.fillRect(30, 17, 3, 23);
questBeaconContext.fillRect(29, 46, 4, 5);
await emitCanvas("Quest/quest-beacon.png", questBeacon);

const darrelDragonManifestBytes = await readFile(darrelDragonManifestPath);
const darrelDragonManifest = JSON.parse(darrelDragonManifestBytes.toString("utf8")) as Record<string, unknown>;
for (const mode of ["sleep", "wake", "idle", "attack"] as const) {
  const frames = darrelDragonManifest[mode];
  if (!Array.isArray(frames) || frames.length === 0) {
    throw new Error(`Darrel dragon manifest is missing ${mode} frames.`);
  }
  for (const framePath of frames) {
    if (typeof framePath !== "string") {
      throw new Error(`Darrel dragon ${mode} manifest contains a non-string frame.`);
    }
    const fileName = path.basename(framePath);
    await emitBytes(`DarrelGrove/Dragon/${fileName}`, await readFile(path.join(darrelDragonSourceRoot, fileName)));
  }
}
await emitBytes("DarrelGrove/Dragon/manifest.json", darrelDragonManifestBytes);

const darrelRuntimeLayoutBytes = Buffer.from(`${JSON.stringify({
  schemaVersion: 1,
  source: "darrelGroveRuntime.ts",
  groveGroundY: 18,
  groveHalfSize: 252,
  detailPhaseDelaysMs: darrelGroveRuntime.DARREL_GROVE_DETAIL_PHASE_DELAYS_MS,
  backyardRiverSegments: darrelGroveRuntime.DARREL_BACKYARD_RIVER_SEGMENTS,
  backyardRiverStones: darrelGroveRuntime.DARREL_BACKYARD_RIVER_STONES,
  waterfallHillStones: darrelGroveRuntime.DARREL_WATERFALL_HILL_STONES,
  waterfallMossPads: darrelGroveRuntime.DARREL_WATERFALL_MOSS_PADS,
  waterfallRiverFeedChannels: darrelGroveRuntime.DARREL_WATERFALL_RIVER_FEED_CHANNELS,
  waterfallRiverMouths: darrelGroveRuntime.DARREL_WATERFALL_RIVER_MOUTHS,
  waterfallSprayPuffs: darrelGroveRuntime.DARREL_WATERFALL_SPRAY_PUFFS,
  waterfallRunnels: darrelGroveRuntime.DARREL_WATERFALL_RUNNELS,
  petalDriftPatches: darrelGroveRuntime.DARREL_PETAL_DRIFT_PATCHES,
  bonsaiBranches: darrelGroveRuntime.DARREL_BONSAI_BRANCHES,
  bonsaiCanopyPads: darrelGroveRuntime.DARREL_BONSAI_CANOPY_PADS,
  bonsaiBlossomClusters: darrelGroveRuntime.DARREL_BONSAI_BLOSSOM_CLUSTERS,
  legacyBonsaiBranches: darrelGroveRuntime.DARREL_LEGACY_BONSAI_BRANCHES,
  legacyBonsaiBlossomClusters: darrelGroveRuntime.DARREL_LEGACY_BONSAI_BLOSSOM_CLUSTERS,
  fallenPetals: darrelGroveRuntime.getDarrelFallenPetals(360, 18),
  fallingPetals: darrelGroveRuntime.getDarrelFallingPetals(68),
}, null, 2)}\n`, "utf8");
await emitBytes("DarrelGrove/runtime-layout.json", darrelRuntimeLayoutBytes);

const launchPreview = createCanvas(360, 360);
const launchContext = launchPreview.getContext("2d");
launchContext.imageSmoothingEnabled = false;
launchContext.setTransform(2, 0, 0, 2, 0, 0);
launchContext.fillStyle = "#090510";
launchContext.fillRect(0, 0, 180, 180);
launchContext.fillStyle = "rgba(34, 211, 238, 0.08)";
for (let x = 0; x < 180; x += 12) launchContext.fillRect(x, 0, 1, 180);
for (let y = 0; y < 180; y += 12) launchContext.fillRect(0, y, 180, 1);
launchContext.strokeStyle = "rgba(250, 204, 21, 0.35)";
launchContext.lineWidth = 2;
launchContext.strokeRect(12, 10, 156, 160);
avatarFactory.drawPixelAvatarFrame(launchContext, {
  direction: 0,
  animation: "holding",
  frame: 0,
  x: 26,
  y: 28,
  scale: 1.35,
  detailScale: 1.35,
});
await emitCanvas("Avatar/Default/launch-preview.png", launchPreview);

// Exact React HUD sources used by the Unity spell-book and navigation-map surfaces.
await emitBytes("HUD/SpellMenu/spellbook_icon.png", await readFile(mobileSpellbookIconPath));
const makeExactBoostThumbnail = (kind: "jump" | "speed") => {
  const canvas = createCanvas(64, 64);
  const context = canvas.getContext("2d", { willReadFrequently: true });
  context.imageSmoothingEnabled = false;
  const blocks: Array<[number, number, number, number, string, number?]> = kind === "jump"
    ? [
      [29, 7, 6, 40, "rgba(190, 242, 100, 0.26)"], [21, 14, 22, 7, "#bef264"],
      [16, 21, 32, 7, "#84cc16"], [24, 28, 16, 19, "#22c55e"],
      [20, 47, 24, 6, "#14532d"], [14, 53, 12, 5, "#a3e635"],
      [38, 53, 12, 5, "#a3e635"], [8, 35, 7, 7, "rgba(34,197,94,0.78)"],
      [49, 32, 7, 7, "rgba(190,242,100,0.78)"], [27, 3, 10, 5, "#f7fee7"],
    ]
    : [
      [7, 14, 34, 5, "#fef08a", -8], [19, 22, 38, 6, "#facc15", -8],
      [11, 32, 44, 7, "#22d3ee", -8], [25, 42, 28, 5, "#fde047", -8],
      [6, 49, 24, 4, "rgba(14,165,233,0.82)", -8], [45, 10, 7, 7, "#fefce8"],
      [51, 27, 5, 5, "#fef08a"], [54, 39, 4, 4, "#67e8f9"],
      [11, 24, 5, 5, "rgba(253,224,71,0.78)"],
    ];
  for (const [x, y, width, height, color, rotation = 0] of blocks) {
    context.save();
    context.fillStyle = color;
    if (rotation) {
      context.translate(x + width / 2, y + height / 2);
      context.rotate((rotation * Math.PI) / 180);
      context.fillRect(-width / 2, -height / 2, width, height);
    } else {
      context.fillRect(x, y, width, height);
    }
    context.restore();
  }
  return canvas;
};
await emitCanvas("HUD/SpellMenu/speedboost.png", makeExactBoostThumbnail("speed"));
await emitCanvas("HUD/SpellMenu/jumpboost.png", makeExactBoostThumbnail("jump"));

outputs.sort((left, right) => left.path.localeCompare(right.path, "en"));
const sourceFiles = [
  avatarFactoryPath,
  hutVisualsPath,
  treeHouseTexturesPath,
  launchMenuPath,
  mobileSpellbookIconPath,
  spellThumbnailSourcePath,
  bushesPath,
  baseVillageHutLayoutPath,
  villagerCharacterRuntimePath,
  darrelGroveRuntimePath,
  darrelGroveTexturesPath,
  darrelDragonRuntimePath,
  darrelDragonManifestPath,
  desertVillageRuntimePath,
  desertVillageRenderingPath,
  desertVillageTerrainPath,
  chicagoCityLayoutPath,
  chicagoCityRenderingPath,
  chicagoCityCollidersPath,
  chicagoCityStreetRuntimePath,
  chicagoCityTrafficRuntimePath,
  chicagoCityTexturesPath,
  ...mountainVillageSourcePaths,
  ...graveyardVillageSourcePaths,
  mountainSlopeGrassRuntimePath,
  survivalGrassGeometryPath,
  survivalTerrainSurfacePath,
  survivalBiomePath,
  survivalRiversPath,
  survivalMathPath,
  survivalTerrainTexturesPath,
  survivalGrassTexturesPath,
];
const sourceHashes = Object.fromEntries(
  await Promise.all(sourceFiles.map(async (sourcePath) => [
    path.relative(reactRoot, sourcePath).replaceAll("\\", "/"),
    sha256(await readFile(sourcePath)),
  ])),
);
const manifest = {
  schemaVersion: 1,
  generator: "Tools/bake-react-visual-assets.mts",
  reactOracle: reactRoot,
  sourceHashes,
  outputCount: outputs.length,
  outputs,
};
const manifestBytes = Buffer.from(`${JSON.stringify(manifest, null, 2)}\n`, "utf8");
const manifestPath = path.join(outputRoot, "react-visual-assets.json");
if (await writeIfChanged(manifestPath, manifestBytes)) changedCount += 1;

console.log(JSON.stringify({
  status: "complete",
  outputRoot,
  outputCount: outputs.length,
  changedCount,
  manifestSha256: sha256(manifestBytes),
}, null, 2));

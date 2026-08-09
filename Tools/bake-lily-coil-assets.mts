import { createHash } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import { pathToFileURL } from "node:url";

const reactRoot = "D:\\CodexProjects\\Wizards-Only-Fools-React-Latest";
const unityRoot = "D:\\CodexProjects\\Wizards-Only-Fools-Unity";
const outputRoot = path.join(unityRoot, "Assets", "WOF", "Art", "Generated", "React", "LilyCoil");
const villageRoot = path.join(reactRoot, "src", "game", "systems", "world", "villages");
const playerRoot = path.join(reactRoot, "src", "game", "systems", "player");
const vegetationRoot = path.join(reactRoot, "src", "game", "systems", "world", "vegetation");
const renderingRoot = path.join(reactRoot, "src", "game", "systems", "rendering");
const eyeRoot = path.join(reactRoot, "public", "sprites", "lily-coil", "eye-cap-frames");

const rendererPath = path.join(villageRoot, "survivalLilyCoilRendering.tsx");
const floraPath = path.join(villageRoot, "lilyCoilFloraRuntime.ts");
const texturesPath = path.join(villageRoot, "lilyCoilTextures.ts");
const tubeMotionPath = path.join(villageRoot, "lilyCoilTubeMotion.ts");
const playerTubeRuntimePath = path.join(playerRoot, "playerLilyCoilTubeRuntime.ts");
const playerMovementConfigPath = path.join(playerRoot, "playerMovementConfig.ts");
const grassTexturesPath = path.join(vegetationRoot, "survivalGrassTextures.ts");
const textureNoisePath = path.join(renderingRoot, "textures", "textureNoise.ts");
const darrelTexturesPath = path.join(villageRoot, "darrelGroveTextures.ts");
const darrelGroveRuntimePath = path.join(villageRoot, "darrelGroveRuntime.ts");
const gameStorePath = path.join(reactRoot, "src", "store", "gameStore.ts");
const eyeManifestPath = path.join(eyeRoot, "manifest.json");

const CHUNK_X = 48;
const CHUNK_Z = -48;
const SURVIVAL_BLOCK_SIZE = 512;
const GROUND_Y = 10;
const REALM_RADIUS = 640;
const WALL_HEIGHT = 650;
const WALL_SEGMENT_COUNT = 36;
const TUBE_PATH_RADIUS = 238;
const TUBE_START_Y = 108;
const TUBE_RISE = 520;
const TUBE_TURNS = 3.15;
const TUBE_START_ANGLE = -Math.PI / 2;
const TUBE_RADIUS = 76;
const TUBE_CURVE_POINT_COUNT = 120;
const TUBE_RENDER_SEGMENTS = 144;
const TUBE_RENDER_RADIAL_SEGMENTS = 16;
const TUBE_COLLIDER_SEGMENTS = 72;
const TUBE_COLLIDER_RADIAL_SEGMENTS = 8;
const EYE_FRAME_COUNT = 36;
const EYE_FRAME_FPS = 10;
const HIGHLIGHT_COUNT = 7;

function assertDDrive(targetPath: string) {
  const resolved = path.resolve(targetPath);
  if (!/^D:\\/i.test(resolved)) throw new Error(`Refusing non-D path: ${resolved}`);
  return resolved;
}

for (const target of [reactRoot, unityRoot, outputRoot, eyeRoot]) assertDDrive(target);

const reactRequire = createRequire(path.join(reactRoot, "package.json"));
const { createCanvas } = reactRequire("canvas") as typeof import("canvas");
const THREE = reactRequire("three") as typeof import("three");
type Canvas = ReturnType<typeof createCanvas>;

Object.defineProperty(globalThis, "document", {
  configurable: true,
  value: {
    createElement(tagName: string) {
      if (tagName.toLowerCase() !== "canvas") {
        throw new Error(`The Lily Coil baker only supports canvas elements, not ${tagName}.`);
      }
      return createCanvas(1, 1);
    },
  },
  writable: false,
});

const lilyTextures = await import(pathToFileURL(texturesPath).href);
const lilyFlora = await import(pathToFileURL(floraPath).href);
const survivalGrassTextures = await import(pathToFileURL(grassTexturesPath).href);

function sha256(bytes: Uint8Array | string) {
  return createHash("sha256").update(bytes).digest("hex");
}

async function writeIfChanged(targetPath: string, bytes: Uint8Array) {
  assertDDrive(targetPath);
  await mkdir(path.dirname(targetPath), { recursive: true });
  try {
    const current = await readFile(targetPath);
    if (Buffer.compare(current, bytes) === 0) return false;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
  }

  const temporaryPath = `${targetPath}.wof-bake-${process.pid}.tmp`;
  await writeFile(temporaryPath, bytes);
  try {
    await rename(temporaryPath, targetPath);
  } finally {
    await rm(temporaryPath, { force: true });
  }
  return true;
}

const outputs: Array<{ path: string; bytes: number; sha256: string }> = [];
let changedCount = 0;

async function emitBytes(relativePath: string, bytes: Uint8Array) {
  const targetPath = path.join(outputRoot, relativePath);
  if (await writeIfChanged(targetPath, bytes)) changedCount += 1;
  outputs.push({ path: relativePath.replaceAll("\\", "/"), bytes: bytes.length, sha256: sha256(bytes) });
}

async function emitCanvas(relativePath: string, canvas: Canvas) {
  await emitBytes(relativePath, canvas.toBuffer("image/png"));
}

function makeTubePoint(t: number) {
  const angle = TUBE_START_ANGLE + Math.PI * 2 * TUBE_TURNS * t;
  return new THREE.Vector3(
    Math.cos(angle) * TUBE_PATH_RADIUS,
    TUBE_START_Y + TUBE_RISE * t,
    Math.sin(angle) * TUBE_PATH_RADIUS,
  );
}

function serializeGeometry(geometry: import("three").BufferGeometry) {
  const position = geometry.getAttribute("position");
  const normal = geometry.getAttribute("normal");
  const uv = geometry.getAttribute("uv");
  const color = geometry.getAttribute("color");
  const index = geometry.getIndex();
  if (!position || !index) throw new Error("Lily Coil tube geometry is missing positions or indices.");
  return {
    vertexCount: position.count,
    positions: Array.from(position.array as ArrayLike<number>),
    normals: normal ? Array.from(normal.array as ArrayLike<number>) : [],
    colors: color ? Array.from(color.array as ArrayLike<number>) : [],
    uvs: uv ? Array.from(uv.array as ArrayLike<number>) : [],
    indices: Array.from(index.array as ArrayLike<number>),
  };
}

await emitCanvas("Textures/grass.png", lilyTextures.getLilyCoilTexture("grass").image as Canvas);
await emitCanvas("Textures/stone.png", lilyTextures.getLilyCoilTexture("stone").image as Canvas);
await emitCanvas("Textures/wall.png", lilyTextures.getLilyCoilTexture("wall").image as Canvas);
await emitCanvas("Textures/ramp.png", lilyTextures.getLilyCoilTexture("ramp").image as Canvas);
await emitCanvas("Textures/calla-bloom.png", lilyTextures.getLilyCoilCallaBloomTexture().image as Canvas);
await emitCanvas("Textures/meadow-overlay.png", lilyTextures.getLilyCoilMeadowOverlayTexture().image as Canvas);
await emitCanvas("Textures/ground-blade-alpha.png", survivalGrassTextures.getLilyCoilBladeAlphaTexture().image as Canvas);
await emitCanvas("Textures/tube-grass-alpha.png", survivalGrassTextures.getLilyCoilGrassPatchAlphaTexture().image as Canvas);

const eyeManifest = JSON.parse(await readFile(eyeManifestPath, "utf8")) as {
  frameCount: number;
  fps: number;
  size: number;
};
if (eyeManifest.frameCount < EYE_FRAME_COUNT || eyeManifest.fps !== EYE_FRAME_FPS || eyeManifest.size !== 96) {
  throw new Error(`Unexpected Lily Coil eye manifest: ${JSON.stringify(eyeManifest)}`);
}

const eyeFrames: Array<{ index: number; file: string; bytes: number; sha256: string }> = [];
const eyeSourceBytes: Buffer[] = [];
for (let index = 0; index < EYE_FRAME_COUNT; index += 1) {
  const fileName = `eye_${index.toString().padStart(3, "0")}.png`;
  const bytes = await readFile(path.join(eyeRoot, fileName));
  eyeSourceBytes.push(bytes);
  const relativePath = `EyeFrames/${fileName}`;
  await emitBytes(relativePath, bytes);
  eyeFrames.push({ index, file: relativePath, bytes: bytes.length, sha256: sha256(bytes) });
}

const curvePoints: import("three").Vector3[] = [];
for (let index = 0; index < TUBE_CURVE_POINT_COUNT; index += 1) {
  curvePoints.push(makeTubePoint(index / (TUBE_CURVE_POINT_COUNT - 1)));
}
const tubeCurve = new THREE.CatmullRomCurve3(curvePoints, false, "catmullrom", 0.04);
const tunnelGeometry = new THREE.TubeGeometry(
  tubeCurve,
  TUBE_RENDER_SEGMENTS,
  TUBE_RADIUS,
  TUBE_RENDER_RADIAL_SEGMENTS,
  false,
);
const tunnelColliderGeometry = new THREE.TubeGeometry(
  tubeCurve,
  TUBE_COLLIDER_SEGMENTS,
  TUBE_RADIUS,
  TUBE_COLLIDER_RADIAL_SEGMENTS,
  false,
);

const tubeGrassGroups = lilyFlora.makeLilyCoilTubeGrassGroups(false, TUBE_RADIUS);
const tubeGrass = tubeGrassGroups.flatMap((group: Array<Record<string, number>>, groupIndex: number) =>
  group.map((tuft) => ({ ...tuft, group: groupIndex })),
);
const tubeLilies = lilyFlora.makeLilyCoilTubeLilies(false);
const tubeFlowers = lilyFlora.makeLilyCoilTubeFlowers(false);
const smallTubeFlowers = lilyFlora.makeLilyCoilSmallTubeFlowers(false);
const smallBloomParticles = lilyFlora.makeLilyCoilBloomParticles(false, smallTubeFlowers.length);
const fireflies = lilyFlora.makeLilyCoilFireflies(false, tubeFlowers.length);
const butterflies = lilyFlora.makeLilyCoilButterflies(false, tubeFlowers.length);
const groundGrass = lilyFlora.makeLilyCoilGroundGrass(false, REALM_RADIUS);
const groundLilies = lilyFlora.makeLilyCoilGroundLilies(false, REALM_RADIUS);
const groundLilyLights = lilyFlora.pickLilyCoilGroundLilyLights(groundLilies);

const sourcePaths = [
  rendererPath,
  floraPath,
  texturesPath,
  tubeMotionPath,
  playerTubeRuntimePath,
  playerMovementConfigPath,
  grassTexturesPath,
  textureNoisePath,
  darrelTexturesPath,
  darrelGroveRuntimePath,
  gameStorePath,
  eyeManifestPath,
];
const sourceBytes = await Promise.all(sourcePaths.map((sourcePath) => readFile(sourcePath)));
const sourceSignature = sha256(Buffer.concat([
  Buffer.from("react-lily-coil-v1-exact-desktop-realm", "utf8"),
  ...sourceBytes,
  ...eyeSourceBytes,
]));

const document = {
  schemaVersion: 1,
  source: "survivalLilyCoilRendering.SurvivalLilyCoil(48,-48,near)",
  sourceSignature,
  chunk: {
    key: `${CHUNK_X}:${CHUNK_Z}`,
    cx: CHUNK_X,
    cz: CHUNK_Z,
    x: CHUNK_X * SURVIVAL_BLOCK_SIZE,
    z: CHUNK_Z * SURVIVAL_BLOCK_SIZE,
    distance: 0,
    biome: "mushroom",
    hasVillage: true,
    villageKind: "lily-coil",
    hasRiver: false,
    riverVertical: false,
    lod: "near",
  },
  spawn: {
    x: CHUNK_X * SURVIVAL_BLOCK_SIZE + 237.11,
    y: 72.15,
    z: CHUNK_Z * SURVIVAL_BLOCK_SIZE - 20.54,
    yawRadians: 3.055,
  },
  constants: {
    survivalBlockSize: SURVIVAL_BLOCK_SIZE,
    groundY: GROUND_Y,
    realmRadius: REALM_RADIUS,
    wallHeight: WALL_HEIGHT,
    wallSegmentCount: WALL_SEGMENT_COUNT,
    tubePathRadius: TUBE_PATH_RADIUS,
    tubeStartY: TUBE_START_Y,
    tubeRise: TUBE_RISE,
    tubeTurns: TUBE_TURNS,
    tubeStartAngle: TUBE_START_ANGLE,
    tubeRadius: TUBE_RADIUS,
    tubeCurvePointCount: TUBE_CURVE_POINT_COUNT,
    tubeRenderSegments: TUBE_RENDER_SEGMENTS,
    tubeRenderRadialSegments: TUBE_RENDER_RADIAL_SEGMENTS,
    tubeColliderSegments: TUBE_COLLIDER_SEGMENTS,
    tubeColliderRadialSegments: TUBE_COLLIDER_RADIAL_SEGMENTS,
    eyeCapRadius: TUBE_RADIUS + 30,
    eyeFrameCount: EYE_FRAME_COUNT,
    eyeFrameFps: EYE_FRAME_FPS,
    highlightCount: HIGHLIGHT_COUNT,
    tubeJumpForce: 18,
    tubeJumpGravity: 38,
    tubeMaxJumpOffset: 18,
    reactPlayerFootOffset: 1.15,
    reactTubePlayerRadius: TUBE_RADIUS - 1.15,
    tubeMovementMultiplier: 4.8,
  },
  counts: {
    tubeGrassGroups: tubeGrassGroups.length,
    tubeGrassTufts: tubeGrassGroups.reduce((sum: number, group: unknown[]) => sum + group.length, 0),
    tubeLilies: tubeLilies.length,
    tubeFlowers: tubeFlowers.length,
    smallTubeFlowers: smallTubeFlowers.length,
    smallBloomParticles: smallBloomParticles.length,
    fireflies: fireflies.length,
    butterflies: butterflies.length,
    groundGrassTufts: groundGrass.length,
    groundLilies: groundLilies.length,
    groundLilyLights: groundLilyLights.length,
    eyeFrames: eyeFrames.length,
  },
  textures: {
    grass: "Textures/grass.png",
    stone: "Textures/stone.png",
    wall: "Textures/wall.png",
    ramp: "Textures/ramp.png",
    callaBloom: "Textures/calla-bloom.png",
    meadowOverlay: "Textures/meadow-overlay.png",
    groundBladeAlpha: "Textures/ground-blade-alpha.png",
    tubeGrassAlpha: "Textures/tube-grass-alpha.png",
  },
  eyeFrames,
  flora: {
    tubeGrass,
    tubeLilies,
    tubeFlowers,
    smallTubeFlowers,
    smallBloomParticles,
    fireflies,
    butterflies,
    groundGrass,
    groundLilies,
    groundLilyLights,
  },
  geometries: {
    tunnel: serializeGeometry(tunnelGeometry),
    tunnelCollider: serializeGeometry(tunnelColliderGeometry),
  },
};

tunnelGeometry.dispose();
tunnelColliderGeometry.dispose();

await emitBytes("runtime-layout.json", Buffer.from(`${JSON.stringify(document, null, 2)}\n`, "utf8"));
outputs.sort((left, right) => left.path.localeCompare(right.path, "en"));
const sourceHashes = Object.fromEntries(sourcePaths.map((sourcePath, index) => [
  path.relative(reactRoot, sourcePath).replaceAll("\\", "/"),
  sha256(sourceBytes[index]),
]));
const manifest = {
  schemaVersion: 1,
  generator: "Tools/bake-lily-coil-assets.mts",
  reactOracle: reactRoot,
  sourceSignature,
  sourceHashes,
  outputCount: outputs.length,
  outputs,
};
const manifestBytes = Buffer.from(`${JSON.stringify(manifest, null, 2)}\n`, "utf8");
if (await writeIfChanged(path.join(outputRoot, "source-manifest.json"), manifestBytes)) changedCount += 1;

console.log(JSON.stringify({
  status: "complete",
  outputRoot,
  changedCount,
  outputCount: outputs.length + 1,
  sourceSignature,
  counts: document.counts,
  manifestSha256: sha256(manifestBytes),
}, null, 2));

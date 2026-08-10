// Unity-only mountain perimeter profile. The authored React summit remains
// untouched inside the protected radius so cabins, villagers, the mineshaft,
// and all summit interactions keep their exact source coordinates.
export const UNITY_MOUNTAIN_CHUNK_X = 3;
export const UNITY_MOUNTAIN_CHUNK_Z = 0;
export const UNITY_MOUNTAIN_PROTECTED_RADIUS = 96;
export const UNITY_MOUNTAIN_RIM_PEAK_RADIUS = 142;
export const UNITY_MOUNTAIN_RIM_OUTER_RADIUS = 205;
export const UNITY_MOUNTAIN_BASE_OUTER_RADIUS = 720;

const smoothstep01 = (value: number) => {
  const clamped = Math.max(0, Math.min(1, value));
  return clamped * clamped * (3 - 2 * clamped);
};

export const getUnityMountainOuterRadius = (angle: number) =>
  UNITY_MOUNTAIN_BASE_OUTER_RADIUS +
  Math.sin(angle * 3 + 0.45) * 54 +
  Math.cos(angle * 7 - 0.2) * 30 +
  Math.sin(angle * 11 + 1.1) * 14;

export const getUnityMountainTargetLift = (localX: number, localZ: number) => {
  const radius = Math.hypot(localX, localZ);
  const angle = Math.atan2(localX, localZ);
  if (radius <= UNITY_MOUNTAIN_PROTECTED_RADIUS) return 214;

  const crestVariation = Math.sin(angle * 5 + 0.8) * 5.2 +
    Math.cos(angle * 9 - 0.35) * 2.8;
  const crestHeight = 250 + crestVariation;
  if (radius <= UNITY_MOUNTAIN_RIM_PEAK_RADIUS) {
    const progress = smoothstep01(
      (radius - UNITY_MOUNTAIN_PROTECTED_RADIUS) /
      (UNITY_MOUNTAIN_RIM_PEAK_RADIUS - UNITY_MOUNTAIN_PROTECTED_RADIUS),
    );
    return 214 + (crestHeight - 214) * progress;
  }

  const outerRimVariation = Math.sin(angle * 4 + 0.2) * 3.6 +
    Math.cos(angle * 8 - 0.65) * 2.1;
  const outerRimHeight = 218 + outerRimVariation;
  if (radius <= UNITY_MOUNTAIN_RIM_OUTER_RADIUS) {
    const progress = smoothstep01(
      (radius - UNITY_MOUNTAIN_RIM_PEAK_RADIUS) /
      (UNITY_MOUNTAIN_RIM_OUTER_RADIUS - UNITY_MOUNTAIN_RIM_PEAK_RADIUS),
    );
    return crestHeight + (outerRimHeight - crestHeight) * progress;
  }

  const outerRadius = getUnityMountainOuterRadius(angle);
  if (radius >= outerRadius) return 0;
  const progress = (radius - UNITY_MOUNTAIN_RIM_OUTER_RADIUS) /
    (outerRadius - UNITY_MOUNTAIN_RIM_OUTER_RADIUS);
  const envelope = Math.pow(Math.max(0, 1 - progress), 1.18);
  const broadAsymmetry = Math.sin(angle * 2 - 0.35) * 7.2 +
    Math.cos(angle * 4 + 0.7) * 4.4;
  return Math.max(0, (outerRimHeight + broadAsymmetry) * envelope);
};

export const getUnityMountainPerimeterLift = (
  worldX: number,
  worldZ: number,
  blockSize: number,
) => {
  const localX = worldX - UNITY_MOUNTAIN_CHUNK_X * blockSize;
  const localZ = worldZ - UNITY_MOUNTAIN_CHUNK_Z * blockSize;
  const radius = Math.hypot(localX, localZ);
  if (radius <= UNITY_MOUNTAIN_PROTECTED_RADIUS ||
      radius >= getUnityMountainOuterRadius(Math.atan2(localX, localZ))) return 0;
  return getUnityMountainTargetLift(localX, localZ);
};

export const getUnityMountainHeightDelta = (
  localX: number,
  localZ: number,
  sourceRadialLift: (radius: number) => number,
) => {
  const radius = Math.hypot(localX, localZ);
  if (radius <= UNITY_MOUNTAIN_PROTECTED_RADIUS) return 0;
  return getUnityMountainTargetLift(localX, localZ) - sourceRadialLift(radius);
};

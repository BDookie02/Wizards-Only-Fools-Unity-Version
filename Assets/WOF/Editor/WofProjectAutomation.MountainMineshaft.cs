using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static void CreateMountainMineshaft(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainVillageMineshaft");
            root.transform.SetParent(parent, false);
            var bottomY = document.baseHeight + document.constants.mineshaftBottomBaseOffset;
            var shaftWallHeight = Mathf.Max(32f, document.summitY - bottomY + 1.2f);
            var shaftWallY = bottomY + shaftWallHeight * 0.5f - 0.2f;
            var shaftMesh = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/OpenShaft48.asset",
                () => CreateMountainOpenCylinderMesh(
                    document.constants.mineshaftHoleRadius,
                    document.constants.mineshaftHoleRadius * 0.82f,
                    shaftWallHeight,
                    48));
            CreateMeshVisual("OpenShaftWall", root.transform, new Vector3(0f, shaftWallY, 0f), shaftMesh, materials.Shaft);

            CreateMountainWallDecor(root.transform, document, bottomY);
            CreateMountainMineshaftBottom(root.transform, document, bottomY);
            CreateMountainBanquet(root.transform, document, bottomY);
            CreateMountainMineshaftRim(root.transform, document);
            CreateMountainInterior(root.transform, document, materials);

            var summitColliderMesh = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/SummitCollider.asset",
                () => CreateDesertSerializedMesh("ExactMountainSummitCollider", document.geometries.summitCollider));
            var summitCollider = new GameObject("ExactSummitRingCollider");
            summitCollider.transform.SetParent(root.transform, false);
            summitCollider.AddComponent<MeshCollider>().sharedMesh = summitColliderMesh;

            var bottomCollider = new GameObject("MineshaftBottomCollider");
            bottomCollider.transform.SetParent(root.transform, false);
            CreateMountainBoxCollider(bottomCollider,
                new Vector3(0f, bottomY - 0.42f, 0f),
                new Vector3(document.constants.mineshaftBottomRadius * 1.64f, 0.84f,
                    document.constants.mineshaftBottomRadius * 1.64f));
            CreateMountainBanquetColliders(root.transform, document, bottomY);
        }

        private static void CreateMountainMineshaftBottom(
            Transform parent,
            WofMountainVillageDocument document,
            float bottomY)
        {
            CreateMountainFrustum("BottomFloor", parent, new Vector3(0f, bottomY - 0.28f, 0f),
                document.constants.mineshaftBottomRadius,
                document.constants.mineshaftBottomRadius * 0.96f,
                0.56f, 48, MountainMaterial("#342519"));
            CreateMountainRing("BottomFloorRing", parent,
                document.constants.mineshaftBottomRadius * 0.28f,
                document.constants.mineshaftBottomRadius * 0.96f,
                48,
                new Vector3(0f, bottomY + 0.04f, 0f),
                MountainMaterial("#5c4932", 0.58f));
            CreateMountainRing("BottomDarkRing", parent,
                document.constants.mineshaftBottomRadius * 0.7f,
                document.constants.mineshaftBottomRadius * 0.98f,
                48,
                new Vector3(0f, bottomY + 0.12f, 0f),
                MountainMaterial("#070504", 0.38f));
            CreateMountainRing("BottomCenter", parent, 0f,
                document.constants.mineshaftBottomRadius * 0.32f,
                36,
                new Vector3(0f, bottomY - 0.08f, 0f),
                MountainMaterial("#080605", 0.48f));
            foreach (var rock in document.opening.bottomRocks)
            {
                var visual = MountainBox($"BottomRock_{rock.index:00}", parent,
                    new Vector3(rock.x, bottomY + 0.12f, rock.z),
                    new Vector3(1.8f * rock.scale[0], 0.65f * rock.scale[1], 1.25f * rock.scale[2]),
                    MountainMaterial(rock.color));
                visual.transform.localRotation = ToMountainEuler(rock.rotation);
            }
        }

        private static void CreateMountainMineshaftRim(
            Transform parent,
            WofMountainVillageDocument document)
        {
            CreateMountainRing("MineshaftRimInner", parent,
                document.constants.mineshaftHoleRadius,
                document.constants.mineshaftRimMidRadius,
                48,
                new Vector3(0f, document.summitY + 0.42f, 0f),
                MountainMaterial("#3a281a"));
            CreateMountainRing("MineshaftRimOuter", parent,
                document.constants.mineshaftRimMidRadius,
                document.constants.mineshaftRimOuterRadius,
                48,
                new Vector3(0f, document.summitY + 0.5f, 0f),
                MountainMaterial("#796650"));
            CreateMountainRing("MineshaftRimInnerShadow", parent,
                document.constants.mineshaftHoleRadius,
                document.constants.mineshaftHoleRadius + 2.4f,
                48,
                new Vector3(0f, document.summitY + 0.58f, 0f),
                MountainMaterial("#050403", 0.58f));
            CreateMountainRing("MineshaftRimOuterShadow", parent,
                document.constants.mineshaftRimOuterRadius - 1.35f,
                document.constants.mineshaftRimOuterRadius,
                48,
                new Vector3(0f, document.summitY + 0.6f, 0f),
                MountainMaterial("#120c08", 0.42f));

            var exitAngle = document.exitBridge.frame.angle;
            foreach (var beam in document.opening.rimBeams)
            {
                if (MountainAbsoluteAngleDelta(exitAngle, beam.angle) < 0.38f) continue;
                var beamRoot = new GameObject($"RimBeam_{beam.index:00}");
                beamRoot.transform.SetParent(parent, false);
                beamRoot.transform.localPosition = new Vector3(beam.x, document.summitY + 1.02f, beam.z);
                beamRoot.transform.localRotation = ToMountainEuler(beam.rotation);
                MountainBox("Beam", beamRoot.transform, Vector3.zero, new Vector3(3.4f, 0.9f, 9.5f),
                    MountainMaterial(beam.index % 2 == 0 ? "#4b3421" : "#5e442d"));
                var beamZs = new[] { -2.9f, 0f, 2.9f };
                for (var index = 0; index < beamZs.Length; index++)
                {
                    MountainBox($"Band_{index}", beamRoot.transform, new Vector3(0f, 0.12f, beamZs[index]),
                        new Vector3(3.76f, 0.16f, 0.24f), MountainMaterial(index == 1 ? "#a67642" : "#24170f"));
                }
                MountainBox("Highlight", beamRoot.transform, new Vector3(0f, 0.54f, 0f),
                    new Vector3(0.22f, 0.12f, 8.2f), MountainMaterial("#d2a46a", 0.52f));
                MountainBox("BottomShadow", beamRoot.transform, new Vector3(0f, -0.52f, 0f),
                    new Vector3(3.22f, 0.16f, 8.9f), MountainMaterial("#060403", 0.62f));
            }

            CreateMountainExitBridge(parent, document);
            foreach (var frame in document.opening.supportFrames)
            {
                var frameRoot = new GameObject($"SupportFrame_{frame.index:00}");
                frameRoot.transform.SetParent(parent, false);
                frameRoot.transform.localRotation = ToMountainEuler(frame.rotation);
                foreach (var post in frame.posts)
                {
                    var postRoot = new GameObject($"SupportPost_{post.side}");
                    postRoot.transform.SetParent(frameRoot.transform, false);
                    postRoot.transform.localPosition = new Vector3(
                        post.positionOffset[0],
                        document.summitY + post.positionOffset[1],
                        post.positionOffset[2]);
                    postRoot.transform.localRotation = ToMountainEuler(post.rotation);
                    MountainBox("Post", postRoot.transform, Vector3.zero, new Vector3(2.3f, 15.5f, 2.3f),
                        MountainMaterial("#392719"));
                    CreateMountainVerticalTimberDetails(postRoot.transform, 15.5f, 2.3f, 2.3f, "#a67642", "#1d130d", "#8a5b34");
                }
                var topRoot = new GameObject("TopBeam");
                topRoot.transform.SetParent(frameRoot.transform, false);
                topRoot.transform.localPosition = new Vector3(
                    frame.topBeamPositionOffset[0],
                    document.summitY + frame.topBeamPositionOffset[1],
                    frame.topBeamPositionOffset[2]);
                MountainBox("Beam", topRoot.transform, Vector3.zero, new Vector3(27.5f, 2.4f, 2.6f),
                    MountainMaterial("#513821"));
                CreateMountainHorizontalTimberDetails(topRoot.transform, 27.5f, 2.4f, 2.6f, "#be8a4c");
                foreach (var cap in frame.snowCaps)
                {
                    MountainBox($"SnowCap_{cap.side}", topRoot.transform, ToMountainVector(cap.position),
                        new Vector3(5.1f, 0.22f, 1.88f), MountainMaterial("#e8f8ff", 0.7f));
                }
            }
        }

        private static void CreateMountainExitBridge(Transform parent, WofMountainVillageDocument document)
        {
            var bridge = document.exitBridge;
            var root = new GameObject("MountainMineshaftTopExitBridge");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(bridge.frame.x, bridge.y, bridge.frame.z);
            root.transform.localRotation = Quaternion.Euler(0f, bridge.frame.angle * Mathf.Rad2Deg, 0f);
            var width = document.constants.mineshaftExitBridgeWidth;
            MountainBox("Body", root.transform, Vector3.zero,
                new Vector3(width, 0.58f, bridge.frame.length), MountainMaterial("#5d3f28"));
            MountainBox("Top", root.transform, new Vector3(0f, 0.42f, 0f),
                new Vector3(width * 0.88f, 0.2f, bridge.frame.length * 0.96f), MountainMaterial("#8a653f"));
            foreach (var edge in bridge.details.edgeShadows)
            {
                MountainBox($"EdgeShadow_{edge.side}", root.transform, ToMountainVector(edge.position),
                    new Vector3(0.24f, 0.12f, bridge.frame.length * 0.98f), MountainMaterial("#080504", 0.72f));
            }
            foreach (var gap in bridge.details.darkGaps)
            {
                MountainBox($"DarkGap_{gap.index:00}", root.transform, new Vector3(0f, 0.8f, gap.z),
                    new Vector3(width * 0.84f, 0.08f, 0.14f), MountainMaterial("#090604", 0.56f));
            }
            foreach (var plank in bridge.details.planks)
            {
                MountainBox($"Plank_{plank.index:00}", root.transform, new Vector3(0f, 0.64f, plank.z),
                    new Vector3(width + 0.8f, 0.16f, 1.45f), MountainMaterial(plank.color));
            }
            foreach (var rail in bridge.details.sideRails)
            {
                MountainBox($"Rail_{rail.side}", root.transform, ToMountainVector(rail.position),
                    new Vector3(0.36f, 0.36f, bridge.frame.length * 0.92f), MountainMaterial("#2b1c12"));
                foreach (var post in rail.posts)
                {
                    MountainBox($"Post_{post.side}_{post.index:00}", root.transform, ToMountainVector(post.position),
                        new Vector3(0.46f, 1.34f, 0.46f), MountainMaterial(post.color));
                }
            }
            foreach (var support in bridge.details.supports)
            {
                var supportRoot = new GameObject($"Support_{support.key}");
                supportRoot.transform.SetParent(root.transform, false);
                supportRoot.transform.localPosition = ToMountainVector(support.position);
                supportRoot.transform.localRotation = ToMountainEuler(support.rotation);
                MountainBox("Beam", supportRoot.transform, Vector3.zero,
                    new Vector3(bridge.details.supportLength, 0.42f, 0.6f), MountainMaterial("#3a2719"));
                CreateMountainHorizontalTimberDetails(supportRoot.transform, bridge.details.supportLength, 0.42f, 0.6f, "#8a5b34");
            }
            CreateMountainLantern(root.transform, ToMountainVector(bridge.details.lanternPosition), 0.7f, true);
            CreateMountainBoxCollider(root, Vector3.zero, new Vector3(width, 0.72f, bridge.frame.length));
        }

        private static void CreateMountainInterior(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainMineshaftWallHuts");
            root.transform.SetParent(parent, false);
            for (var index = 0; index < document.layout.interiorHuts.Length; index++)
            {
                var hut = document.layout.interiorHuts[index];
                var platform = document.interiorPlatforms[index];
                CreateMountainCatwalk(root.transform, document, hut, platform);
                CreateMountainInteriorPlatform(root.transform, hut, platform);
                CreateMountainCabin(root.transform, hut, hut.y, true, platform, materials);
            }
            for (var index = 0; index < document.layout.interiorLadders.Length; index++)
                CreateMountainLadder(root.transform, document.layout.interiorLadders[index], document.ladderDetails[index]);
        }

        private static void CreateMountainCatwalk(
            Transform parent,
            WofMountainVillageDocument document,
            WofMountainInteriorHutRecord hut,
            WofMountainInteriorPlatformRecord platform)
        {
            var root = new GameObject(hut.key + "_Catwalk");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, hut.y + 0.08f, 0f);
            CreateMountainRing("Base", root.transform,
                document.constants.mineshaftCatwalkInnerRadius,
                document.constants.mineshaftCatwalkOuterRadius,
                64, Vector3.zero, MountainMaterial("#47311f"));
            CreateMountainRing("Top", root.transform,
                document.constants.mineshaftCatwalkInnerRadius + 1.1f,
                document.constants.mineshaftCatwalkOuterRadius - 1.2f,
                64, new Vector3(0f, 0.05f, 0f), MountainMaterial("#6b4a2d"));
            CreateMountainRing("InnerShadow", root.transform,
                document.constants.mineshaftCatwalkInnerRadius,
                document.constants.mineshaftCatwalkInnerRadius + 0.62f,
                64, new Vector3(0f, 0.18f, 0f), MountainMaterial("#070504", 0.7f));
            CreateMountainRing("OuterShadow", root.transform,
                document.constants.mineshaftCatwalkOuterRadius - 0.58f,
                document.constants.mineshaftCatwalkOuterRadius,
                64, new Vector3(0f, 0.2f, 0f), MountainMaterial("#0d0805", 0.54f));
            foreach (var plank in document.catwalk.planks)
            {
                var visual = MountainBox($"Plank_{plank.index:00}", root.transform, ToMountainVector(plank.position),
                    new Vector3(1.15f, 0.24f,
                        document.constants.mineshaftCatwalkOuterRadius - document.constants.mineshaftCatwalkInnerRadius + 0.8f),
                    MountainMaterial(plank.index % 2 == 0 ? "#7a5635" : "#5d3f28"));
                visual.transform.localRotation = ToMountainEuler(plank.rotation);
            }
            foreach (var gap in document.catwalk.darkGaps)
            {
                var visual = MountainBox($"DarkGap_{gap.index:00}", root.transform, ToMountainVector(gap.position),
                    new Vector3(0.16f, 0.08f,
                        document.constants.mineshaftCatwalkOuterRadius - document.constants.mineshaftCatwalkInnerRadius + 1f),
                    MountainMaterial("#080504", 0.58f));
                visual.transform.localRotation = ToMountainEuler(gap.rotation);
            }
            foreach (var edge in document.catwalk.edgeBlocks)
            {
                if (Array.IndexOf(platform.guardRailOpenings.visibleEdgeBlockIndices, edge.index) < 0) continue;
                var visual = MountainBox($"EdgeBlock_{edge.index:00}", root.transform, ToMountainVector(edge.position),
                    new Vector3(0.68f, 0.34f, 0.54f), MountainMaterial(edge.index % 3 == 0 ? "#9b6a3b" : "#2f1e13"));
                visual.transform.localRotation = ToMountainEuler(edge.rotation);
            }
            foreach (var post in document.catwalk.guardPosts)
            {
                if (Array.IndexOf(platform.guardRailOpenings.visibleGuardPostIndices, post.index) < 0) continue;
                var visual = MountainBox($"GuardPost_{post.index:00}", root.transform, ToMountainVector(post.position),
                    new Vector3(0.42f, 1.48f, 0.42f), MountainMaterial(post.index % 2 == 0 ? "#2b1c12" : "#4d301b"));
                visual.transform.localRotation = ToMountainEuler(post.rotation);
            }
            var railHeights = new[] { 0.86f, 1.55f, 2.08f };
            for (var railRow = 0; railRow < railHeights.Length; railRow++)
            {
                foreach (var rail in document.catwalk.railSegments)
                {
                    if (Array.IndexOf(platform.guardRailOpenings.visibleRailSegmentIndices, rail.index) < 0) continue;
                    var position = ToMountainVector(rail.position);
                    position.y = railHeights[railRow];
                    var visual = MountainBox($"Rail_{railRow}_{rail.index:00}", root.transform, position,
                        new Vector3(document.catwalk.centerGuardRailSegmentLength, 0.24f, railRow == 0 ? 0.32f : 0.28f),
                        MountainMaterial(railRow == 1 ? "#8d6238" : "#24170f"));
                    visual.transform.localRotation = ToMountainEuler(rail.rotation);
                }
            }
            foreach (var pole in platform.catwalkLightPoles)
            {
                var poleRoot = new GameObject($"LightPole_{pole.index}");
                poleRoot.transform.SetParent(root.transform, false);
                poleRoot.transform.localPosition = ToMountainVector(pole.position);
                poleRoot.transform.localRotation = ToMountainEuler(pole.rotation);
                MountainBox("Post", poleRoot.transform, new Vector3(0f, 1.44f, 0f), new Vector3(0.34f, 2.88f, 0.34f), MountainMaterial("#25170e"));
                MountainBox("Arm", poleRoot.transform, new Vector3(-0.62f, 2.78f, 0f), new Vector3(1.36f, 0.26f, 0.26f), MountainMaterial("#4f321c"));
                MountainBox("Cord", poleRoot.transform, new Vector3(-1.18f, 2.48f, 0f), new Vector3(0.16f, 0.58f, 0.16f), MountainMaterial("#1b120c"));
                CreateMountainLantern(poleRoot.transform, new Vector3(-1.18f, 1.44f, 0f), 0.62f, false);
            }

            foreach (var segment in document.catwalkColliders.segments)
            {
                var colliderObject = new GameObject($"Collider_{segment.index:00}");
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.transform.localPosition = ToMountainVector(segment.positionOffset);
                colliderObject.transform.localRotation = ToMountainEuler(segment.rotation);
                CreateMountainBoxCollider(colliderObject, Vector3.zero,
                    new Vector3(document.catwalkColliders.args[0] * 2f,
                        document.catwalkColliders.args[1] * 2f,
                        document.catwalkColliders.args[2] * 2f));
            }
        }

        private static void CreateMountainInteriorPlatform(
            Transform parent,
            WofMountainInteriorHutRecord hut,
            WofMountainInteriorPlatformRecord platform)
        {
            var root = new GameObject(hut.key + "_Platform");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(hut.localX, hut.y, hut.localZ);
            root.transform.localRotation = Quaternion.Euler(0f, hut.rotation * Mathf.Rad2Deg, 0f);
            foreach (var piece in platform.pieces)
            {
                MountainBox("Base_" + piece.key, root.transform, new Vector3(piece.centerX, 0.18f, platform.platformZ),
                    new Vector3(piece.width, 0.52f, hut.platformDepth), MountainMaterial("#3f2b1c"));
                CreateMountainBoxCollider(root, new Vector3(piece.centerX, 0f, platform.platformZ),
                    new Vector3(piece.width, 0.9f, hut.platformDepth));
            }
            foreach (var piece in platform.topPieces)
            {
                MountainBox("Top_" + piece.key, root.transform, new Vector3(piece.centerX, 0.56f, platform.platformZ),
                    new Vector3(piece.width, 0.22f, hut.platformDepth * 0.88f), MountainMaterial("#6d4a2e"));
            }
            foreach (var details in platform.details.pieces)
            {
                foreach (var shadow in details.sideShadows)
                    MountainBox($"SideShadow_{details.key}_{shadow.side}", root.transform, ToMountainVector(shadow.position),
                        new Vector3(0.2f, 0.12f, hut.platformDepth * 0.92f), MountainMaterial("#080504", 0.74f));
                foreach (var groove in details.plankGrooves)
                    MountainBox($"Groove_{details.key}_{groove.index}", root.transform, ToMountainVector(groove.position),
                        new Vector3(groove.width, 0.07f, 0.09f), MountainMaterial(groove.color));
                MountainBox("FrontRail_" + details.key, root.transform, ToMountainVector(details.frontRail.position),
                    new Vector3(details.frontRail.width, 0.22f, 0.32f), MountainMaterial("#8b6239"));
                MountainBox("BackRail_" + details.key, root.transform, ToMountainVector(details.backRail.position),
                    new Vector3(details.backRail.width, 0.18f, 0.24f), MountainMaterial("#2c1d13"));
                foreach (var bolt in details.bolts)
                    MountainBox($"Bolt_{details.key}_{bolt.side}", root.transform, ToMountainVector(bolt.position),
                        new Vector3(0.28f, 0.1f, 0.28f), MountainMaterial("#d0a05d"));
            }
            foreach (var support in platform.details.supports)
            {
                var supportRoot = new GameObject($"PlatformSupport_{support.side}");
                supportRoot.transform.SetParent(root.transform, false);
                supportRoot.transform.localPosition = ToMountainVector(support.position);
                supportRoot.transform.localRotation = ToMountainEuler(support.rotation);
                MountainBox("Post", supportRoot.transform, Vector3.zero, new Vector3(0.58f, 4.8f, 0.58f), MountainMaterial("#2d1e14"));
                CreateMountainVerticalTimberDetails(supportRoot.transform, 4.8f, 0.58f, 0.58f, "#8a5b34");
            }
            CreateMountainLightPole(root.transform, ToMountainVector(platform.details.lightPole.position), platform.details.lightPole.direction, false);
        }

        private static void CreateMountainLadder(
            Transform parent,
            WofMountainLadderRecord ladder,
            WofMountainLadderDetailRecord detail)
        {
            var root = new GameObject(ladder.key);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(ladder.localX, ladder.startY, ladder.localZ);
            root.transform.localRotation = Quaternion.Euler(0f, ladder.rotation * Mathf.Rad2Deg, 0f);
            MountainBox("LeftRail", root.transform, new Vector3(-ladder.width * 0.5f, detail.height * 0.5f, 0f),
                new Vector3(0.34f, detail.height, 0.28f), MountainMaterial("#26180f"));
            MountainBox("RightRail", root.transform, new Vector3(ladder.width * 0.5f, detail.height * 0.5f, 0f),
                new Vector3(0.34f, detail.height, 0.28f), MountainMaterial("#26180f"));
            foreach (var rung in detail.details.rungs)
                MountainBox($"Rung_{rung.index:00}", root.transform, ToMountainVector(rung.position),
                    new Vector3(ladder.width + 0.55f, 0.24f, 0.32f), MountainMaterial(rung.color));
            MountainBox("BackShadow", root.transform, new Vector3(0f, detail.height * 0.5f, -0.12f),
                new Vector3(ladder.width + 0.9f, detail.height * 0.94f, 0.08f), MountainMaterial("#050403", 0.22f));
            foreach (var side in new[] { -1f, 1f })
                MountainBox($"RailHighlight_{side}", root.transform,
                    new Vector3(side * ladder.width * 0.5f + side * 0.08f, detail.height * 0.5f, 0.2f),
                    new Vector3(0.1f, detail.height * 0.94f, 0.08f), MountainMaterial("#8a5b34"));
            foreach (var wrap in detail.details.wraps)
            {
                MountainBox($"WrapLeft_{wrap.index:00}", root.transform, ToMountainVector(wrap.leftPosition),
                    new Vector3(0.64f, 0.3f, 0.42f), MountainMaterial(wrap.color));
                MountainBox($"WrapRight_{wrap.index:00}", root.transform, ToMountainVector(wrap.rightPosition),
                    new Vector3(0.64f, 0.3f, 0.42f), MountainMaterial(wrap.color));
            }
            foreach (var edge in detail.details.brightEdges)
                MountainBox($"BrightEdge_{edge.index:00}", root.transform, ToMountainVector(edge.position),
                    new Vector3(ladder.width + 0.18f, 0.07f, 0.1f), MountainMaterial("#b27a42"));
            foreach (var edge in detail.details.darkEdges)
                MountainBox($"DarkEdge_{edge.index:00}", root.transform, ToMountainVector(edge.position),
                    new Vector3(ladder.width + 0.42f, 0.08f, 0.12f), MountainMaterial("#090604", 0.72f));
            MountainBox("TopCap", root.transform, new Vector3(0f, detail.height + 0.34f, 0f),
                new Vector3(ladder.width + 1.2f, 0.46f, 0.46f), MountainMaterial("#6f5131"));
            MountainBox("BottomCap", root.transform, new Vector3(0f, 0.34f, 0f),
                new Vector3(ladder.width + 1.2f, 0.46f, 0.46f), MountainMaterial("#6f5131"));
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, detail.height * 0.5f, 0f);
            trigger.size = new Vector3(ladder.width + 1.8f, detail.height, 4.2f);
            root.AddComponent<WofMountainLadderZone>().Configure(ladder.key);
        }

        private static void CreateMountainWallDecor(
            Transform parent,
            WofMountainVillageDocument document,
            float bottomY)
        {
            var root = new GameObject("MineshaftWallDecor");
            root.transform.SetParent(parent, false);
            foreach (var light in document.wallDecor.ropeLights)
            {
                var item = new GameObject(light.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = ToMountainVector(light.position);
                item.transform.localRotation = ToMountainEuler(light.rotation);
                var scale = light.bulbScale;
                MountainBox("Cord", item.transform, new Vector3(0f, 0f, -0.14f),
                    new Vector3(2.08f * scale, 0.24f * scale, 0.16f), MountainMaterial("#160d08"));
                foreach (var side in new[] { -1f, 1f })
                    MountainBox($"Socket_{side}", item.transform, new Vector3(side * 0.66f * scale, 0f, -0.18f),
                        new Vector3(0.22f * scale, 0.36f * scale, 0.18f), MountainMaterial("#4f321f"));
                MountainBox("Bulb", item.transform, new Vector3(0f, 0f, -0.28f),
                    Vector3.one * 0.92f * scale, MountainMaterial(light.glowColor, 0.96f));
                MountainBox("GlowA", item.transform, new Vector3(0f, 0f, -0.36f),
                    Vector3.one * 2.75f * scale, MountainMaterial(light.glowColor, 0.28f));
                MountainBox("GlowB", item.transform, new Vector3(0f, 0f, -0.42f),
                    Vector3.one * 4.1f * scale, MountainMaterial(light.glowColor, 0.1f));
                if (light.hasLight)
                {
                    var point = new GameObject("PointLight");
                    point.transform.SetParent(item.transform, false);
                    point.transform.localPosition = new Vector3(0f, 0f, -1.1f);
                    var component = point.AddComponent<Light>();
                    component.type = LightType.Point;
                    component.color = HexColor(light.glowColor);
                    component.intensity = 3.6f;
                    component.range = 20f;
                    component.shadows = LightShadows.None;
                }
            }
            foreach (var lantern in document.wallDecor.lanterns)
                CreateMountainWallLantern(root.transform, lantern);
            foreach (var painting in document.wallDecor.paintings)
                CreateMountainPainting(root.transform, painting);
        }

        private static void CreateMountainWallLantern(Transform parent, WofMountainWallLanternRecord record)
        {
            var root = new GameObject($"WallLantern_{record.index:00}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = ToMountainVector(record.position);
            root.transform.localRotation = ToMountainEuler(record.rotation);
            MountainBox("WallBeam", root.transform, new Vector3(0f, 0.42f, -0.08f), new Vector3(2.9f, 0.68f, 0.34f), MountainMaterial("#1b1009"));
            MountainBox("WallBracket", root.transform, new Vector3(0f, 0.14f, -0.92f), new Vector3(2.54f, 0.34f, 1.96f), MountainMaterial(record.index % 2 == 0 ? "#53331d" : "#342113"));
            MountainBox("Drop", root.transform, new Vector3(0f, -0.84f, -1.84f), new Vector3(0.24f, 1.62f, 0.24f), MountainMaterial("#0f0906"));
            CreateMountainLantern(root.transform, new Vector3(0f, -2.9f, -1.84f), 1.02f, record.withLight, 1.55f, 8.8f, 34f);
        }

        private static void CreateMountainPainting(Transform parent, WofMountainWallPaintingRecord record)
        {
            var root = new GameObject($"VillagerPainting_{record.index:00}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = ToMountainVector(record.position);
            root.transform.localRotation = ToMountainEuler(record.rotation);
            var frameColor = record.variant % 2 == 0 ? "#5c3a20" : "#2e1d12";
            var canvasColors = new[] { "#263345", "#473328", "#2f4638", "#3b2d4f" };
            var floorColors = new[] { "#6d4a2e", "#4e3826", "#3f4f37", "#7b5730" };
            MountainBox("Outer", root.transform, Vector3.zero, new Vector3(6.8f, 4.92f, 0.28f), MountainMaterial("#0b0705"));
            MountainBox("Frame", root.transform, new Vector3(0f, 0f, -0.08f), new Vector3(6.28f, 4.42f, 0.18f), MountainMaterial(frameColor));
            MountainBox("Canvas", root.transform, new Vector3(0f, 0f, -0.2f), new Vector3(5.38f, 3.48f, 0.12f), MountainMaterial(canvasColors[record.variant % 4]));
            MountainBox("PaintedFloor", root.transform, new Vector3(0f, -1.18f, -0.29f), new Vector3(5.42f, 1.1f, 0.08f), MountainMaterial(floorColors[record.variant % 4]));
            var moon = record.variant % 3 == 0;
            MountainBox("MoonSun", root.transform, new Vector3(moon ? -1.92f : 1.78f, 1.08f, -0.31f), Vector3.one * 0.64f,
                MountainMaterial(moon ? "#f4e5b0" : "#ffb347", 0.9f));
            if (record.variant % 2 == 0)
            {
                CreateMountainPaintedVillager(root.transform, -1.55f, -0.78f, 0.94f, "#8e1e24", "#d7a548");
                CreateMountainPaintedVillager(root.transform, 0f, -0.72f, 1.08f, "#3a6b78", "#6f4528");
                CreateMountainPaintedVillager(root.transform, 1.48f, -0.82f, 0.88f, "#5c6f35", "#a67642");
            }
            else
            {
                CreateMountainPaintedVillager(root.transform, -0.72f, -0.88f, 1.22f, "#6d4a8e", "#d7a548");
                MountainBox("PaintedThrone", root.transform, new Vector3(1.28f, -0.34f, -0.34f),
                    new Vector3(0.82f, 1.94f, 0.08f), MountainMaterial("#2a1a10"));
                MountainBox("PaintedThroneCrown", root.transform, new Vector3(1.28f, 0.7f, -0.32f),
                    new Vector3(1.24f, 0.34f, 0.08f), MountainMaterial("#d7a548"));
                MountainBox("PaintedThroneCushion", root.transform, new Vector3(1.28f, 1f, -0.3f),
                    new Vector3(0.74f, 0.58f, 0.08f), MountainMaterial("#9f2428"));
            }
            foreach (var x in new[] { -2.56f, 2.56f })
                MountainBox($"Pin_{x}", root.transform, new Vector3(x, 1.82f, -0.38f),
                    new Vector3(0.22f, 0.22f, 0.08f), MountainMaterial("#d7a548"));
            for (var index = 0; index < 4; index++)
                MountainBox($"Highlight_{index}", root.transform,
                    new Vector3(-2.1f + index * 1.35f, 1.54f - index % 2 * 0.36f, -0.36f),
                    new Vector3(0.92f, 0.08f, 0.06f), MountainMaterial("#f6e2a8", 0.26f));
        }

        private static void CreateMountainPaintedVillager(Transform parent, float x, float y, float scale, string body, string hat)
        {
            var root = new GameObject("PaintedVillager");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, y, -0.36f);
            root.transform.localScale = new Vector3(scale, scale, 1f);
            MountainBox("Head", root.transform, new Vector3(0f, 0.74f, 0f), new Vector3(0.46f, 0.42f, 0.08f), MountainMaterial("#c88d68"));
            MountainBox("Body", root.transform, new Vector3(0f, 0.28f, 0.02f), new Vector3(0.58f, 0.74f, 0.08f), MountainMaterial(body));
            MountainBox("LeftArm", root.transform, new Vector3(-0.4f, 0.28f, 0.02f),
                new Vector3(0.18f, 0.58f, 0.08f), MountainMaterial("#3a2415"));
            MountainBox("RightArm", root.transform, new Vector3(0.4f, 0.28f, 0.02f),
                new Vector3(0.18f, 0.58f, 0.08f), MountainMaterial("#3a2415"));
            MountainBox("HatBrim", root.transform, new Vector3(0f, 1.06f, 0.03f), new Vector3(0.72f, 0.22f, 0.08f), MountainMaterial(hat));
            MountainBox("HatTop", root.transform, new Vector3(0f, 1.24f, 0.04f), new Vector3(0.46f, 0.28f, 0.08f), MountainMaterial(hat));
            MountainBox("LeftEye", root.transform, new Vector3(-0.1f, 0.82f, 0.06f),
                new Vector3(0.08f, 0.08f, 0.06f), MountainMaterial("#090604"));
            MountainBox("RightEye", root.transform, new Vector3(0.14f, 0.82f, 0.06f),
                new Vector3(0.08f, 0.08f, 0.06f), MountainMaterial("#090604"));
        }

        private static void CreateMountainLantern(
            Transform parent,
            Vector3 position,
            float scale,
            bool withLight,
            float glowScale = 1f,
            float lightIntensity = 4.8f,
            float lightDistance = 22f)
        {
            var root = new GameObject("RetroMineshaftLantern");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * scale;
            var sphere = GetOrCreateMeshAsset(MountainGeometryRoot + "/LanternSphere8x6.asset", () => CreateUvSphereMesh(1f, 8, 6));
            var glowA = CreateMeshVisual("GlowA", root.transform, new Vector3(0f, 0.78f, 0.07f), sphere, MountainMaterial("#ff9d36", 0.28f));
            glowA.transform.localScale = Vector3.one * 1.28f * glowScale;
            var glowB = CreateMeshVisual("GlowB", root.transform, new Vector3(0f, 0.78f, 0.12f), sphere, MountainMaterial("#ffd56f", 0.38f));
            glowB.transform.localScale = Vector3.one * 0.74f * glowScale;
            MountainBox("Case", root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(0.82f, 0.92f, 0.82f), MountainMaterial("#2a1b12"));
            MountainBox("Light", root.transform, new Vector3(0f, 0.78f, 0.04f), new Vector3(0.52f, 0.62f, 0.64f), MountainMaterial("#ffc15d", 0.96f));
            MountainBox("Glint", root.transform, new Vector3(0f, 0.78f, 0.08f), new Vector3(0.2f, 0.72f, 0.72f), MountainMaterial("#fff0b2", 0.72f));
            MountainBox("Top", root.transform, new Vector3(0f, 1.34f, 0f), new Vector3(1.02f, 0.22f, 1.02f), MountainMaterial("#51331f"));
            MountainBox("Bottom", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.92f, 0.22f, 0.92f), MountainMaterial("#51331f"));
            MountainBox("Hook", root.transform, new Vector3(0f, 1.63f, 0f), new Vector3(0.18f, 0.42f, 0.18f), MountainMaterial("#1b120c"));
            if (!withLight) return;
            var lightObject = new GameObject("PointLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.84f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HexColor("#ffb65b");
            light.intensity = lightIntensity;
            light.range = lightDistance;
            light.shadows = LightShadows.None;
        }

        private static void CreateMountainLightPole(Transform parent, Vector3 position, int direction, bool withLight)
        {
            var root = new GameObject("MountainMineshaftLightPole");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            MountainBox("Post", root.transform, new Vector3(0f, 1.72f, 0f), new Vector3(0.42f, 3.44f, 0.42f), MountainMaterial("#22150d"));
            MountainBox("Arm", root.transform, new Vector3(direction * 0.68f, 3.28f, 0f), new Vector3(1.62f, 0.32f, 0.32f), MountainMaterial("#392414"));
            MountainBox("Cord", root.transform, new Vector3(direction * 1.38f, 2.94f, 0f), new Vector3(0.18f, 0.7f, 0.18f), MountainMaterial("#1b120c"));
            CreateMountainLantern(root.transform, new Vector3(direction * 1.38f, 1.72f, 0f), 0.78f, withLight);
        }

        private static void CreateMountainBanquet(Transform parent, WofMountainVillageDocument document, float bottomY)
        {
            var root = new GameObject("MineshaftRoyalBanquet");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, bottomY, 0f);
            CreateMountainRing("BanquetShadow", root.transform, 0f, document.constants.mineshaftBottomRadius * 0.62f, 40,
                new Vector3(0f, 0.09f, 0f), MountainMaterial("#120b07", 0.28f));
            foreach (var entry in document.banquet.bottomLights)
            {
                var lightRoot = new GameObject($"BottomLight_{entry.index:00}");
                lightRoot.transform.SetParent(root.transform, false);
                lightRoot.transform.localPosition = ToMountainVector(entry.position);
                lightRoot.transform.localRotation = ToMountainEuler(entry.rotation);
                CreateMountainRing("Glow", lightRoot.transform, 0f, 3.4f, 12, new Vector3(0f, 0.06f, 0f), MountainMaterial("#ff9d36", 0.24f));
                CreateMountainFrustum("Base", lightRoot.transform, new Vector3(0f, 0.12f, 0f), 1.55f, 1.85f, 0.24f, 8, MountainMaterial("#20140d"));
                CreateMountainFrustum("Body", lightRoot.transform, new Vector3(0f, 0.42f, 0f), 1.1f, 1.35f, 0.46f, 8, MountainMaterial(entry.bodyColor));
                MountainBox("Post", lightRoot.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.42f, 1.35f, 0.42f), MountainMaterial("#1a100a"));
                CreateMountainLantern(lightRoot.transform, new Vector3(0f, 2.08f, 0f), 0.72f, entry.withLight);
            }
            CreateMountainBanquetTable(root.transform, document.banquet.table);
            foreach (var chair in document.banquet.chairs) CreateMountainBanquetChair(root.transform, chair);
            CreateMountainThrone(root.transform);
        }

        private static void CreateMountainBanquetTable(Transform parent, WofMountainBanquetTableRecord table)
        {
            var root = new GameObject("RoyalBanquetTable");
            root.transform.SetParent(parent, false);
            CreateMountainFrustum("Pedestal", root.transform, new Vector3(0f, 1.2f, 0f), 1.55f, 2.1f, 1.75f, 12, MountainMaterial("#3a2415"));
            CreateMountainFrustum("Table", root.transform, new Vector3(0f, 1.78f, 0f), table.radius, table.radius * 0.96f, 0.58f, 20, MountainMaterial("#5e3a20"));
            CreateMountainFrustum("Rim", root.transform, new Vector3(0f, 2.14f, 0f), table.radius * 1.05f, table.radius * 1.05f, 0.22f, 20, MountainMaterial("#2a1a10"));
            foreach (var plank in table.planks)
                MountainBox($"Plank_{plank.index:00}", root.transform, new Vector3(0f, 2.28f, plank.z),
                    new Vector3(plank.width, 0.08f, 0.32f), MountainMaterial(plank.color, 0.76f));
            foreach (var leg in table.legs)
                MountainBox($"Leg_{leg.index:00}", root.transform, ToMountainVector(leg.position),
                    new Vector3(0.42f, 1.55f, 0.42f), MountainMaterial("#21140c"));
            var sphere = GetOrCreateMeshAsset(MountainGeometryRoot + "/BanquetSphere10x6.asset", () => CreateUvSphereMesh(1f, 10, 6));
            var roast = CreateMeshVisual("Roast", root.transform, new Vector3(0f, 2.7f, 0f), sphere, MountainMaterial("#9a4f2c"));
            roast.transform.localScale = new Vector3(2.35f, 0.52f, 1.22f);
            foreach (var x in new[] { -1.86f, 1.86f })
            {
                var bone = new GameObject(x < 0f ? "RoastBoneLeft" : "RoastBoneRight");
                bone.transform.SetParent(root.transform, false);
                bone.transform.localPosition = new Vector3(x, 2.72f, 0.12f);
                bone.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreateMountainFrustum("Bone", bone.transform, Vector3.zero, 0.16f, 0.16f, 1.42f, 8,
                    MountainMaterial("#f1d8a0"));
            }
            foreach (var bread in table.breads)
            {
                var breadRoot = new GameObject($"Bread_{bread.index:00}");
                breadRoot.transform.SetParent(root.transform, false);
                breadRoot.transform.localPosition = ToMountainVector(bread.position);
                breadRoot.transform.localRotation = ToMountainEuler(bread.rotation);
                var item = CreateMeshVisual("Loaf", breadRoot.transform, Vector3.zero, sphere, MountainMaterial(bread.color));
                item.transform.localScale = new Vector3(1.18f, 0.36f, 0.62f);
                MountainBox("Score", breadRoot.transform, new Vector3(0f, 0.12f, 0.18f),
                    new Vector3(1.4f, 0.08f, 0.12f), MountainMaterial("#fff0b2", 0.44f));
            }
            foreach (var bowl in table.fruitBowls)
            {
                var bowlRoot = new GameObject($"FruitBowl_{bowl.index:00}");
                bowlRoot.transform.SetParent(root.transform, false);
                bowlRoot.transform.localPosition = ToMountainVector(bowl.position);
                CreateMountainFrustum("Bowl", bowlRoot.transform, new Vector3(0f, -0.04f, 0f), 0.86f, 0.7f, 0.18f, 10, MountainMaterial("#2b1a0f"));
                foreach (var fruit in bowl.fruits)
                {
                    var visual = CreateMeshVisual($"Fruit_{fruit.index:00}", bowlRoot.transform, ToMountainVector(fruit.position), sphere, MountainMaterial(fruit.color));
                    visual.transform.localScale = Vector3.one * 0.24f;
                }
            }
            foreach (var plate in table.plates)
            {
                var plateRoot = new GameObject($"Place_{plate.index:00}");
                plateRoot.transform.SetParent(root.transform, false);
                plateRoot.transform.localPosition = ToMountainVector(plate.position);
                plateRoot.transform.localRotation = ToMountainEuler(plate.rotation);
                CreateMountainFrustum("Plate", plateRoot.transform, Vector3.zero, 0.82f, 0.9f, 0.08f, 12, MountainMaterial("#d7cab2"));
                var food = CreateMeshVisual("Food", plateRoot.transform, new Vector3(0f, 0.09f, -0.05f), sphere, MountainMaterial(plate.foodColor));
                food.transform.localScale = new Vector3(0.48f, 0.12f, 0.32f);
                CreateMountainFrustum("Cup", plateRoot.transform, new Vector3(0.78f, 0.2f, -0.18f), 0.16f, 0.22f, 0.42f, 8, MountainMaterial("#b58b45"));
            }
            foreach (var candle in table.candles)
            {
                var candleRoot = new GameObject($"Candle_{candle.index:00}");
                candleRoot.transform.SetParent(root.transform, false);
                candleRoot.transform.localPosition = ToMountainVector(candle.position);
                CreateMountainFrustum("Wax", candleRoot.transform, new Vector3(0f, 0.3f, 0f), 0.16f, 0.16f, 0.6f, 8, MountainMaterial("#f6e2a8"));
                var flame = CreateMeshVisual("Flame", candleRoot.transform, new Vector3(0f, 0.74f, 0f), sphere, MountainMaterial("#ffb347", 0.84f));
                flame.transform.localScale = Vector3.one * 0.34f;
            }
        }

        private static void CreateMountainBanquetChair(Transform parent, WofMountainBanquetChairRecord chair)
        {
            var root = new GameObject($"BanquetChair_{chair.index:00}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = ToMountainVector(chair.position);
            root.transform.localRotation = ToMountainEuler(chair.rotation);
            MountainBox("Seat", root.transform, new Vector3(0f, 0.72f, 0f), new Vector3(2f, 0.38f, 1.72f), MountainMaterial(chair.seatColor));
            MountainBox("Cushion", root.transform, new Vector3(0f, 0.96f, -0.12f), new Vector3(1.62f, 0.22f, 1.2f), MountainMaterial("#8e1e24"));
            MountainBox("Back", root.transform, new Vector3(0f, 1.86f, 0.82f), new Vector3(2.18f, 2.32f, 0.42f), MountainMaterial("#3d2617"));
            MountainBox("BackPanel", root.transform, new Vector3(0f, 2f, 1.08f), new Vector3(1.54f, 1.74f, 0.18f), MountainMaterial("#7b5332"));
            foreach (var side in new[] { -1.24f, 1.24f })
                MountainBox($"Arm_{side}", root.transform, new Vector3(side, 1.12f, -0.08f), new Vector3(0.32f, 0.98f, 1.74f), MountainMaterial("#2a1a10"));
            foreach (var x in new[] { -0.74f, 0.74f }) foreach (var z in new[] { -0.5f, 0.54f })
                MountainBox($"Leg_{x}_{z}", root.transform, new Vector3(x, 0.36f, z),
                    new Vector3(0.24f, 0.72f, 0.24f), MountainMaterial("#1b1009"));
            foreach (var x in new[] { -0.72f, 0f, 0.72f })
                MountainBox($"Gold_{x}", root.transform, new Vector3(x, 2.92f, 1.1f), new Vector3(0.24f, 0.36f, 0.24f), MountainMaterial("#d7a548"));
        }

        private static void CreateMountainThrone(Transform parent)
        {
            var root = new GameObject("KingsThrone");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, -15.6f);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            MountainBox("Base", root.transform, new Vector3(0f, 0.28f, -0.04f), new Vector3(5.2f, 0.56f, 3.8f), MountainMaterial("#21140c"));
            MountainBox("Seat", root.transform, new Vector3(0f, 0.86f, -0.28f), new Vector3(4.35f, 0.72f, 3f), MountainMaterial("#704527"));
            MountainBox("Cushion", root.transform, new Vector3(0f, 1.16f, -0.42f), new Vector3(3.45f, 0.24f, 2.1f), MountainMaterial("#8e1e24"));
            MountainBox("Back", root.transform, new Vector3(0f, 2.46f, 1.08f), new Vector3(4.55f, 3.8f, 0.72f), MountainMaterial("#3a2415"));
            MountainBox("BackCushion", root.transform, new Vector3(0f, 2.56f, 1.48f), new Vector3(3.18f, 2.86f, 0.22f), MountainMaterial("#9f2428"));
            foreach (var side in new[] { -1.94f, 1.94f })
            {
                MountainBox($"Arm_{side}", root.transform, new Vector3(side, 1.28f, -0.3f), new Vector3(0.62f, 1.42f, 3.12f), MountainMaterial("#2b1a0f"));
                MountainBox($"ArmGold_{side}", root.transform, new Vector3(side, 2.12f, -1.18f), new Vector3(0.78f, 0.28f, 1.28f), MountainMaterial("#d7a548"));
            }
            var crownMesh = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/ThroneCrownCone4.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            var crown = CreateMeshVisual("Crown", root.transform, new Vector3(0f, 4.64f, 1.1f), crownMesh,
                MountainMaterial("#e2b34c"));
            crown.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            crown.transform.localScale = new Vector3(1.26f, 1.16f, 1.26f);
            for (var index = 0; index < 3; index++)
            {
                var x = new[] { -1.72f, 0f, 1.72f }[index];
                MountainBox($"Spire_{index}", root.transform,
                    new Vector3(x, 4.36f + (index == 1 ? 0.36f : 0f), 1.12f),
                    new Vector3(0.4f, index == 1 ? 1.28f : 0.9f, 0.42f), MountainMaterial("#d7a548"));
            }
            MountainBox("Carpet", root.transform, new Vector3(0f, 0.74f, -2.45f), new Vector3(6.4f, 0.16f, 1.7f), MountainMaterial("#68161d"));
        }

        private static void CreateMountainBanquetColliders(Transform parent, WofMountainVillageDocument document, float bottomY)
        {
            var root = new GameObject("BanquetColliders");
            root.transform.SetParent(parent, false);
            CreateMountainColliderFromRecord(root.transform, "Table", document.banquetColliders.table, bottomY);
            CreateMountainColliderFromRecord(root.transform, "Throne", document.banquetColliders.throne, bottomY);
            foreach (var chair in document.banquetColliders.chairs)
                CreateMountainColliderFromRecord(root.transform, $"Chair_{chair.index:00}", chair, bottomY);
        }

        private static void CreateMountainColliderFromRecord(Transform parent, string name, WofMountainColliderRecord record, float bottomY)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(record.positionOffset[0], bottomY + record.positionOffset[1], record.positionOffset[2]);
            if (record.rotation != null && record.rotation.Length == 3) item.transform.localRotation = ToMountainEuler(record.rotation);
            CreateMountainBoxCollider(item, Vector3.zero, new Vector3(record.args[0] * 2f, record.args[1] * 2f, record.args[2] * 2f));
        }

        private static void CreateMountainFrustum(
            string name,
            Transform parent,
            Vector3 position,
            float topRadius,
            float bottomRadius,
            float height,
            int segments,
            Material material)
        {
            var key = $"Frustum_{Mathf.RoundToInt(topRadius * 100f)}_{Mathf.RoundToInt(bottomRadius * 100f)}_{Mathf.RoundToInt(height * 100f)}_{segments}.asset";
            var mesh = GetOrCreateMeshAsset(MountainGeometryRoot + "/" + key,
                () => CreateDarrelFrustumMesh(topRadius, bottomRadius, height, segments));
            CreateMeshVisual(name, parent, position, mesh, material);
        }

        private static void CreateMountainRing(
            string name,
            Transform parent,
            float innerRadius,
            float outerRadius,
            int segments,
            Vector3 position,
            Material material)
        {
            var key = $"Ring_{Mathf.RoundToInt(innerRadius * 100f)}_{Mathf.RoundToInt(outerRadius * 100f)}_{segments}.asset";
            var mesh = GetOrCreateMeshAsset(MountainGeometryRoot + "/" + key,
                () => CreateDarrelRingMesh(innerRadius, outerRadius, segments));
            CreateMeshVisual(name, parent, position, mesh, material);
        }

        private static Mesh CreateMountainOpenCylinderMesh(float topRadius, float bottomRadius, float height, int segments)
        {
            var vertices = new List<Vector3>(segments * 2);
            var normals = new List<Vector3>(segments * 2);
            var uv = new List<Vector2>(segments * 2);
            var triangles = new List<int>(segments * 6);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var sin = Mathf.Sin(angle);
                var cos = Mathf.Cos(angle);
                vertices.Add(new Vector3(sin * bottomRadius, -height * 0.5f, cos * bottomRadius));
                vertices.Add(new Vector3(sin * topRadius, height * 0.5f, cos * topRadius));
                var normal = new Vector3(sin, 0f, cos).normalized;
                normals.Add(normal);
                normals.Add(normal);
                uv.Add(new Vector2(index / (float)segments, 0f));
                uv.Add(new Vector2(index / (float)segments, 1f));
            }
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var a = index * 2;
                var b = a + 1;
                var c = next * 2;
                var d = c + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(d);
            }
            var mesh = new Mesh { name = "Exact React Open Mineshaft" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float MountainAbsoluteAngleDelta(float left, float right)
        {
            return Mathf.Abs(Mathf.DeltaAngle(left * Mathf.Rad2Deg, right * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
        }
    }
}

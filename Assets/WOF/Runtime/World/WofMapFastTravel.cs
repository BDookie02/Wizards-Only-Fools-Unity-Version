using System;
using UnityEngine;

namespace WOF
{
    public enum WofMapDestination
    {
        Base = 0,
        Chicago = 1,
        Swamp = 2,
        Desert = 3,
        Mountain = 4,
        Graveyard = 5
    }

    public readonly struct WofMapDestinationRecord
    {
        public WofMapDestinationRecord(WofMapDestination destination, string label, Vector3 position)
        {
            Destination = destination;
            Label = label;
            Position = position;
        }

        public WofMapDestination Destination { get; }
        public string Label { get; }
        public Vector3 Position { get; }
    }

    /// <summary>
    /// The player-facing destinations from React mapOverlayRuntime.ts. These coordinates are
    /// deliberately allow-listed so a client can never use the map RPC as an arbitrary teleport.
    /// </summary>
    public static class WofMapFastTravel
    {
        public const float MapMinX = -2304f;
        public const float MapMaxX = 3328f;
        public const float MapMinZ = -2304f;
        public const float MapMaxZ = 1792f;

        private static readonly WofMapDestinationRecord[] DestinationRecords =
        {
            new(WofMapDestination.Base, "BASE VILLAGE", new Vector3(0f, 15f, 30f)),
            new(WofMapDestination.Chicago, "CHICAGO", new Vector3(-1536f, 140f, -1322f)),
            new(WofMapDestination.Swamp, "SWAMP", new Vector3(0f, 140f, -1322f)),
            new(WofMapDestination.Desert, "DESERT", new Vector3(2048f, 140f, -1834f)),
            new(WofMapDestination.Mountain, "MOUNTAIN", new Vector3(1536f, 270f, 62f)),
            new(WofMapDestination.Graveyard, "GRAVEYARD", new Vector3(2560f, 92f, 1156f))
        };

        public static ReadOnlySpan<WofMapDestinationRecord> Destinations => DestinationRecords;

        public static bool TryGet(WofMapDestination destination, out WofMapDestinationRecord record)
        {
            var index = (int)destination;
            if (index >= 0 && index < DestinationRecords.Length &&
                DestinationRecords[index].Destination == destination)
            {
                record = DestinationRecords[index];
                return true;
            }

            record = default;
            return false;
        }

        public static bool IsValid(int destinationValue)
        {
            return destinationValue >= 0 && destinationValue < DestinationRecords.Length;
        }
    }
}

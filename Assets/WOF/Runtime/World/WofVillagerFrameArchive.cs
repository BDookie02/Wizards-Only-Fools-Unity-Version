using System;
using System.Collections.Generic;
using System.Text;

namespace WOF
{
    public sealed class WofVillagerFrameArchive
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("WOFAV01\0");
        private readonly byte[] _bytes;
        private readonly Dictionary<string, FrameEntry> _entries;

        private WofVillagerFrameArchive(byte[] bytes, Dictionary<string, FrameEntry> entries)
        {
            _bytes = bytes;
            _entries = entries;
        }

        public int EntryCount => _entries.Count;

        public static bool TryParse(byte[] bytes, out WofVillagerFrameArchive archive, out string error)
        {
            archive = null;
            error = string.Empty;
            if (bytes == null || bytes.Length < Magic.Length + 4)
            {
                error = "Villager frame archive is truncated.";
                return false;
            }

            for (var index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] == Magic[index]) continue;
                error = "Villager frame archive magic does not match WOFAV01.";
                return false;
            }

            var cursor = Magic.Length;
            if (!TryReadUInt32(bytes, ref cursor, out var entryCount) || entryCount == 0 || entryCount > 256)
            {
                error = "Villager frame archive has an invalid entry count.";
                return false;
            }

            var entries = new Dictionary<string, FrameEntry>((int)entryCount, StringComparer.Ordinal);
            for (var entryIndex = 0u; entryIndex < entryCount; entryIndex++)
            {
                if (cursor >= bytes.Length)
                {
                    error = "Villager frame archive ended inside its entry table.";
                    return false;
                }

                var keyLength = bytes[cursor++];
                if (keyLength == 0 || cursor + keyLength > bytes.Length)
                {
                    error = "Villager frame archive contains an invalid frame key.";
                    return false;
                }

                var key = Encoding.UTF8.GetString(bytes, cursor, keyLength);
                cursor += keyLength;
                if (!TryReadUInt32(bytes, ref cursor, out var offset) ||
                    !TryReadUInt32(bytes, ref cursor, out var length) ||
                    length == 0 ||
                    offset > bytes.Length ||
                    length > bytes.Length - offset)
                {
                    error = $"Villager frame archive contains invalid bounds for {key}.";
                    return false;
                }

                if (!entries.TryAdd(key, new FrameEntry((int)offset, (int)length)))
                {
                    error = $"Villager frame archive contains duplicate key {key}.";
                    return false;
                }
            }

            archive = new WofVillagerFrameArchive(bytes, entries);
            return true;
        }

        public bool Contains(string key)
        {
            return key != null && _entries.ContainsKey(key);
        }

        public bool TryExtractPng(string key, out byte[] png)
        {
            png = null;
            if (key == null || !_entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            png = new byte[entry.Length];
            Buffer.BlockCopy(_bytes, entry.Offset, png, 0, entry.Length);
            return true;
        }

        private static bool TryReadUInt32(byte[] bytes, ref int cursor, out uint value)
        {
            value = 0;
            if (cursor < 0 || cursor + 4 > bytes.Length)
            {
                return false;
            }

            value = (uint)(bytes[cursor] |
                           bytes[cursor + 1] << 8 |
                           bytes[cursor + 2] << 16 |
                           bytes[cursor + 3] << 24);
            cursor += 4;
            return true;
        }

        private readonly struct FrameEntry
        {
            public FrameEntry(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public int Offset { get; }
            public int Length { get; }
        }
    }
}

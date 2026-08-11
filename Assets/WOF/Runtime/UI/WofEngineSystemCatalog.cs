using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofEngineSystemDescriptor
    {
        public string id;
        public string name;
        public string category;
        public string owner;
        public string responsibility;
        public string[] currentEntrypoints = Array.Empty<string>();
        public string extractionTarget;
    }

    [Serializable]
    public sealed class WofEngineSystemCatalogDocument
    {
        public int version = 1;
        public string sourceModule;
        public string sourceSha256;
        public WofEngineSystemDescriptor[] systems = Array.Empty<WofEngineSystemDescriptor>();
    }

    public static class WofEngineSystemCatalog
    {
        internal const string ResourcePath = "WOF/EngineSystemCatalog";
        public const int ReactSystemCount = 18;

        private static WofEngineSystemCatalogDocument _cached;

        public static IReadOnlyList<WofEngineSystemDescriptor> Systems => Load().systems;

        public static WofEngineSystemCatalogDocument Load()
        {
            if (_cached != null) return _cached;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            _cached = Parse(asset == null ? string.Empty : asset.text);
            if (_cached.systems.Length != ReactSystemCount)
            {
                Debug.LogError(
                    $"[WOF] Engine system catalog is incomplete: expected {ReactSystemCount}, found {_cached.systems.Length}.");
            }
            return _cached;
        }

        internal static WofEngineSystemCatalogDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new WofEngineSystemCatalogDocument();
            try
            {
                var document = JsonUtility.FromJson<WofEngineSystemCatalogDocument>(json) ??
                               new WofEngineSystemCatalogDocument();
                document.systems ??= Array.Empty<WofEngineSystemDescriptor>();
                return document;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WOF] Unable to parse engine system catalog: {exception.Message}");
                return new WofEngineSystemCatalogDocument();
            }
        }
    }
}

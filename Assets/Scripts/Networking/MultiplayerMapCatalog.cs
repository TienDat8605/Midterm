using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MultiplayerMapEntry
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string sceneName;

    public string Id => id;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
}

[CreateAssetMenu(fileName = "MultiplayerMapCatalog", menuName = "DINO PARK/Multiplayer Map Catalog")]
public sealed class MultiplayerMapCatalog : ScriptableObject
{
    [SerializeField] private string defaultMapId = "map1";
    [SerializeField] private List<MultiplayerMapEntry> maps = new List<MultiplayerMapEntry>();

    public string DefaultMapId => defaultMapId;
    public IReadOnlyList<MultiplayerMapEntry> Maps => maps;

    public bool TryGetMap(string mapId, out MultiplayerMapEntry map)
    {
        map = null;
        if (string.IsNullOrWhiteSpace(mapId))
            return false;

        for (int i = 0; i < maps.Count; i++)
        {
            MultiplayerMapEntry candidate = maps[i];
            if (candidate != null && string.Equals(candidate.Id, mapId, StringComparison.Ordinal))
            {
                map = candidate;
                return true;
            }
        }

        return false;
    }

    public bool IsValid(out string error)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < maps.Count; i++)
        {
            MultiplayerMapEntry map = maps[i];
            if (map == null || string.IsNullOrWhiteSpace(map.Id) ||
                string.IsNullOrWhiteSpace(map.DisplayName) || string.IsNullOrWhiteSpace(map.SceneName))
            {
                error = $"Map entry {i} is incomplete.";
                return false;
            }

            if (!ids.Add(map.Id))
            {
                error = $"Duplicate map id: {map.Id}.";
                return false;
            }
        }

        if (!TryGetMap(defaultMapId, out _))
        {
            error = $"Default map id '{defaultMapId}' is not present in the catalog.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EscapistClueRegistry
{
    private readonly HashSet<int> targetEscapistIds = new HashSet<int>();
    private readonly HashSet<string> globalCollectedClues = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<string>> cluesByEscapistId = new Dictionary<int, HashSet<string>>();

    public void SetTargetEscapists(IEnumerable<int> escapistIds)
    {
        targetEscapistIds.Clear();

        if (escapistIds == null) return;

        foreach (var id in escapistIds)
        {
            if (id > 0) targetEscapistIds.Add(id);
        }

        foreach (var id in targetEscapistIds)
        {
            if (!cluesByEscapistId.ContainsKey(id))
            {
                cluesByEscapistId[id] = new HashSet<string>(StringComparer.Ordinal);
            }
        }

        var toRemove = cluesByEscapistId.Keys.Where(id => !targetEscapistIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            cluesByEscapistId.Remove(id);
        }
    }

    public bool AddClue(int escapistId, string clueId)
    {
        if (escapistId <= 0) return false;
        if (string.IsNullOrWhiteSpace(clueId)) return false;

        globalCollectedClues.Add(clueId);

        if (!cluesByEscapistId.TryGetValue(escapistId, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            cluesByEscapistId[escapistId] = set;
        }

        return set.Add(clueId);
    }

    public IReadOnlyCollection<int> TargetEscapistIds => targetEscapistIds.ToList();

    public IReadOnlyDictionary<int, IReadOnlyCollection<string>> GetSnapshot()
    {
        var result = new Dictionary<int, IReadOnlyCollection<string>>();
        var globalCluesList = globalCollectedClues.ToList();

        foreach (var escapistId in targetEscapistIds)
        {
            result[escapistId] = globalCluesList;
        }

        return result;
    }

    public int GetUniqueCluesCollectedCount()
    {
        return globalCollectedClues.Count;
    }

    public int GetGlobalCollectedCount()
    {
        return globalCollectedClues.Count;
    }

    public bool HaveAllRequiredClues(int requiredTotalUniqueClues)
    {
        if (requiredTotalUniqueClues <= 0) return false;
        return GetUniqueCluesCollectedCount() >= requiredTotalUniqueClues;
    }

    public void Clear()
    {
        targetEscapistIds.Clear();
        cluesByEscapistId.Clear();
        globalCollectedClues.Clear();
    }

    public void SyncFromSnapshot(IReadOnlyDictionary<int, IReadOnlyCollection<string>> cluesByEscapist)
    {
        if (cluesByEscapist == null) return;


        globalCollectedClues.Clear();
        cluesByEscapistId.Clear();

        var allClues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in cluesByEscapist)
        {
            foreach (var clueId in kvp.Value)
            {
                allClues.Add(clueId);
            }
        }

        foreach (var clueId in allClues)
        {
            globalCollectedClues.Add(clueId);
        }

        foreach (var kvp in cluesByEscapist)
        {
            int escapistId = kvp.Key;
            var clueIds = kvp.Value;

            if (!cluesByEscapistId.ContainsKey(escapistId))
            {
                cluesByEscapistId[escapistId] = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (var clueId in clueIds)
            {
                cluesByEscapistId[escapistId].Add(clueId);
            }

        }

    }
}
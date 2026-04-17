using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AutoMinion;

internal record MinionEntry(uint Id, string Name);

internal enum MinionLoadState
{
    NotLoaded,
    Ready,
    PlayerNotReady,
    SheetUnavailable,
}

internal sealed class MinionService
{
    private List<MinionEntry> cachedOwnedMinions = [];

    public MinionLoadState LoadState { get; private set; } = MinionLoadState.NotLoaded;

    public bool ShouldAttemptLoad(bool hasNoCachedUiData)
    {
        return LoadState switch
        {
            MinionLoadState.NotLoaded => true,
            MinionLoadState.PlayerNotReady => hasNoCachedUiData && AutoMinion.PlayerState.IsLoaded,
            _ => false,
        };
    }

    public unsafe List<MinionEntry> GetOwnedMinions(bool forceReload = false)
    {
        if (!forceReload && LoadState == MinionLoadState.Ready)
        {
            return [.. cachedOwnedMinions];
        }

        if (!AutoMinion.PlayerState.IsLoaded)
        {
            cachedOwnedMinions = [];
            LoadState = MinionLoadState.PlayerNotReady;
            return [];
        }

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            cachedOwnedMinions = [];
            LoadState = MinionLoadState.PlayerNotReady;
            return [];
        }

        var companionSheet = AutoMinion.DataManager.GetExcelSheet<Companion>();
        if (companionSheet == null)
        {
            cachedOwnedMinions = [];
            LoadState = MinionLoadState.SheetUnavailable;
            return [];
        }

        var ownedMinions = new List<MinionEntry>();
        foreach (var minion in companionSheet)
        {
            if (minion.RowId == 0 || !uiState->IsCompanionUnlocked(minion.RowId))
            {
                continue;
            }

            var minionName = minion.Singular.ToString();
            if (string.IsNullOrWhiteSpace(minionName))
            {
                continue;
            }

            ownedMinions.Add(new MinionEntry(minion.RowId, minionName));
        }

        ownedMinions.Sort(static (left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
        cachedOwnedMinions = ownedMinions;
        LoadState = MinionLoadState.Ready;
        return [.. cachedOwnedMinions];
    }
}

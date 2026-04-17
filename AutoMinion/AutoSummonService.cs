using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AutoMinion;

internal sealed class AutoSummonService : IDisposable
{
    private static readonly TimeSpan DuplicateSummonWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryAttemptWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PendingPollWindow = TimeSpan.FromSeconds(1);

    private readonly AutoMinion autoMinion;
    private uint lastTriggeredJobId;
    private uint lastTriggeredMinionId;
    private DateTime lastTriggeredAtUtc = DateTime.MinValue;
    private uint? pendingJobId;
    private uint? pendingMinionId;
    private uint lastAttemptedJobId;
    private uint lastAttemptedMinionId;
    private DateTime lastAttemptedAtUtc = DateTime.MinValue;
    private DateTime lastPendingPollAtUtc = DateTime.MinValue;

    public AutoSummonService(AutoMinion autoMinion)
    {
        this.autoMinion = autoMinion;
        AutoMinion.ClientState.ClassJobChanged += OnClassJobChanged;
        AutoMinion.ClientState.TerritoryChanged += OnTerritoryChanged;
        AutoMinion.ClientState.Login += OnLogin;
        AutoMinion.Condition.ConditionChange += OnConditionChange;
        AutoMinion.Framework.Update += OnFrameworkUpdate;

        QueueCurrentJobSummonIfNeeded();
        TryProcessPendingSummon();
    }

    public void Dispose()
    {
        AutoMinion.ClientState.ClassJobChanged -= OnClassJobChanged;
        AutoMinion.ClientState.TerritoryChanged -= OnTerritoryChanged;
        AutoMinion.ClientState.Login -= OnLogin;
        AutoMinion.Condition.ConditionChange -= OnConditionChange;
        AutoMinion.Framework.Update -= OnFrameworkUpdate;
    }

    internal static unsafe bool TrySummonMinion(uint minionId)
    {
        try
        {
            var actionManager = ActionManager.Instance();
            if (actionManager == null)
            {
                return false;
            }

            return actionManager->UseAction(ActionType.Companion, minionId, 0, 0, default, 0, null);
        }
        catch
        {
            return false;
        }
    }

    private void OnClassJobChanged(uint classJobId)
    {
        var minionId = ResolveConfiguredMinion(classJobId);
        if (!minionId.HasValue)
        {
            ClearPendingSummon();
            return;
        }

        QueuePendingSummon(classJobId, minionId.Value);
        TryProcessPendingSummon();
    }

    private void OnTerritoryChanged(ushort territoryId)
    {
        _ = territoryId;
        QueueCurrentJobSummonIfNeeded();
        TryProcessPendingSummon();
    }

    private void OnLogin()
    {
        QueueCurrentJobSummonIfNeeded();
        TryProcessPendingSummon();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (value)
        {
            return;
        }

        if (flag is ConditionFlag.UsingFashionAccessory or
            ConditionFlag.Mounted or
            ConditionFlag.RidingPillion or
            ConditionFlag.Mounting or
            ConditionFlag.MountOrOrnamentTransition or
            ConditionFlag.BetweenAreas or
            ConditionFlag.BetweenAreas51)
        {
            QueueCurrentJobSummonIfNeeded();
            TryProcessPendingSummon();
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _ = framework;
        if (!pendingJobId.HasValue || !pendingMinionId.HasValue)
        {
            return;
        }

        if (DateTime.UtcNow - lastPendingPollAtUtc < PendingPollWindow)
        {
            return;
        }

        lastPendingPollAtUtc = DateTime.UtcNow;
        QueueCurrentJobSummonIfNeeded();
        TryProcessPendingSummon();
    }

    private void TryProcessPendingSummon()
    {
        if (!pendingJobId.HasValue || !pendingMinionId.HasValue)
        {
            return;
        }

        var currentJobId = GetCurrentJobId();
        if (!currentJobId.HasValue || currentJobId.Value != pendingJobId.Value)
        {
            ClearPendingSummon();
            return;
        }

        var configuredMinionId = ResolveConfiguredMinion(currentJobId.Value);
        if (!configuredMinionId.HasValue)
        {
            ClearPendingSummon();
            return;
        }

        if (configuredMinionId.Value != pendingMinionId.Value)
        {
            QueuePendingSummon(currentJobId.Value, configuredMinionId.Value);
        }

        if (!CanAttemptAutoSummon())
        {
            return;
        }

        if (GetCurrentActiveMinionId() == pendingMinionId.Value)
        {
            ClearPendingSummon();
            return;
        }

        if (IsDuplicateTrigger(pendingJobId.Value, pendingMinionId.Value) ||
            IsAttemptThrottled(pendingJobId.Value, pendingMinionId.Value))
        {
            return;
        }

        RecordAttempt(pendingJobId.Value, pendingMinionId.Value);
        if (!TrySummonMinion(pendingMinionId.Value))
        {
            return;
        }

        lastTriggeredJobId = pendingJobId.Value;
        lastTriggeredMinionId = pendingMinionId.Value;
        lastTriggeredAtUtc = DateTime.UtcNow;
        if (autoMinion.Configuration.EnableChatOutput)
        {
            AutoMinion.ChatGui.Print($"[AutoMinion] Sent summon action for minion {pendingMinionId.Value}.");
        }
    }

    private uint? ResolveConfiguredMinion(uint classJobId)
    {
        var assignment = autoMinion.Configuration.Assignments.Find(entry => entry.JobId == classJobId);
        return assignment?.MinionId;
    }

    private void QueueCurrentJobSummonIfNeeded()
    {
        var currentJobId = GetCurrentJobId();
        if (!currentJobId.HasValue)
        {
            return;
        }

        var configuredMinionId = ResolveConfiguredMinion(currentJobId.Value);
        if (!configuredMinionId.HasValue)
        {
            return;
        }

        if (GetCurrentActiveMinionId() == configuredMinionId.Value)
        {
            ClearPendingSummon();
            return;
        }

        QueuePendingSummon(currentJobId.Value, configuredMinionId.Value);
    }

    private static bool CanAttemptAutoSummon()
    {
        if (!AutoMinion.PlayerState.IsLoaded)
        {
            return false;
        }

        if (AutoMinion.Condition[ConditionFlag.Mounted] ||
            AutoMinion.Condition[ConditionFlag.RidingPillion] ||
            AutoMinion.Condition[ConditionFlag.Mounting] ||
            AutoMinion.Condition[ConditionFlag.MountOrOrnamentTransition] ||
            AutoMinion.Condition[ConditionFlag.UsingFashionAccessory])
        {
            return false;
        }

        return AutoMinion.ClientState.IsClientIdle();
    }

    private static uint? GetCurrentJobId()
    {
        return AutoMinion.PlayerState.IsLoaded && AutoMinion.PlayerState.ClassJob.IsValid
            ? AutoMinion.PlayerState.ClassJob.RowId
            : null;
    }

    private bool IsDuplicateTrigger(uint classJobId, uint minionId)
    {
        return classJobId == lastTriggeredJobId &&
               minionId == lastTriggeredMinionId &&
               DateTime.UtcNow - lastTriggeredAtUtc < DuplicateSummonWindow;
    }

    private bool IsAttemptThrottled(uint classJobId, uint minionId)
    {
        return classJobId == lastAttemptedJobId &&
               minionId == lastAttemptedMinionId &&
               DateTime.UtcNow - lastAttemptedAtUtc < RetryAttemptWindow;
    }

    private void RecordAttempt(uint classJobId, uint minionId)
    {
        lastAttemptedJobId = classJobId;
        lastAttemptedMinionId = minionId;
        lastAttemptedAtUtc = DateTime.UtcNow;
    }

    private void QueuePendingSummon(uint classJobId, uint minionId)
    {
        pendingJobId = classJobId;
        pendingMinionId = minionId;
        lastPendingPollAtUtc = DateTime.MinValue;
    }

    private void ClearPendingSummon()
    {
        pendingJobId = null;
        pendingMinionId = null;
    }

    private static unsafe uint? GetCurrentActiveMinionId()
    {
        var localPlayer = AutoMinion.ObjectTable.LocalPlayer;
        if (localPlayer == null || localPlayer.Address == IntPtr.Zero)
        {
            return null;
        }

        var character = (Character*)localPlayer.Address;
        if (character == null || character->CompanionObject == null)
        {
            return null;
        }

        var currentMinionId = character->CompanionObject->BaseId;
        return currentMinionId == 0 ? null : currentMinionId;
    }
}

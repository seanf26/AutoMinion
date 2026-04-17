using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoMinion.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly AutoMinion autoMinion;
    private readonly Dictionary<uint, string> minionSearchTexts = [];
    private readonly Dictionary<uint, JobEntry> jobsById;
    private readonly List<JobEntry> allJobs;

    private List<MinionEntry> ownedMinions = [];

    public MainWindow(AutoMinion autoMinion)
        : base(BuildWindowTitle() + "##AutoMinionAssignments")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.autoMinion = autoMinion;
        allJobs = autoMinion.JobService.GetJobs();
        jobsById = allJobs.ToDictionary(job => job.Id);
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (autoMinion.MinionService.ShouldAttemptLoad(ownedMinions.Count == 0))
        {
            LoadMinions();
        }

        DrawStatusMessage();
        DrawToolbar();
        ImGui.Separator();
        DrawAssignments();
    }

    private void DrawStatusMessage()
    {
        switch (autoMinion.MinionService.LoadState)
        {
            case MinionLoadState.PlayerNotReady:
                ImGui.TextUnformatted("Player not ready");
                break;
            case MinionLoadState.SheetUnavailable:
                ImGui.TextUnformatted("No minions found");
                break;
        }
    }

    private void DrawToolbar()
    {
        var availableJobs = GetAvailableJobs();
        DrawAddJobCombo(availableJobs);

        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            LoadMinions(forceReload: true);
        }

        ImGui.SameLine();
        var enableChatOutput = autoMinion.Configuration.EnableChatOutput;
        if (ImGui.Checkbox("Enable Chat Output", ref enableChatOutput))
        {
            autoMinion.Configuration.EnableChatOutput = enableChatOutput;
            autoMinion.Configuration.Save();
        }
    }

    private void DrawAddJobCombo(List<JobEntry> availableJobs)
    {
        var previewText = GetAddJobPreview(availableJobs);
        if (!ImGui.BeginCombo("##AddJob", previewText))
        {
            return;
        }

        if (availableJobs.Count == 0)
        {
            ImGui.TextUnformatted("All jobs assigned");
            ImGui.EndCombo();
            return;
        }

        foreach (var job in availableJobs)
        {
            var isSelected = false;
            if (ImGui.Selectable(FormatJob(job), isSelected))
            {
                AddJobAssignment(job.Id);
                ImGui.EndCombo();
                return;
            }
        }

        ImGui.EndCombo();
    }

    private void DrawAssignments()
    {
        var assignments = GetOrderedAssignments();
        if (assignments.Count == 0)
        {
            ImGui.TextWrapped("Add a job above to assign one of your owned minions to it.");
            return;
        }

        if (!ImGui.BeginTable(
                "JobMinionAssignments",
                3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Resizable))
        {
            return;
        }

        ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Minion", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Remove Job", ImGuiTableColumnFlags.WidthFixed, 96f);
        ImGui.TableHeadersRow();

        foreach (var assignment in assignments)
        {
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(GetJobLabel(assignment.JobId));

            ImGui.TableSetColumnIndex(1);
            DrawSearchableMinionCombo(assignment);

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Button($"Remove##{assignment.JobId}"))
            {
                RemoveAssignment(assignment.JobId);
            }
        }

        ImGui.EndTable();
    }

    private void DrawSearchableMinionCombo(JobMinionAssignment assignment)
    {
        var comboId = $"##minion-combo-{assignment.JobId}";
        var searchId = $"##minion-search-{assignment.JobId}";
        var previewText = GetMinionPreview(assignment.MinionId);

        if (!minionSearchTexts.TryGetValue(assignment.JobId, out var searchText))
        {
            searchText = string.Empty;
        }

        if (!ImGui.BeginCombo(comboId, previewText))
        {
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint(searchId, "Search minions", ref searchText, 100);
        minionSearchTexts[assignment.JobId] = searchText;

        ImGui.Separator();

        if (ImGui.Selectable($"None##{assignment.JobId}", assignment.MinionId is null))
        {
            UpdateAssignmentMinion(assignment.JobId, null);
            minionSearchTexts[assignment.JobId] = string.Empty;
        }

        var filteredMinions = GetFilteredMinions(searchText);
        if (ownedMinions.Count == 0)
        {
            ImGui.TextUnformatted("No minions found");
            ImGui.EndCombo();
            return;
        }

        if (filteredMinions.Count == 0)
        {
            ImGui.TextUnformatted("No matches");
            ImGui.EndCombo();
            return;
        }

        foreach (var minion in filteredMinions)
        {
            var isSelected = assignment.MinionId == minion.Id;
            if (ImGui.Selectable($"{minion.Name} ({minion.Id})##{assignment.JobId}-{minion.Id}", isSelected))
            {
                UpdateAssignmentMinion(assignment.JobId, minion.Id);
                minionSearchTexts[assignment.JobId] = string.Empty;
            }

            if (isSelected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void LoadMinions(bool forceReload = false)
    {
        ownedMinions = autoMinion.MinionService.GetOwnedMinions(forceReload);
    }

    private List<JobEntry> GetAvailableJobs()
    {
        var assignedJobIds = autoMinion.Configuration.Assignments.Select(assignment => assignment.JobId).ToHashSet();
        return allJobs.FindAll(job => !assignedJobIds.Contains(job.Id));
    }

    private List<JobMinionAssignment> GetOrderedAssignments()
    {
        return
        [
            .. autoMinion.Configuration.Assignments
                .OrderBy(assignment => GetJobLabel(assignment.JobId), StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    private List<MinionEntry> GetFilteredMinions(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [.. ownedMinions];
        }

        return ownedMinions.FindAll(minion =>
            minion.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private string GetAddJobPreview(List<JobEntry> availableJobs)
    {
        if (availableJobs.Count == 0)
        {
            return "All jobs assigned";
        }

        return "Select a job";
    }

    private string GetJobLabel(uint jobId)
    {
        return jobsById.TryGetValue(jobId, out var job)
            ? FormatJob(job)
            : $"Unknown job ({jobId})";
    }

    private string GetMinionPreview(uint? minionId)
    {
        if (!minionId.HasValue)
        {
            return "None";
        }

        var selectedMinion = ownedMinions.Find(minion => minion.Id == minionId.Value);
        return selectedMinion is null
            ? $"Unknown minion ({minionId.Value})"
            : selectedMinion.Name;
    }

    private void AddJobAssignment(uint jobId)
    {
        autoMinion.Configuration.Assignments.Add(new JobMinionAssignment
        {
            JobId = jobId,
            MinionId = null,
        });
        autoMinion.Configuration.Save();
    }

    private void RemoveAssignment(uint jobId)
    {
        autoMinion.Configuration.Assignments.RemoveAll(assignment => assignment.JobId == jobId);
        minionSearchTexts.Remove(jobId);
        autoMinion.Configuration.Save();
    }

    private void UpdateAssignmentMinion(uint jobId, uint? minionId)
    {
        var assignment = autoMinion.Configuration.Assignments.Find(entry => entry.JobId == jobId);
        if (assignment is null || assignment.MinionId == minionId)
        {
            return;
        }

        assignment.MinionId = minionId;
        autoMinion.Configuration.Save();
    }

    private static string FormatJob(JobEntry job)
    {
        return $"{job.Abbreviation} - {job.Name}";
    }

    private static string BuildWindowTitle()
    {
        var version = AutoMinion.PluginInterface.Manifest.AssemblyVersion?.ToString() ?? "0.0.0.0";
        if (version.EndsWith(".0", StringComparison.Ordinal))
        {
            version = version[..^2];
        }

        return $"AutoMinion v{version}";
    }
}

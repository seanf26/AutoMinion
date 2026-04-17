using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace AutoMinion;

internal record JobEntry(uint Id, string Name, string Abbreviation);

internal sealed class JobService
{
    private List<JobEntry>? cachedJobs;

    public List<JobEntry> GetJobs()
    {
        if (cachedJobs is not null)
        {
            return [.. cachedJobs];
        }

        var classJobSheet = AutoMinion.DataManager.GetExcelSheet<ClassJob>();
        if (classJobSheet == null)
        {
            cachedJobs = [];
            return [];
        }

        var jobs = new List<JobEntry>();
        foreach (var classJob in classJobSheet)
        {
            if (classJob.RowId == 0)
            {
                continue;
            }

            var name = classJob.Name.ToString();
            var abbreviation = classJob.Abbreviation.ToString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(abbreviation))
            {
                continue;
            }

            var hasPlayableIndex = classJob.JobIndex > 0 || classJob.BattleClassIndex > 0 || classJob.DohDolJobIndex > 0;
            if (!hasPlayableIndex)
            {
                continue;
            }

            jobs.Add(new JobEntry(classJob.RowId, name, abbreviation));
        }

        jobs.Sort(static (left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
        cachedJobs = jobs;
        return [.. cachedJobs];
    }
}

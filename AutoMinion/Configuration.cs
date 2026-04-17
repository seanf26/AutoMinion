using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace AutoMinion;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<JobMinionAssignment> Assignments { get; set; } = [];

    public void Save()
    {
        AutoMinion.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public sealed class JobMinionAssignment
{
    public uint JobId { get; set; }
    public uint? MinionId { get; set; }
}

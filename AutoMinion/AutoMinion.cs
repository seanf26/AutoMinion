using AutoMinion.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AutoMinion;

public sealed class AutoMinion : IDalamudPlugin
{
    private const string CommandName = "/autominion";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Configuration { get; init; }
    internal MinionService MinionService { get; init; }
    internal JobService JobService { get; init; }
    internal AutoSummonService AutoSummonService { get; init; }

    public readonly WindowSystem WindowSystem = new("AutoMinion");

    private MainWindow MainWindow { get; init; }

    public AutoMinion()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Assignments ??= [];

        MinionService = new MinionService();
        JobService = new JobService();
        AutoSummonService = new AutoSummonService(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close the AutoMinion window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"===Initialized {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        AutoSummonService.Dispose();
        MainWindow.Dispose();
    }

    public void ToggleConfigUi() => MainWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    private void OnCommand(string command, string arguments)
    {
        _ = command;
        _ = arguments;
        MainWindow.Toggle();
    }
}

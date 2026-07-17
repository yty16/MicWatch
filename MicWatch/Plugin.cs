using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicWatch.Controls;
using MicWatch.Core;
using MicWatch.Models;
using MicWatch.Services;
using MicWatch.Triggers;
using MicWatch.ViewModels;
using MicWatch.Views;

namespace MicWatch;

[PluginEntrance]
public partial class Plugin : PluginBase
{
    public static Settings Settings { get; set; } = new Settings();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Ownership.AssertLoaded();

        Settings = ConfigureFileHelper.LoadConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"));
        Settings.PropertyChanged += (_, _) =>
        {
            ConfigureFileHelper.SaveConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);
        };

        services.AddSingleton<IMicActivityMonitorService, MicActivityMonitorService>();
        services.AddSettingsPage<MicActivityMonitorSettingsPage>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddRule<MicRuleSettings, MicRuleSettingsControl>("micwatch.ismicinuse", "麦克风是否被使用", "🎙");

        
        services.AddTrigger<MicStartedTrigger>();
        services.AddTrigger<MicStoppedTrigger>();

        AppBase.Current.AppStarted += (_, _) => StartMicMonitoringIfNeeded();
    }

    private static void StartMicMonitoringIfNeeded()
    {
        if (Settings.AutoStart && Settings.SelectedMic is not null)
        {
            var micActivityMonitorService = IAppHost.GetService<IMicActivityMonitorService>();
            micActivityMonitorService.StartMonitoring(Settings.SelectedMic.Id);
        }
    }
}

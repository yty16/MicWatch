using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using MicWatch.ViewModels;

namespace MicWatch.Views;

[SettingsPageInfo(
    "MicActivityMonitorSettingsPage",   
    "MicWatch",  
    "\uEB80",
    "\uEB7F",
    SettingsPageCategory.External  
)]
public partial class MicActivityMonitorSettingsPage : SettingsPageBase
{
    private readonly SettingsPageViewModel _viewModel;

    public MicActivityMonitorSettingsPage(SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }
}

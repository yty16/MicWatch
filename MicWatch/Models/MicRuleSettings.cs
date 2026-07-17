using CommunityToolkit.Mvvm.ComponentModel;

namespace MicWatch.Models;

public partial class MicRuleSettings : ObservableObject
{
    [ObservableProperty]
    private bool _matchProcessName;

    [ObservableProperty]
    private string? _processName;
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace MicWatch.Models;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    private MicDeviceInfo? _selectedMic;

    [ObservableProperty]
    private bool _autoStart = true;
}

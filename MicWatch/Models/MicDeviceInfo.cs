using CommunityToolkit.Mvvm.ComponentModel;

namespace MicWatch.Models;

public partial class MicDeviceInfo : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;
}

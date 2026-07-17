using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClassIsland.Core.Abstractions.Controls;
using MicWatch.Models;

namespace MicWatch.Controls;

public partial class MicRuleSettingsControl : RuleSettingsControlBase<MicRuleSettings>
{
    public MicRuleSettingsControl()
    {
        InitializeComponent();
    }
}

using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using MicWatch.Services;

namespace MicWatch.Triggers;




[TriggerInfo("micwatch.trigger.micstarted", "当麦克风被调用时", "\uE720")]
public class MicStartedTrigger : TriggerBase
{
    private IMicActivityMonitorService? _micService;

    public override void Loaded()
    {
        _micService = IAppHost.GetService<IMicActivityMonitorService>();
        if (_micService is not null)
        {
            _micService.UsageChanged += OnUsageChanged;
        }
    }

    public override void UnLoaded()
    {
        if (_micService is not null)
        {
            _micService.UsageChanged -= OnUsageChanged;
        }
    }

    private void OnUsageChanged(bool inUse)
    {
        
        if (inUse)
        {
            Trigger();
        }
    }
}

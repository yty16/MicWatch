using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MicWatch.Controls;

public partial class CheckOrDismiss : UserControl
{
    public CheckOrDismiss()
    {
        InitializeComponent();
    }

    
    
    
    public static readonly StyledProperty<bool> StatusProperty =
        AvaloniaProperty.Register<CheckOrDismiss, bool>(nameof(Status), false);

    
    
    
    
    public bool Status
    {
        get => this.GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}

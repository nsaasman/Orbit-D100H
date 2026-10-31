using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbitD100H.Core;

namespace OrbitD100H.App;

public partial class DeviceDiagram : UserControl
{
    private readonly Button[] keys;
    public event Action<int>? ButtonSelected;
    public DeviceDiagram()
    {
        InitializeComponent();
        keys = [Position1, Position2, Position3, Position4, Position5, Position6, Position7, Position8];
        RefreshLanguage();
        Select(ButtonLayout.ProtocolId(1));
    }

    internal void RefreshLanguage()
    {
        foreach (var key in keys)
        {
            var number = int.Parse((string)key.Tag);
            var protocolId = ButtonLayout.ProtocolId(number);
            var label = Text.Get("ButtonPosition", number, ButtonLayout.PositionName(protocolId));
            if (number != 8) key.Content = number.ToString();
            key.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(key, label);
        }
    }

    public static string PositionName(int button) => ButtonLayout.PositionName(button);

    public void Select(int button)
    {
        foreach (var key in keys)
        {
            var selected = int.Parse((string)key.Tag) == ButtonLayout.Number(button);
            key.SetResourceReference(Control.BackgroundProperty, selected ? "Accent" : "DeviceButton");
            key.SetResourceReference(Control.BorderBrushProperty, selected ? "Accent" : "DeviceStroke");
            key.SetResourceReference(Control.ForegroundProperty, selected ? "AccentText" : "DeviceText");
        }
    }

    private void ChooseButton(object sender, RoutedEventArgs e)
    {
        var button = ButtonLayout.ProtocolId(int.Parse((string)((Button)sender).Tag));
        Select(button);
        ButtonSelected?.Invoke(button);
    }
}

using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace AdvancedTooltip.Settings;

[Submenu]
public class ItemLevelSettings
{
    public ToggleNode Enable { get; set; } = new(true);
    public ColorNode TextColor { get; set; } = new ColorBGRA(0, 255, 255, 255);
    public ColorNode BackgroundColor { get; set; } = new ColorBGRA(255, 255, 255, 150);
}
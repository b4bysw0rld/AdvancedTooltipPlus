using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace AdvancedTooltip;

[Submenu]
public class WeaponDpsSettings
{
    [Menu("Enable Weapon DPS", "Display weapon DPS calculation")]
    public ToggleNode EnableWeaponDps { get; set; } = new(true);
    
    [Menu("Always Full Quality", "Calculate weapon DPS as if the weapon had at least 20% quality. Over-quality is still considered.")]
    public ToggleNode AlwaysFullQuality { get; set; } = new(false);

    public ColorNode TextColor { get; set; } = new ColorBGRA(255, 255, 255, 255);
    
    public ColorNode BackgroundColor { get; set; } = new ColorBGRA(255, 255, 255, 150);
    public ColorNode DmgFireColor { get; set; } = new ColorBGRA(255, 0, 0, 255);
    public ColorNode DmgColdColor { get; set; } = new ColorBGRA(0, 128, 255, 255);
    public ColorNode DmgLightningColor { get; set; } = new ColorBGRA(255, 255, 0, 255);
    public ColorNode DmgChaosColor { get; set; } = new ColorBGRA(144, 31, 208, 255);
    public ColorNode PhysicalDamageColor { get; set; } = new ColorBGRA(255, 255, 255, 255);
    public ColorNode ElementalDamageColor { get; set; } = new ColorBGRA(255, 155, 255, 255);
}
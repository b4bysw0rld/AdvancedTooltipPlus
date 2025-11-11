using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace AdvancedTooltip.Settings;

[Submenu]
public class ItemModsSettings
{
    [Menu("Enable Tooltip", "Enable the advanced tooltip display")]
    public ToggleNode EnableTooltip { get; set; } = new(true);
    
    [Menu("Sort mods by tier", "Sort mods so the best tiers are shown at the top instead of their natural order.")]
    public ToggleNode SortModsByTier { get; set; } = new(false);
    
    [Menu("Sort mods by name", "Sort mods alphabetically. Applies after sort by tier when both are active.\nUses short name to sort.")]
    public ToggleNode SortModsByName { get; set; } = new(false);
    
    [Menu("Show short names", "Shows shorthand names for good mods e.g. 'T1 Phys Hybrid'")]
    public ToggleNode ShowShortNames { get; set; } = new(true);
    
    [Menu("Show full names", "Show the 'human names' of mods in tooltip e.g. 'Tyrannical'")]
    public ToggleNode ShowModNames { get; set; } = new(true);
    
    [Menu("Show Tags", "Show mod tags in the advanced tooltip")]
    public ToggleNode ShowTags { get; set; } = new(false);
    
    public ToggleNode StartTagsOnSameLine { get; set; } = new(false);
    public ToggleNode StartStatsOnSameLine { get; set; } = new ToggleNode(false);

    [Menu("Show matching stat names", "When enabled will print matching stat names below mods. Useful for creating filters, etc.")]
    public ToggleNode ShowStatNames { get; set; } = new ToggleNode(false);
    
    [Menu("Enable Mod Count", "Display T1/T2/T3 mod counter in corner")]
    public ToggleNode EnableModCount { get; set; } = new(true);
    
    [Menu("Enable Fast Mods", "Show quick mod tier info next to tooltip")]
    public ToggleNode EnableFastMods { get; set; } = new ToggleNode(false);
    
    public ToggleNode EnableFastModsTags { get; set; } = new ToggleNode(false);
    public ListNode FastModsAnchor { get; set; } = new ListNode();
    
    [Menu("Override Tooltip Hotkey", "Hold this key to draw advanced tooltip on top of the original game tooltip.\nCan be used to avoid drawing over other parts of your HUD.")]
    public HotkeyNode OverrideTooltip { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.LShiftKey);
    
    [Menu("Inverse Override", "The advanced tooltip will always be drawn on top of the original game tooltip unless the button assigned above is held.")]
    public ToggleNode InverseOverride { get; set; } = new(false);
    
    public ColorNode BackgroundColor { get; set; } = new ColorBGRA(0, 0, 0, 220);
    public ColorNode PrefixColor { get; set; } = new ColorBGRA(178, 184, 255, 255);
    public ColorNode SuffixColor { get; set; } = new ColorBGRA(238, 255, 168, 255);
    public ColorNode T1Color { get; set; } = new ColorBGRA(255, 0, 255, 255);
    public ColorNode T2Color { get; set; } = new ColorBGRA(0, 255, 255, 255);
    public ColorNode T3Color { get; set; } = new ColorBGRA(0, 255, 0, 255);
    
    public SpecialColorsSettings SpecialColors { get; set; } = new();
    
    [Menu("Dump Mod Names", "Hotkey to copy mod names to clipboard (for debugging)")]
    public HotkeyNode DumpModNames { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.None);
    
    [Menu("Dump Stat Names", "Hotkey to copy stat names to clipboard (for debugging)")]
    public HotkeyNode DumpStatNames { get; set; } = new HotkeyNode(System.Windows.Forms.Keys.None);

    public bool GetOverrideTooltipState()
    {
        if (!InverseOverride && OverrideTooltip.PressedOnce() || InverseOverride && !OverrideTooltip.PressedOnce())
        {
            return true;
        }
        return false;
    }
}

[Submenu(CollapsedByDefault = true)]
public class SpecialColorsSettings
{
    public ColorNode AbyssalColor { get; set; } = new ColorBGRA(30, 130, 0, 255);
    public ColorNode CraftedColor { get; set; } = new ColorBGRA(180, 96, 255, 255);
}
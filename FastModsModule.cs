using System;
using System.Collections.Generic;
using System.Linq;
using AdvancedTooltip.Settings;
using ExileCore;
using ExileCore.Shared.Enums;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using RectangleF = SharpDX.RectangleF;

namespace AdvancedTooltip;

//it shows the suffix/prefix tier directly near mod on hover item
public class FastModsModule
{
    private readonly Graphics _graphics;
    private readonly ItemModsSettings _modsSettings;

    public FastModsModule(Graphics graphics, ItemModsSettings modsSettings)
    {
        _graphics = graphics;
        _modsSettings = modsSettings;
    }

    public void DrawUiHoverFastMods(List<ModValue> mods, RectangleF tooltip)
    {
        try
        {
            List<ModTierInfo> modTiers = InitializeElements(mods);

            var height = _graphics.MeasureText("P1").Y * 1.5f;
            var fastModsHeight = height * modTiers.Count();

            var drawPos = new Vector2(tooltip.X - 6, tooltip.TopLeft.Y);
            if (_modsSettings.FastModsAnchor.Value == "Bottom")
            {
                drawPos.Y = tooltip.BottomLeft.Y - fastModsHeight;
            }

            for (var i = 0; i < modTiers.Count; i++)
            {
                var modTierInfo = modTiers[i];
                Logger.Log($"Drawing tier #{i}: {modTierInfo.DisplayName} with tags {string.Join(", ", modTierInfo.ModTags.Select(m => m.Name))}");
                var boxHeight = height * modTierInfo.ModLines;

                var textPos = drawPos.Translate(0, boxHeight / 2);

                var textSize = _graphics.DrawText(modTierInfo.DisplayName,
                    textPos, modTierInfo.Color,
                    FontAlign.Right | FontAlign.VerticalCenter);

                textSize.X += 5;
                textPos.X -= textSize.X + 5;

                var initialTextSize = textSize;

                // Tags not supported in PoE1
                // Tag display code removed for PoE1 compatibility

                var rectangleF = new RectangleF(drawPos.X - textSize.X, drawPos.Y, textSize.X + 6,
                    height * modTierInfo.ModLines);
                _graphics.DrawBox(rectangleF, Color.Black);
                _graphics.DrawFrame(rectangleF, Color.Gray, 1);

                _graphics.DrawFrame(new RectangleF(drawPos.X - initialTextSize.X, drawPos.Y, initialTextSize.X + 6,
                    height * modTierInfo.ModLines), Color.Gray, 1);

                drawPos.Y += boxHeight;
                i += modTierInfo.ModLines - 1;
            }
        }
        catch
        {
            //ignored   
        }
    }

    private List<ModTierInfo> InitializeElements(List<ModValue> modValues)
    {
        List<ModTierInfo> modTierInfo = new List<ModTierInfo>();
        foreach (ModValue mod in modValues.OrderBy(m => m.AffixType).ThenBy(m => m.Tier))
        {
            // Skip implicits, uniques, and corrupted mods for FastMods
            if (mod.AffixType == ModType.Unique || mod.AffixType == ModType.Corrupted || mod.IsImplicit)
            {
                continue;
            }

            // Skip if not a prefix or suffix
            if (mod.AffixType != ModType.Prefix && mod.AffixType != ModType.Suffix)
            {
                continue;
            }

            string affix = string.Empty;
            Color color = Color.White;
            
            if (mod.AffixType == ModType.Prefix)
            {
                affix = "P";
                color = _modsSettings.PrefixColor;
            }
            else if (mod.AffixType == ModType.Suffix)
            {
                affix = "S";
                color = _modsSettings.SuffixColor;
            }

            // Color by tier
            color = mod.Tier switch
            {
                1 => _modsSettings.T1Color,
                2 => _modsSettings.T2Color,
                3 => _modsSettings.T3Color,
                _ => color
            };
            
            // Add tier to display if it exists
            if (mod.Tier > 0)
            {
                affix += mod.Tier;
            }
            else
            {
                affix += "?";
            }

            ModTierInfo currentModTierInfo = new ModTierInfo(affix, color);
            
            // Tags don't work in PoE1 (tooltip parsing limitation)
            // Keeping the structure for potential future enhancement
            
            modTierInfo.Add(currentModTierInfo);
        }
        return modTierInfo;
    }

    private class ModTierInfo
    {
        public ModTierInfo(string displayName, Color color)
        {
            DisplayName = displayName;
            Color = color;
        }

        public string DisplayName { get; }
        public Color Color { get; }
        public List<ModTag> ModTags { get; } = new List<ModTag>();

        /// <summary>
        /// Mean twinned mod
        /// </summary>
        public int ModLines { get; set; } = 1;
    }

    public class ModTag
    {
        public ModTag(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; }
        public Color Color { get; }
    }
}
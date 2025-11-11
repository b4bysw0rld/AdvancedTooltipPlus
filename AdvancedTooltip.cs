using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using AdvancedTooltip.Settings;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using RectangleF = SharpDX.RectangleF;

namespace AdvancedTooltip;

public class AdvancedTooltip : BaseSettingsPlugin<AdvancedTooltipSettings>
{
    private const string Backdrop = "textures/backdrop.png";
    private const string BackdropLeft = "textures/backdrop_left.png";
    private Dictionary<int, Color> TColors;
    private FastModsModule _fastMods;
    private HoverItemIcon _hoverItemIcon;

    public override void OnLoad()
    {
        Graphics.InitImage("backdrop.png", Path.Combine(DirectoryFullName, Backdrop));
        Graphics.InitImage("backdrop_left.png", Path.Combine(DirectoryFullName, BackdropLeft));
    }

    public override bool Initialise()
    {
        _fastMods = new FastModsModule(Graphics, Settings.ItemMods);
        Logger.settings = Settings;
        Settings.ItemMods.FastModsAnchor.SetListValues(new List<string> { "Top", "Bottom" });
        if (Settings.ItemMods.FastModsAnchor.Value == null)
        {
            Settings.ItemMods.FastModsAnchor.Value = "Bottom";
        }
        TColors = new Dictionary<int, Color>
        {
            { 1, Settings.ItemMods.T1Color },
            { 2, Settings.ItemMods.T2Color },
            { 3, Settings.ItemMods.T3Color },
        };

        return true;
    }

    private void DumpStatNamesToClipboard(HoverItemIcon icon)
    {
        if (icon == null)
            return;

        var modsComponent = icon?.Item?.GetComponent<Mods>();
        if (modsComponent == null)
            return;

        string statNames = string.Empty;

        foreach (var mod in modsComponent.ItemMods)
        {
            var statName = mod.ModRecord?.StatNames?.FirstOrDefault()?.ToString() ?? "";
            if (!string.IsNullOrEmpty(statName))
                statNames += statName + "\n";
        }

        if (!string.IsNullOrEmpty(statNames))
        {
            Clipboard.SetText(statNames);
            DebugWindow.LogMsg("Hovered item matching stats copied to clipboard.");
        }
    }

    private void DumpModNamesToClipboard(HoverItemIcon icon)
    {
        if (icon == null)
            return;

        var modsComponent = icon?.Item?.GetComponent<Mods>();
        if (modsComponent == null)
            return;

        string modNames = string.Empty;

        foreach (var mod in modsComponent.ItemMods)
        {
            modNames += mod.RawName + "\n";
        }

        if (!string.IsNullOrEmpty(modNames))
        {
            Clipboard.SetText(modNames);
            DebugWindow.LogMsg("Hovered item mod names copied to clipboard.");
        }
    }

    public override Job Tick()
    {
        if (!Initialized) Initialise();

        var hoverItemIcon = GameController?.Game?.IngameState?.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItemIcon != null && hoverItemIcon.IsValid)
        {
            _hoverItemIcon = hoverItemIcon;
        }
        else
        {
            _hoverItemIcon = null;
        }

        if (Settings.ItemMods.DumpStatNames.PressedOnce())
        {
            var thread = new Thread(new ParameterizedThreadStart(param => { DumpStatNamesToClipboard(_hoverItemIcon); }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        if (Settings.ItemMods.DumpModNames.PressedOnce())
        {
            var thread = new Thread(new ParameterizedThreadStart(param => { DumpModNamesToClipboard(_hoverItemIcon); }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        return null;
    }

    public override void Render()
    {
        if (_hoverItemIcon is not { ToolTipType: not ToolTipType.None, ItemFrame: { } tooltip })
        {
            return;
        }

        var poeEntity = _hoverItemIcon.Item;
        if (poeEntity == null || poeEntity.Address == 0 || !poeEntity.IsValid)
        {
            return;
        }

        var modsComponent = poeEntity?.GetComponent<Mods>();
        var origTooltipRect = tooltip.GetClientRect();

        var itemMods = modsComponent?.ItemMods;

        if (itemMods == null ||
            itemMods.Any(x => string.IsNullOrEmpty(x.RawName) && string.IsNullOrEmpty(x.Name)))
            return;

        var origTooltipHeaderOffset = modsComponent.ItemRarity == ItemRarity.Rare || modsComponent.ItemRarity == ItemRarity.Unique
            ? (modsComponent.Identified ? 80 : 50)
            : 50;

        var mods = itemMods.Select(item => new ModValue(item, GameController.Files, modsComponent.ItemLevel,
            GameController.Files.BaseItemTypes.Translate(poeEntity.Path), modsComponent, tooltip)).ToList();

        // Enhanced Tooltip Display
        if (Settings.ItemMods.EnableTooltip.Value && modsComponent.Identified && modsComponent.ItemRarity != ItemRarity.Normal)
        {
            var tooltipTop = origTooltipRect.Bottom + 5;
            var tooltipLeft = origTooltipRect.Left;
            var modPosition = new Vector2(0, 0);

            if (Settings.ItemMods.GetOverrideTooltipState())
            {
                tooltipTop = origTooltipRect.Top + origTooltipHeaderOffset;
                modPosition = new Vector2(tooltipLeft + 5, tooltipTop + 34);
            }
            else
            {
                modPosition = new Vector2(tooltipLeft + 5, tooltipTop + 4);
            }

            // Sort mods
            mods = mods
                .OrderBy(m => !m.IsImplicit)
                .ThenBy(m =>
                {
                    if (m.AffixType == ModType.Corrupted) return -1;
                    if (m.AffixType == ModType.Unique) return 0;
                    return (int)m.AffixType;
                })
                .ThenBy(m => Settings.ItemMods.SortModsByTier ? m.Tier : 0)
                .ThenBy(m =>
                {
                    if (Settings.ItemMods.SortModsByName)
                    {
                        return string.IsNullOrEmpty(m.ShortName) ? 1 : 0;
                    }
                    else
                    {
                        return 0;
                    }
                })
                .ThenBy(m => Settings.ItemMods.SortModsByName ? m.ShortName : null)
                .ToList();

            var height = mods.Where(x => x.Record.StatNames.Any(y => y != null))
                             .Aggregate(modPosition, (position, item) => DrawMod(item, position)).Y -
                         tooltipTop;

            if (height > 4)
            {
                var backgroundHeight = Settings.ItemMods.GetOverrideTooltipState()
                    ? Math.Max(height, origTooltipRect.Height - origTooltipHeaderOffset)
                    : height;
                var modsRect = new RectangleF(tooltipLeft, tooltipTop, origTooltipRect.Width, backgroundHeight);
                Graphics.DrawBox(modsRect, Settings.ItemMods.BackgroundColor);
            }
        }

        // Mod Count Display
        if (Settings.ItemMods.EnableModCount)
        {
            var startPosition = new Vector2(origTooltipRect.TopLeft.X, origTooltipRect.TopLeft.Y);
            var t1 = mods.Count(item => item.CouldHaveTiers() && item.Tier == 1 && (item.AffixType == ModType.Prefix || item.AffixType == ModType.Suffix));
            var t2 = mods.Count(item => item.CouldHaveTiers() && item.Tier == 2 && (item.AffixType == ModType.Prefix || item.AffixType == ModType.Suffix));
            var t3 = mods.Count(item => item.CouldHaveTiers() && item.Tier == 3 && (item.AffixType == ModType.Prefix || item.AffixType == ModType.Suffix));
            if (t1 + t2 + t3 > 0)
            {
                var tierNoteHeight = Graphics.MeasureText("T").Y * (Math.Sign(t1) + Math.Sign(t2) + Math.Sign(t3)) + 5;
                var width = Graphics.MeasureText("T1 x6").X + 10;
                var scale = Settings.ItemMods.ModCountSize;
                Graphics.DrawBox(startPosition, startPosition + new Vector2(width * scale, tierNoteHeight * scale), Settings.ItemMods.BackgroundColor);
                Graphics.DrawFrame(startPosition, startPosition + new Vector2(width * scale, tierNoteHeight * scale), Color.Gray, 1);
                startPosition.X += 5 * scale;
                startPosition.Y += 2 * scale;
                
                var oldScale = Graphics.TextScale;
                Graphics.TextScale = scale;
                if (t1 > 0)
                {
                    startPosition.Y += Graphics.DrawText($"T1 x{t1}", startPosition, Settings.ItemMods.T1Color).Y;
                }

                if (t2 > 0)
                {
                    startPosition.Y += Graphics.DrawText($"T2 x{t2}", startPosition, Settings.ItemMods.T2Color).Y;
                }

                if (t3 > 0)
                {
                    startPosition.Y += Graphics.DrawText($"T3 x{t3}", startPosition, Settings.ItemMods.T3Color).Y;
                }
                Graphics.TextScale = oldScale;
            }
        }

        // Item Level Display
        if (Settings.ItemLevel.Enable.Value)
        {
            var itemLevel = "iLVL: " + Convert.ToString(modsComponent?.ItemLevel ?? 0);
            var itemLevelPosition = new Vector2(origTooltipRect.TopLeft.X, origTooltipRect.TopLeft.Y + origTooltipHeaderOffset);
            var textSize = Graphics.MeasureText(itemLevel);
            var iLvlScale = Settings.ItemLevel.TextSize;
            var backdropSize = new Vector2(385 * 0.4f, 68 * 0.4f);
            backdropSize.X *= (float)Math.Pow(iLvlScale, 0.6);
            backdropSize.Y *= (float)Math.Pow(iLvlScale, 0.6);
            var backdropPosition = new Vector2(itemLevelPosition.X, itemLevelPosition.Y);
            Graphics.DrawImage("backdrop_left.png", new RectangleF(backdropPosition.X, backdropPosition.Y, backdropSize.X, backdropSize.Y),
                Settings.WeaponDps.BackgroundColor);
            itemLevelPosition = itemLevelPosition.Translate(5, 4);
            
            var oldScale = Graphics.TextScale;
            Graphics.TextScale = iLvlScale;
            Graphics.DrawText(itemLevel, itemLevelPosition, Settings.ItemLevel.TextColor);
            Graphics.TextScale = oldScale;
        }

        // Weapon DPS Display
        if (Settings.WeaponDps.EnableWeaponDps && poeEntity.TryGetComponent<Weapon>(out var weaponComponent))
        {
            DrawWeaponDps(origTooltipRect, origTooltipHeaderOffset, poeEntity, mods, weaponComponent);
        }

        // Exit if override mode is active (don't draw fast mods over the original tooltip)
        if (Settings.ItemMods.GetOverrideTooltipState())
        {
            return;
        }

        // Fast Mods Display
        if (Settings.ItemMods.EnableFastMods &&
            (modsComponent == null ||
             modsComponent.ItemRarity == ItemRarity.Magic ||
             modsComponent.ItemRarity == ItemRarity.Rare))
        {
            _fastMods.DrawUiHoverFastMods(mods, origTooltipRect);
        }
    }

    private Vector2 DrawMod(ModValue item, Vector2 position)
    {
        const float epsilon = 0.001f;
        const int marginBottom = 4;
        var oldPosition = position;
        var settings = Settings.ItemMods;

        var (affixTypeText, color) = item.AffixType switch
        {
            ModType.Prefix => ("[P]", settings.PrefixColor.Value),
            ModType.Suffix => ("[S]", settings.SuffixColor.Value),
            ModType.Corrupted => ("[C]", new Color(220, 20, 60)),
            ModType.Unique => ("[U]", new Color(255, 140, 0)),
            ModType.Enchantment => ("[E]", new Color(255, 0, 255)),
            ModType.Nemesis => ("[NEM]", new Color(255, 20, 147)),
            ModType.BloodLines => ("[BLD]", new Color(0, 128, 0)),
            ModType.Torment => ("[TOR]", new Color(178, 34, 34)),
            ModType.Tempest => ("[TEM]", new Color(65, 105, 225)),
            ModType.Talisman => ("[TAL]", new Color(218, 165, 32)),
            ModType.EssenceMonster => ("[ESS]", new Color(139, 0, 139)),
            ModType.Bestiary => ("[BES]", new Color(255, 99, 71)),
            ModType.DelveArea => ("[DEL]", new Color(47, 79, 79)),
            ModType.SynthesisA => ("[SYN]", new Color(255, 105, 180)),
            ModType.SynthesisGlobals => ("[SGS]", new Color(186, 85, 211)),
            ModType.SynthesisBonus => ("[SYB]", new Color(100, 149, 237)),
            ModType.Blight => ("[BLI]", new Color(0, 100, 0)),
            ModType.BlightTower => ("[BLT]", new Color(0, 100, 0)),
            ModType.MonsterAffliction => ("[MAF]", new Color(123, 104, 238)),
            ModType.FlaskEnchantmentEnkindling => ("[FEE]", new Color(255, 165, 0)),
            ModType.FlaskEnchantmentInstilling => ("[FEI]", new Color(255, 165, 0)),
            ModType.ExpeditionLogbook => ("[LOG]", new Color(218, 165, 32)),
            ModType.ScourgeUpside => ("[SCU]", new Color(218, 165, 32)),
            ModType.ScourgeDownside => ("[SCD]", new Color(218, 165, 32)),
            ModType.ScourgeMap => ("[SCM]", new Color(218, 165, 32)),
            ModType.ExarchImplicit => ("[EXI]", new Color(255, 69, 0)),
            ModType.EaterImplicit => ("[EAT]", new Color(255, 69, 0)),
            ModType.WeaponTree => ("[CRU]", new Color(254, 114, 53)),
            ModType.WeaponTreeRecombined => ("[CRC]", new Color(254, 114, 53)),
            _ => ("[?]", new Color(211, 211, 211))
        };

        // Override for implicit mods
        if (item.IsImplicit)
        {
            affixTypeText = "[I]";
            color = new Color(218, 219, 193, 156);
        }

        var affixTypeWidth = Graphics.MeasureText(affixTypeText + " ").X;
        var affixTypeMinWidth = Graphics.MeasureText("[P] ").X;
        affixTypeWidth = Math.Max(affixTypeMinWidth, affixTypeWidth);

        Graphics.DrawText(affixTypeText, position, color);

        if (item.AffixType != ModType.Unique && item.AffixType != ModType.Corrupted)
        {
            var totalTiers = item.TotalTiers;
            Color affixTextColor = (item.AffixType, totalTiers > 1) switch
            {
                (ModType.Prefix, true) => TColors.GetValueOrDefault(item.Tier, settings.PrefixColor),
                (ModType.Suffix, true) => TColors.GetValueOrDefault(item.Tier, settings.SuffixColor),
                (ModType.Prefix, false) => settings.PrefixColor,
                (ModType.Suffix, false) => settings.SuffixColor,
                _ => default
            };

            // Check for special domains
            affixTextColor = item.Record.Domain switch
            {
                ModDomain.Crafted => settings.SpecialColors.CraftedColor,
                _ => affixTextColor
            };

            var affixTierText = (totalTiers > 1 ? $"T{item.Tier} " : string.Empty);
            var affixTierSize = item.AffixType switch
            {
                ModType.Prefix => Graphics.DrawText(affixTierText, position.Translate(affixTypeWidth), affixTextColor),
                ModType.Suffix => Graphics.DrawText(affixTierText, position.Translate(affixTypeWidth), affixTextColor),
                _ => default
            };

            // Special handling for crafted mods
            if (item.Record.Domain == ModDomain.Crafted)
            {
                affixTierSize = Graphics.DrawText("Crafted ", position.Translate(affixTypeWidth), affixTextColor);
            }

            // Show short names if enabled
            if (Settings.ItemMods.ShowShortNames && item.ShortName.Length > 0)
            {
                affixTierSize.X += Graphics.DrawText($"{item.ShortName}", position.Translate(affixTypeWidth + affixTierSize.X), affixTextColor).X;
            }

            // Show full mod names if enabled
            var affixTextSize = Settings.ItemMods.ShowModNames
                ? Graphics.DrawText(
                    item.ShortName.Length > 0 || (affixTierSize.X > 0 && !item.CouldHaveTiers())
                    ? $" | \"{item.AffixText}\""
                    : $"\"{item.AffixText}\"",
                    position.Translate(affixTypeWidth + affixTierSize.X), affixTextColor)
                : Vector2.Zero;

            // Show tags if enabled
            var tagSize = Vector2.Zero;
            if (Settings.ItemMods.ShowTags && item.Tags.Count > 0)
            {
                var tagsPosition = Vector2.Zero;
                if (Settings.ItemMods.StartTagsOnSameLine)
                {
                    tagsPosition = new Vector2(position.X + affixTypeWidth + affixTierSize.X + affixTextSize.X, position.Y);
                    tagSize.X += Graphics.DrawText(" ", tagsPosition, affixTextColor).X;
                }
                else
                {
                    tagsPosition = new Vector2(position.X + affixTypeWidth, position.Y + Math.Max(affixTierSize.Y, affixTextSize.Y));
                }

                foreach (var tag in item.Tags)
                {
                    tagSize.X += Graphics.DrawText($"[{tag}] ", tagsPosition + tagSize, GetTagColor(tag)).X;
                }
                tagSize.Y = item.Tags.Count > 0 ? Graphics.MeasureText(item.Tags[0]).Y : 0;
                
                if (!Settings.ItemMods.StartTagsOnSameLine)
                {
                    position.Y += tagSize.Y;
                }
            }

            if (Settings.ItemMods.StartStatsOnSameLine)
            {
                position.X += affixTierSize.X + affixTextSize.X + (Settings.ItemMods.StartTagsOnSameLine ? tagSize.X : 0);
            }
            else
            {
                position.Y += Math.Max(affixTierSize.Y, affixTextSize.Y);
            }
        }

        // Display human-readable mod text with stats
        if (!string.IsNullOrEmpty(item.HumanName))
        {
            var displayText = item.HumanName;
            var txSize = Graphics.DrawText(Settings.ItemMods.StartStatsOnSameLine ? $" {displayText}" : $"{displayText}", 
                position.Translate(affixTypeWidth), Color.Gainsboro);
            position.Y += txSize.Y;
        }

        // Show stat names for debugging if enabled
        if (Settings.ItemMods.ShowStatNames)
        {
            var statName = item.Record.StatNames.FirstOrDefault()?.ToString() ?? "";
            if (!string.IsNullOrEmpty(statName))
            {
                var txSize = Graphics.DrawText(statName, position.Translate(affixTypeWidth), Color.Gray);
                position.Y += txSize.Y;
            }
        }

        return Math.Abs(position.Y - oldPosition.Y) > epsilon
            ? oldPosition with { Y = position.Y + marginBottom }
            : oldPosition;
    }

    private Color GetTagColor(string tag)
    {
        return tag switch
        {
            "Fire" => Color.Red,
            "Cold" => new Color(41, 102, 241),
            "Life" => Color.Magenta,
            "Lightning" => Color.Yellow,
            "Physical" => new Color(225, 170, 20),
            "Critical" => new Color(168, 220, 26),
            "Mana" => new Color(20, 240, 255),
            "Attack" => new Color(240, 100, 30),
            "Speed" => new Color(0, 255, 192),
            "Caster" => new Color(216, 0, 255),
            "Elemental" => Color.White,
            "Gem Level" => new Color(200, 230, 160),
            _ => Color.Gray
        };
    }

    private void DrawWeaponDps(RectangleF clientRect, float headerOffset, Entity itemEntity, List<ModValue> modValues, Weapon weaponComponent)
    {
        if (weaponComponent == null) return;
        if (!itemEntity.IsValid) return;
        var aSpd = (float)Math.Round(1000f / weaponComponent.AttackTime, 2);
        var cntDamages = Enum.GetValues(typeof(DamageType)).Length;
        var doubleDpsPerStat = new float[cntDamages];
        float physDmgMultiplier = 1;
        var physHi = weaponComponent.DamageMax;
        var physLo = weaponComponent.DamageMin;

        foreach (var mod in modValues)
        {
            foreach (var (stat, range, value) in mod.Record.StatNames.Zip(mod.Record.StatRange, mod.StatValue))
            {
                if (range.Min == 0 && range.Max == 0) continue;
                if (stat == null) continue;

                switch (stat.Key)
                {
                    case "physical_damage_+%":
                    case "local_physical_damage_+%":
                        physDmgMultiplier += value / 100f;
                        break;

                    case "local_attack_speed_+%":
                        aSpd *= (100f + value) / 100;
                        break;

                    case "local_minimum_added_physical_damage":
                        physLo += value;
                        break;
                    case "local_maximum_added_physical_damage":
                        physHi += value;
                        break;

                    case "local_minimum_added_fire_damage":
                    case "local_maximum_added_fire_damage":
                    case "unique_local_minimum_added_fire_damage_when_in_main_hand":
                    case "unique_local_maximum_added_fire_damage_when_in_main_hand":
                        doubleDpsPerStat[(int)DamageType.Fire] += value;
                        break;

                    case "local_minimum_added_cold_damage":
                    case "local_maximum_added_cold_damage":
                    case "unique_local_minimum_added_cold_damage_when_in_off_hand":
                    case "unique_local_maximum_added_cold_damage_when_in_off_hand":
                        doubleDpsPerStat[(int)DamageType.Cold] += value;
                        break;

                    case "local_minimum_added_lightning_damage":
                    case "local_maximum_added_lightning_damage":
                        doubleDpsPerStat[(int)DamageType.Lightning] += value;
                        break;

                    case "unique_local_minimum_added_chaos_damage_when_in_off_hand":
                    case "unique_local_maximum_added_chaos_damage_when_in_off_hand":
                    case "local_minimum_added_chaos_damage":
                    case "local_maximum_added_chaos_damage":
                        doubleDpsPerStat[(int)DamageType.Chaos] += value;
                        break;
                }
            }
        }

        var settings = Settings.WeaponDps;

        Color[] elementalDmgColors =
        {
            Color.White, settings.DmgFireColor, settings.DmgColdColor, settings.DmgLightningColor,
            settings.DmgChaosColor
        };

        var component = itemEntity.GetComponent<Quality>();
        if (component == null) return;
        
        var qualityMultiplier = (component.ItemQuality + 100) / 100f;
        if (Settings.WeaponDps.AlwaysFullQuality && qualityMultiplier < 1.2f)
        {
            qualityMultiplier = 1.2f;
        }
        
        physLo = (int)Math.Round(physLo * qualityMultiplier * physDmgMultiplier);
        physHi = (int)Math.Round(physHi * qualityMultiplier * physDmgMultiplier);
        doubleDpsPerStat[(int)DamageType.Physical] = physLo + physHi;

        aSpd = (float)Math.Round(aSpd, 2);
        var pDps = doubleDpsPerStat[(int)DamageType.Physical] / 2 * aSpd;
        float eDps = 0;
        var firstEmg = 0;
        Color dpsColor = settings.PhysicalDamageColor;

        for (var i = 1; i < cntDamages; i++)
        {
            eDps += doubleDpsPerStat[i] / 2 * aSpd;
            if (!(doubleDpsPerStat[i] > 0)) continue;

            if (firstEmg == 0)
            {
                firstEmg = i;
                dpsColor = elementalDmgColors[i];
            }
            else
                dpsColor = settings.ElementalDamageColor;
        }

        var textPosition = new Vector2(clientRect.Right - 8, clientRect.Y);
        var backdropSize = new Vector2(385, 68);
        var dpsScale = Settings.WeaponDps.DpsTextSize;
        backdropSize.X *= dpsScale;
        backdropSize.Y *= dpsScale;
        var backdropPosition = new Vector2(textPosition.X - backdropSize.X + 10, textPosition.Y - 2);
        Graphics.DrawImage("backdrop.png", new RectangleF(backdropPosition.X, backdropPosition.Y, backdropSize.X, backdropSize.Y),
            settings.BackgroundColor);
        textPosition = textPosition.Translate(0, 4);
        
        var oldScale = Graphics.TextScale;
        Graphics.TextScale = dpsScale;
        
        var pDpsSize = pDps > 0
            ? Graphics.DrawText("pDPS " + pDps.ToString("#"), textPosition, FontAlign.Right)
            : Vector2.Zero;

        var eDpsSize = eDps > 0
            ? Graphics.DrawText("eDPS " + eDps.ToString("#"), textPosition.Translate(0, pDpsSize.Y), dpsColor,
                FontAlign.Right)
            : Vector2.Zero;

        var dps = pDps + eDps;

        if (dps >= pDps || dps >= eDps)
        {
            var dpsSize = dps > 0
                ? Graphics.DrawText("Total " + dps.ToString("#"), textPosition.Translate(0, pDpsSize.Y + eDpsSize.Y),
                    Color.White, FontAlign.Right)
                : Vector2.Zero;
        }
        
        Graphics.TextScale = oldScale;
    }
}
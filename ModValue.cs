using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.FilesInMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Models;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;

namespace AdvancedTooltip;

public class ModValue
{
    public ModType AffixType { get; }
    public bool IsImplicit { get; }
    public bool IsCrafted { get; }
    public string AffixText { get; }
    public Color Color { get; }
    public ModsDat.ModRecord Record { get; }
    public string HumanName { get; }
    public string ShortName { get; }
    public int[] StatValue { get; }
    public int Tier { get; }
    public int TotalTiers { get; } = 1;
    public List<string> Tags { get; } = new List<string>();

    public ModValue(ItemMod mod, FilesContainer fs, int iLvl, BaseItemType baseItem, Mods modsComponent = null, Element tooltip = null)
    {
        Logger.Log($"Inspecting mod {mod.RawName}");
        var baseClassName = baseItem.ClassName.ToLower().Replace(' ', '_');
        Record = fs.Mods.records[mod.RawName];
        HumanName = !string.IsNullOrEmpty(mod.DisplayName) ? mod.DisplayName : mod.Name;
        ShortName = ShortModNames.GetByGroup(mod.Group);
        AffixType = Record.AffixType;
        AffixText = string.IsNullOrEmpty(Record.UserFriendlyName) ? Record.Key : Record.UserFriendlyName;
        IsCrafted = Record.Domain == ModDomain.Crafted;
        IsImplicit = modsComponent?.ImplicitMods?.Any(iMod => iMod.RawName == mod.RawName) ?? false;
        StatValue = mod.Values.ToArray();
        Tier = 0;
        var subOptimalTierDistance = 0;

        // Try to extract tier and tags from tooltip if available
        if (tooltip != null && tooltip.IsValid)
        {
            try
            {
                var tooltipLine = Util.FindUIElement(tooltip.Children, x => x.Children, 
                    c => c.Text != null && !string.IsNullOrEmpty(HumanName) && c.Text.Contains(HumanName)).FirstOrDefault();
                
                if (tooltipLine != null)
                {
                    Logger.Log($"{AffixType} {Record.Group}: found matching tooltip line!");
                    try
                    {
                        string tiertext = tooltipLine.Parent?.Children?.LastOrDefault()?.Children?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrEmpty(tiertext))
                        {
                            Regex tier = new Regex(@".*T([0-9]+).*", RegexOptions.Compiled);
                            var match = tier.Match(tiertext);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedTier))
                            {
                                Tier = parsedTier;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to parse Tier from tooltip line: {ex.Message}");
                    }
                }

                // Try to find tier info from hover icon
                var tooltipTierIcon = Util.FindUIElement(tooltip.Children, x => x.Children,
                    c => c.IsValid && c.Tooltip != null && !string.IsNullOrEmpty(c.Tooltip.Text) && 
                         !string.IsNullOrEmpty(mod.DisplayName) && c.Tooltip.Text.Contains(mod.DisplayName)).FirstOrDefault();
                
                if (tooltipTierIcon != null)
                {
                    Logger.Log($"{AffixType} {Record.Group}: found matching tooltip tier icon!");
                    try
                    {
                        string tiertext = tooltipTierIcon.Tooltip?.Text;
                        if (!string.IsNullOrEmpty(tiertext))
                        {
                            Regex tier = new Regex(@".*Tier\:\s([0-9]+).*", RegexOptions.Compiled);
                            var match = tier.Match(tiertext);
                            if (match.Success && Tier <= 0)
                            {
                                if (int.TryParse(match.Groups[1].Value, out var parsedTier))
                                {
                                    Tier = parsedTier;
                                }
                            }

                            // Extract tags
                            Regex tag = new Regex(@"\<rgb\(\d+\,\d+\,\d+\)\>\{([\w ]+)\}", RegexOptions.Compiled);
                            var tagMatches = tag.Matches(tiertext);
                            foreach (Match tagMatch in tagMatches)
                            {
                                Tags.Add(tagMatch.Groups[1].Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to parse Tier and tags from tooltip tier icon: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error parsing tooltip: {ex.Message}");
            }
        }

        foreach (var tag in Tags)
        {
            Logger.Log($"Found Tag {tag}");
        }

        Logger.Log($"{AffixType} {Record.Group}: Looking up mod record in files..");

        // Skip tier calculation for unique/implicit/crafted mods
        if (AffixType.ToString() == "Unique" || IsImplicit || IsCrafted)
        {
            double hue = Tier == 1 ? 180 : 120 - Math.Min(Tier - 1, 3) * 40;
            Color = ConvertHelper.ColorFromHsv(hue, Tier == 1 ? 0 : 1, 1);
            Tier = 0; // Crafted mods should not have a tier
            return;
        }

        if (fs.Mods.recordsByTier.TryGetValue(Tuple.Create(Record.Group, Record.AffixType), out var allTiers))
        {
            var tierFound = false;
            TotalTiers = 0;
            var modRecordKey = Record.Key.Where(char.IsLetter).ToArray();
            var optimizedListTiers = allTiers.Where(x => x.Key.StartsWith(new string(modRecordKey), StringComparison.Ordinal)).ToList();

            Logger.Log($"Found {optimizedListTiers.Count} tier records for {Record.Group}");

            foreach (var tmp in optimizedListTiers)
            {
                var keyrcd = tmp.Key.Where(char.IsLetter).ToArray();

                if (!keyrcd.SequenceEqual(modRecordKey))
                {
                    continue;
                }

                TotalTiers++;

                if (tmp.Equals(Record))
                {
                    if (Tier <= 0)
                    {
                        Tier = TotalTiers;
                        Logger.Log($"Matched tier {Tier} from recordsByTier for {Record.Key}");
                    }
                    tierFound = true;
                }

                if (!tierFound && tmp.MinLevel <= iLvl)
                {
                    subOptimalTierDistance++;
                }
            }

            if (Tier <= 0 && !string.IsNullOrEmpty(Record.Tier))
            {
                var tierNumber = new string(Record.Tier.Where(char.IsDigit).ToArray());

                if (int.TryParse(tierNumber, out var result))
                {
                    Tier = result;
                    TotalTiers = optimizedListTiers.Count;
                    Logger.Log($"Parsed tier {Tier} from Record.Tier string '{Record.Tier}'");
                }
            }

            if (Tier <= 0)
            {
                Logger.LogError($"Failed to determine tier for mod: {Record.Key} (Group: {Record.Group}, AffixType: {AffixType}, HumanName: {HumanName})");
            }

            double hue = TotalTiers == 1 ? 180 : 120 - Math.Min(subOptimalTierDistance, 3) * 40;
            Color = ConvertHelper.ColorFromHsv(hue, TotalTiers == 1 ? 0 : 1, 1);
        }
        else
        {
            Logger.LogError($"No recordsByTier entry found for mod: {Record.Key} (Group: {Record.Group}, AffixType: {AffixType})");
            
            // Fallback color calculation
            double hue = Tier == 1 ? 180 : 120 - Math.Min(Tier > 0 ? Tier - 1 : 0, 3) * 40;
            Color = ConvertHelper.ColorFromHsv(hue, Tier == 1 ? 0 : 1, 1);
        }
    }

    public bool CouldHaveTiers()
    {
        return TotalTiers > 1;
    }
}
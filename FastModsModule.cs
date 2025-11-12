using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AdvancedTooltip.Settings;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using RectangleF = SharpDX.RectangleF;
using UiElement = ExileCore.PoEMemory.MemoryObjects.Element;

namespace AdvancedTooltip;

// Shows the suffix/prefix tier directly near mod on hover item
public class FastModsModule
{
    private readonly Graphics _graphics;
    private readonly ItemModsSettings _modsSettings;
    private long _lastTooltipAddress;
    private UiElement _regularModsElement;
    private readonly List<ModTierInfo> _mods = new List<ModTierInfo>();
    private static readonly Regex FracturedRegex = new Regex(@"\<fractured\>\{([^\n]*\n[^\n]*)(?:\n\<italic\>\{[^\n]*\})?\}(?=\n|$)", RegexOptions.Compiled);

    public FastModsModule(Graphics graphics, ItemModsSettings modsSettings)
    {
        _graphics = graphics;
        _modsSettings = modsSettings;
    }

    // New PoE1-stable path: parse tooltip to derive P/S and tiers (no tags)
    public void DrawUiHoverFastMods(UiElement tooltip)
    {
        try
        {
            InitializeElements(tooltip);

            if (_regularModsElement is not { IsVisibleLocal: true } || _mods.Count == 0)
                return;

            var rect = _regularModsElement.GetClientRectCache;
            var height = rect.Height / _mods.Count;

            var drawPos = new Vector2(tooltip.GetClientRectCache.X - 3, rect.TopLeft.Y);
            if (_modsSettings.FastModsAnchor.Value == "Bottom")
            {
                drawPos.Y = rect.Bottom - height * _mods.Count;
            }

            for (var i = 0; i < _mods.Count; i++)
            {
                var modTierInfo = _mods[i];
                var boxHeight = height * modTierInfo.ModLines;

                var textPos = drawPos.Translate(0, boxHeight / 2);

                var textSize = _graphics.DrawText(modTierInfo.DisplayName,
                    textPos, modTierInfo.Color,
                    FontAlign.Right | FontAlign.VerticalCenter);

                textSize.X += 5;
                textPos.X -= textSize.X + 5;

                var rectangleF = new RectangleF(drawPos.X - textSize.X - 3, drawPos.Y, textSize.X + 6,
                    height * modTierInfo.ModLines);
                _graphics.DrawBox(rectangleF, Color.Black);
                _graphics.DrawFrame(rectangleF, Color.Gray, 1);

                drawPos.Y += boxHeight;
                i += modTierInfo.ModLines - 1;
            }
        }
        catch
        {
            //ignored   
        }
    }

    private void InitializeElements(UiElement tooltip)
    {
        _mods.Clear();

        var modsRoot = tooltip.GetChildAtIndex(1);
        if (modsRoot == null)
            return;

        UiElement extendedModsElement = null;
        UiElement regularModsElement = null;
        for (var i = modsRoot.Children.Count - 1; i >= 0; i--)
        {
            var element = modsRoot.Children[i];
            if (element.ChildCount is > 2 or 0)
            {
                continue;
            }

            var textElements = GetExtendedModsTextElements(element);
            var elementText = textElements.FirstOrDefault()?.Text;
            if (!string.IsNullOrEmpty(elementText) &&
                (elementText.StartsWith("<smaller>", StringComparison.Ordinal) ||
                 elementText.StartsWith("<fractured>{<smaller>", StringComparison.Ordinal)) &&
                element.TextNoTags?.StartsWith("Allocated Crucible", StringComparison.Ordinal) != true)
            {
                extendedModsElement = element;
                regularModsElement = modsRoot.Children[i - 1];
                break;
            }
        }

        if (regularModsElement == null)
        {
            _regularModsElement = null;
            _lastTooltipAddress = default;
            return;
        }
        if (_lastTooltipAddress != tooltip.Address ||
            _regularModsElement?.Address != regularModsElement.Address)
        {
            _lastTooltipAddress = tooltip.Address;
            _regularModsElement = regularModsElement;
            ParseItemHover(tooltip, extendedModsElement);
        }
    }

    private static List<UiElement> GetExtendedModsTextElements(UiElement element)
    {
        return element.Children.SelectMany(x => x.Children).Where(x => x.ChildCount == 1).Select(x => x[0]).Where(x => x != null).ToList();
    }

    private static string RemoveFractured(string x)
    {
        return FracturedRegex.Replace(x, "$1");
    }

    private void ParseItemHover(UiElement tooltip, UiElement extendedModsElement)
    {
        var extendedModsStr = string.Join("\n", GetExtendedModsTextElements(extendedModsElement).Select(x => x.Text));
        var extendedModsLines = RemoveFractured(extendedModsStr.Replace("\r\n", "\n")).Split('\n');

        var regularModsStr = _regularModsElement.GetTextWithNoTags(2500);
        var regularModsLines = regularModsStr.Replace("\r\n", "\n").Split('\n');

        ModTierInfo currentModTierInfo = null;
        var modsDict = new Dictionary<string, ModTierInfo>();

        foreach (var extendedModsLine in extendedModsLines)
        {
            if (extendedModsLine.StartsWith("<italic>", StringComparison.Ordinal))
                continue;

            if (extendedModsLine.StartsWith("<smaller>", StringComparison.Ordinal) || extendedModsLine.StartsWith("<crafted>", StringComparison.Ordinal))
            {
                var isPrefix = extendedModsLine.Contains("Prefix");
                var isSuffix = extendedModsLine.Contains("Suffix");
                if (!isPrefix && !isSuffix)
                    continue;

                var affix = isPrefix ? "P" : "S";
                Color color = isPrefix ? _modsSettings.PrefixColor : _modsSettings.SuffixColor;

                const string tierPrefix = "(Tier: ";
                const string rankPrefix = "(Rank: ";
                var tierPos = extendedModsLine.IndexOf(tierPrefix, StringComparison.Ordinal);
                var isRank = false;
                if (tierPos != -1)
                {
                    tierPos += tierPrefix.Length;
                }
                else
                {
                    tierPos = extendedModsLine.IndexOf(rankPrefix, StringComparison.Ordinal);
                    if (tierPos != -1)
                    {
                        tierPos += rankPrefix.Length;
                        isRank = true;
                    }
                }

                if (tierPos != -1 &&
                    (int.TryParse(extendedModsLine.Substring(tierPos, 2), out var tier) ||
                     int.TryParse(extendedModsLine.Substring(tierPos, 1), out tier)))
                {
                    affix += isRank ? $" Rank{tier}" : tier.ToString();
                    color = tier switch
                    {
                        1 => _modsSettings.T1Color,
                        2 => _modsSettings.T2Color,
                        3 => _modsSettings.T3Color,
                        _ => color
                    };
                }
                else if (extendedModsLine.Contains("Essence"))
                {
                    affix += "(Ess)";
                }

                currentModTierInfo = new ModTierInfo(affix, color);
                continue;
            }

            if (extendedModsLine.StartsWith("<", StringComparison.Ordinal) && !char.IsLetterOrDigit(extendedModsLine[0]))
            {
                currentModTierInfo = null;
                continue;
            }

            if (currentModTierInfo != null)
            {
                var modLine = Regex.Replace(extendedModsLine, @"\([\d-.]+\)", string.Empty);
                modLine = Regex.Replace(modLine, @"[\d-.]+", "#");
                modLine = Regex.Replace(modLine, @"\s\([\d]+% Increased\)", string.Empty);
                modLine = modLine.Replace(" (#% Increased)", string.Empty);
                if (modLine.StartsWith('+'))
                    modLine = modLine[1..];

                modsDict.TryAdd(modLine, currentModTierInfo);
            }
        }

        var modTierInfos = new List<ModTierInfo>();
        foreach (var regularModsLine in regularModsLines)
        {
            var modFixed = regularModsLine;
            if (modFixed.StartsWith('+'))
                modFixed = modFixed[1..];
            modFixed = Regex.Replace(modFixed, @"[\d-.]+", "#");

            var found = false;
            foreach (var keyValuePair in modsDict)
            {
                if (modFixed.Contains(keyValuePair.Key))
                {
                    found = true;
                    modTierInfos.Add(keyValuePair.Value);
                    break;
                }
            }

            if (!found)
            {
                var modTierInfo = new ModTierInfo("?", Color.Gray);
                modTierInfos.Add(modTierInfo);
            }
        }

        if (modTierInfos.Count > 1)
        {
            for (var i = 1; i < modTierInfos.Count; i++)
            {
                var info = modTierInfos[i];
                var prevInfo = modTierInfos[i - 1];
                if (info == prevInfo)
                {
                    info.ModLines++;
                }
            }
        }

        _mods.Clear();
        _mods.AddRange(modTierInfos);
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
        /// <summary>Mean twinned mod</summary>
        public int ModLines { get; set; } = 1;
    }
}
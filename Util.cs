using ExileCore;
using AdvancedTooltip.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedTooltip
{
    internal static class Util
    {
        internal static IEnumerable<T> FindUIElement<T>(this IEnumerable<T> items, Func<T, IEnumerable<T>> childSelector, Func<T, bool> predicate)
        {
            var queue = new Queue<T>(items);
            var visited = new HashSet<T>();

            while (queue.Any())
            {
                var next = queue.Dequeue();
                if (next != null && visited.Add(next))
                {
                    if (predicate(next))
                    {
                        yield return next;
                    }
                    foreach (var child in childSelector(next) ?? Enumerable.Empty<T>())
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }
    }

    public class Logger
    {
        internal static AdvancedTooltipSettings settings;

        internal static void Log(string msg)
        {
            if (settings?.DebugSettings?.ShowDebug?.Value == true)
                DebugWindow.LogMsg(msg);
        }

        internal static void Log(string msg, int time)
        {
            if (settings?.DebugSettings?.ShowDebug?.Value == true)
                DebugWindow.LogMsg(msg, time);
        }

        internal static void LogError(string msg)
        {
            if (settings?.DebugSettings?.ShowDebug?.Value == true)
                DebugWindow.LogError(msg);
        }

        internal static void LogError(string msg, int time)
        {
            if (settings?.DebugSettings?.ShowDebug?.Value == true)
                DebugWindow.LogError(msg, time);
        }
    }
}


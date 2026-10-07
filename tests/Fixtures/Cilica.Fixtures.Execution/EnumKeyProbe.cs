using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public enum EnumKey { Minimum = int.MinValue, Negative = -1, Zero = 0, Seven = 7, Maximum = int.MaxValue }
    public enum AliasEnumKey { Zero = 0, Alias = 0 }
    public enum WideEnumKey : long { Zero = 0 }
    public static class EnumKeyProbe
    {
        public static int Dictionary(int a, int b)
        {
            var map = new Dictionary<EnumKey, int>();
            for (int i = 0; i < 40; i++) map[(EnumKey)unchecked(a + i * 7)] = unchecked(b + i);
            int result = 0;
            for (int i = 0; i < 40; i++)
            {
                var key = (EnumKey)unchecked(a + i * 7);
                if (map.TryAdd(key, a)) result++;
                if (map.TryGetValue(key, out int value)) result = unchecked(result * 31 + value);
                if ((i & 1) == 0) map.Remove(key);
            }
            map[(EnumKey)a] = b;
            return unchecked(result + map[(EnumKey)a] + map.Count);
        }
        public static int Aliases(int a, int b)
        {
            var map = new Dictionary<AliasEnumKey, int>(); map.TryAdd(AliasEnumKey.Zero, a);
            int result = map.TryAdd(AliasEnumKey.Alias, b) ? 5 : 7;
            map[AliasEnumKey.Alias] = b;
            map.TryGetValue(AliasEnumKey.Zero, out int value);
            return unchecked(result + value);
        }
        public static int Set(int a, int b)
        {
            var set = new HashSet<EnumKey>(); int result = 0;
            for (int i = 0; i < 40; i++) if (set.Add((EnumKey)unchecked(a + i * 7))) result++;
            for (int i = 0; i < 40; i++)
            {
                var key = (EnumKey)unchecked(a + i * 7);
                if (set.Contains(key)) result = unchecked(result * 31 + i);
                if ((i & 1) == 0) set.Remove(key);
            }
            if (set.Add((EnumKey)a)) result = unchecked(result + b);
            result = unchecked(result + set.Count); set.Clear();
            return unchecked(result + set.Count);
        }
        public static int Views(int a, int b)
        {
            var map = new Dictionary<EnumKey, EnumKey>(); map[(EnumKey)a] = (EnumKey)b;
            int result = map.ContainsValue((EnumKey)b) ? 7 : 11;
            foreach (var pair in map) result = unchecked(result * 31 + (int)pair.Key + (int)pair.Value);
            map.Clear(); return unchecked(result + map.Count);
        }
        public static int UnsupportedAliasFormatting(int a, int b) { var map = new Dictionary<AliasEnumKey, int>(); map.TryAdd(AliasEnumKey.Zero, b); return map[AliasEnumKey.Alias]; }
        public static int UnsupportedWide(int a, int b) { var map = new Dictionary<WideEnumKey, int>(); map[(WideEnumKey)a] = b; return map.Count; }
        public static int UnsupportedHidden(int a, int b) { var map = new Dictionary<PreparationFlags, int>(); map[(PreparationFlags)a] = b; return map.Count; }
    }
}

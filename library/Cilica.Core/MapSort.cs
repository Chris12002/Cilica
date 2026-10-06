// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ArraySortHelper's comparison-based introsort, adapted from spans to an int map.
using System;
namespace Cilica.Runtime
{
    public interface IMapComparison { int Compare(int first, int second); }
    internal static class MapSort
    {
        internal static void Sort(int[] map, IMapComparison comparer)
        {
            try
            {
                if (map.Length > 1)
                {
                    int bits = 0; for (uint n = (uint)map.Length; n != 0; n >>= 1) bits++;
                    IntroSort(map, 0, map.Length, 2 * bits, comparer);
                }
            }
            catch (IndexOutOfRangeException)
            {
                // LINQ sorts its Int32 index map with Comparison<Int32> upstream.
                throw new ArgumentException("Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: 'System.Comparison`1[System.Int32]'.");
            }
            catch (Exception inner) { throw new InvalidOperationException("Failed to compare two elements in the array.", inner); }
        }
        private static void Swap(int[] map, int offset, int first, int second)
        { int value = map[offset + first]; map[offset + first] = map[offset + second]; map[offset + second] = value; }
        private static void SwapIfGreater(int[] map, int offset, int first, int second, IMapComparison comparer)
        { if (comparer.Compare(map[offset + first], map[offset + second]) > 0) Swap(map, offset, first, second); }
        private static void IntroSort(int[] map, int offset, int count, int depth, IMapComparison comparer)
        {
            int size = count;
            while (size > 1)
            {
                if (size <= 16)
                {
                    if (size == 2) { SwapIfGreater(map, offset, 0, 1, comparer); return; }
                    if (size == 3)
                    {
                        SwapIfGreater(map, offset, 0, 1, comparer); SwapIfGreater(map, offset, 0, 2, comparer);
                        SwapIfGreater(map, offset, 1, 2, comparer); return;
                    }
                    InsertionSort(map, offset, size, comparer); return;
                }
                if (depth == 0) { HeapSort(map, offset, size, comparer); return; }
                depth--;
                int pivot = Partition(map, offset, size, comparer);
                IntroSort(map, offset + pivot + 1, size - pivot - 1, depth, comparer); size = pivot;
            }
        }
        private static int Partition(int[] map, int offset, int size, IMapComparison comparer)
        {
            int hi = size - 1, middle = hi >> 1;
            SwapIfGreater(map, offset, 0, middle, comparer); SwapIfGreater(map, offset, 0, hi, comparer);
            SwapIfGreater(map, offset, middle, hi, comparer);
            int pivot = map[offset + middle]; Swap(map, offset, middle, hi - 1);
            int left = 0, right = hi - 1;
            while (left < right)
            {
                // Enforce the original span boundaries, even for an inconsistent comparer.
                do { left++; if ((uint)left >= (uint)size) throw new IndexOutOfRangeException(); }
                while (comparer.Compare(map[offset + left], pivot) < 0);
                do { right--; if ((uint)right >= (uint)size) throw new IndexOutOfRangeException(); }
                while (comparer.Compare(pivot, map[offset + right]) < 0);
                if (left >= right) break;
                Swap(map, offset, left, right);
            }
            if (left != hi - 1) Swap(map, offset, left, hi - 1);
            return left;
        }
        private static void HeapSort(int[] map, int offset, int size, IMapComparison comparer)
        {
            for (int i = size >> 1; i >= 1; i--) DownHeap(map, offset, i, size, comparer);
            for (int i = size; i > 1; i--) { Swap(map, offset, 0, i - 1); DownHeap(map, offset, 1, i - 1, comparer); }
        }
        private static void DownHeap(int[] map, int offset, int i, int size, IMapComparison comparer)
        {
            int value = map[offset + i - 1];
            while (i <= size >> 1)
            {
                int child = 2 * i;
                if (child < size && comparer.Compare(map[offset + child - 1], map[offset + child]) < 0) child++;
                if (!(comparer.Compare(value, map[offset + child - 1]) < 0)) break;
                map[offset + i - 1] = map[offset + child - 1]; i = child;
            }
            map[offset + i - 1] = value;
        }
        private static void InsertionSort(int[] map, int offset, int size, IMapComparison comparer)
        {
            for (int i = 0; i < size - 1; i++)
            {
                int value = map[offset + i + 1], j = i;
                while (j >= 0 && comparer.Compare(value, map[offset + j]) < 0) { map[offset + j + 1] = map[offset + j]; j--; }
                map[offset + j + 1] = value;
            }
        }
    }
}

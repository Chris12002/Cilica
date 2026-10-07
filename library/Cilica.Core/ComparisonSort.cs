// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Pinned comparison-based introsort, adapted from spans to a captured T[] prefix.
using System;
namespace Cilica.Runtime
{
    internal static class ComparisonSort<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        private static extern string ComparisonName();
        internal static int CompareOrdinal(T first, T second) => String.CompareOrdinal((string)(object)first, (string)(object)second);
        internal static void Sort(T[] map, int count, Comparison<T> comparer)
        {
            try
            {
                if (count > 1)
                {
                    int bits = 0; for (uint n = (uint)count; n != 0; n >>= 1) bits++;
                    IntroSort(map, 0, count, 2 * bits, comparer);
                }
            }
            catch (IndexOutOfRangeException)
            {
                // The span boundary is the captured list size, not spare backing capacity.
                throw new ArgumentException("Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: '" + ComparisonName() + "'.");
            }
            catch (Exception inner) { throw new InvalidOperationException("Failed to compare two elements in the array.", inner); }
        }
        private static void Swap(T[] map, int offset, int first, int second)
        { T value = map[offset + first]; map[offset + first] = map[offset + second]; map[offset + second] = value; }
        private static void SwapIfGreater(T[] map, int offset, int first, int second, Comparison<T> comparer)
        { if (comparer(map[offset + first], map[offset + second]) > 0) Swap(map, offset, first, second); }
        private static void IntroSort(T[] map, int offset, int count, int depth, Comparison<T> comparer)
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
        private static int Partition(T[] map, int offset, int size, Comparison<T> comparer)
        {
            int hi = size - 1, middle = hi >> 1;
            SwapIfGreater(map, offset, 0, middle, comparer); SwapIfGreater(map, offset, 0, hi, comparer);
            SwapIfGreater(map, offset, middle, hi, comparer);
            T pivot = map[offset + middle]; Swap(map, offset, middle, hi - 1);
            int left = 0, right = hi - 1;
            while (left < right)
            {
                // Enforce the original span boundaries, even for an inconsistent comparer.
                do { left++; if ((uint)left >= (uint)size) throw new IndexOutOfRangeException(); }
                while (comparer(map[offset + left], pivot) < 0);
                do { right--; if ((uint)right >= (uint)size) throw new IndexOutOfRangeException(); }
                while (comparer(pivot, map[offset + right]) < 0);
                if (left >= right) break;
                Swap(map, offset, left, right);
            }
            if (left != hi - 1) Swap(map, offset, left, hi - 1);
            return left;
        }
        private static void HeapSort(T[] map, int offset, int size, Comparison<T> comparer)
        {
            for (int i = size >> 1; i >= 1; i--) DownHeap(map, offset, i, size, comparer);
            for (int i = size; i > 1; i--) { Swap(map, offset, 0, i - 1); DownHeap(map, offset, 1, i - 1, comparer); }
        }
        private static void DownHeap(T[] map, int offset, int i, int size, Comparison<T> comparer)
        {
            T value = map[offset + i - 1];
            while (i <= size >> 1)
            {
                int child = 2 * i;
                if (child < size && comparer(map[offset + child - 1], map[offset + child]) < 0) child++;
                if (!(comparer(value, map[offset + child - 1]) < 0)) break;
                map[offset + i - 1] = map[offset + child - 1]; i = child;
            }
            map[offset + i - 1] = value;
        }
        private static void InsertionSort(T[] map, int offset, int size, Comparison<T> comparer)
        {
            for (int i = 0; i < size - 1; i++)
            {
                T value = map[offset + i + 1]; int j = i;
                while (j >= 0 && comparer(value, map[offset + j]) < 0) { map[offset + j + 1] = map[offset + j]; j--; }
                map[offset + j + 1] = value;
            }
        }
    }
}

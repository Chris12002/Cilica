using System;
using System.Collections;
using System.Collections.Generic;

namespace Cilica.Fixtures.Execution
{
    public sealed class ContractPair<T> : IReadOnlyList<T>
    {
        private readonly T first;
        private readonly T second;
        public ContractPair(T first, T second) { this.first = first; this.second = second; }
        public int Count => 2;
        public T this[int index]
        {
            get
            {
                if (index == 0) return first;
                if (index == 1) return second;
                throw new ArgumentOutOfRangeException("index");
            }
        }
        public IEnumerator<T> GetEnumerator() => new ContractPairEnumerator<T>(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class ContractPairEnumerator<T> : IEnumerator<T>
    {
        private readonly ContractPair<T> pair;
        private int index;
        public ContractPairEnumerator(ContractPair<T> pair) { this.pair = pair; index = -1; }
        public T Current
        {
            get
            {
                if ((uint)index >= 2) throw new InvalidOperationException();
                return pair[index];
            }
        }
        object IEnumerator.Current => Current;
        public bool MoveNext() { if (index < 2) ++index; return index < 2; }
        public void Reset() { index = -1; }
        public void Dispose() { }
    }

    public static class CollectionContractProbe
    {
        public static int Wide(int input, int mode)
        {
            long first = ((long)input << 32) | (uint)mode;
            IReadOnlyList<long> pair = new ContractPair<long>(first, ~first);
            long value = pair[(int)((uint)mode % (uint)pair.Count)];
            return unchecked((int)(value ^ (value >> 32)));
        }
        public static int References(int input, int mode)
        {
            string first = string.Concat("first", input < 0 ? "negative" : "positive");
            IReadOnlyList<string> pair = new ContractPair<string>(first, string.Concat("second", "value"));
            string next = string.Concat(pair[0], pair[1]);
            return next.Length + ((object)pair[0] == first ? 17 : 31) + pair.Count + mode;
        }
        public static int Enumeration(int input, int mode)
        {
            IReadOnlyList<string> pair = new ContractPair<string>(string.Concat("A", "B"), mode < 0 ? "XYZ" : "K");
            IEnumerator<string> enumerator = pair.GetEnumerator();
            int result = input;
            while (enumerator.MoveNext()) result = unchecked(result * 31 + enumerator.Current.Length);
            enumerator.Reset();
            if (enumerator.MoveNext()) result += enumerator.Current.Length;
            enumerator.Dispose();
            return result;
        }
        public static int NonGenericEnumeration(int input, int mode)
        {
            IEnumerable pair = new ContractPair<string>("A", string.Concat("BC", "D"));
            IEnumerator enumerator = pair.GetEnumerator();
            int result = input;
            while (enumerator.MoveNext()) result = unchecked(result * 31 + ((string)enumerator.Current).Length);
            enumerator.Reset();
            if (enumerator.MoveNext()) result += ((string)enumerator.Current).Length;
            ((IDisposable)enumerator).Dispose();
            return result + mode;
        }
        public static int Faults(int input, int mode)
        {
            IReadOnlyList<string> pair = mode < 0 ? null : new ContractPair<string>("a", "b");
            try { return pair[input / mode].Length; }
            catch (DivideByZeroException) { return 11; }
            catch (NullReferenceException) { return 17; }
            catch (ArgumentOutOfRangeException error) { return 23 + error.ParamName.Length; }
            catch (OverflowException) { return 31; }
        }
        public static int EnumeratorFaults(int input, int mode)
        {
            IEnumerator<string> enumerator = new ContractPair<string>("first", "second").GetEnumerator();
            int result = input;
            try { result += enumerator.Current.Length; }
            catch (InvalidOperationException) { result += 11; }
            while (enumerator.MoveNext()) result += enumerator.Current.Length;
            try { result += enumerator.Current.Length; }
            catch (InvalidOperationException) { result += 17; }
            enumerator.Dispose();
            return result + mode;
        }
        public static int VariantCall(int input, int mode)
        {
            IReadOnlyList<object> pair = new ContractPair<string>("a", "b");
            return pair[0] == null ? input : mode;
        }
        public static int ArrayCall(int input, int mode)
        {
            IReadOnlyList<int> pair = new[] { input, mode };
            return pair[0];
        }
    }
}

using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    internal enum HiddenDispatchPhase { None, First }
    public static class HiddenInterfacePacketProbe
    {
        private enum PrivatePhase { None, First }
        public static int InternalCount(int a, int b)
        {
            ICollection<ValueTuple<HiddenDispatchPhase, int>> values = new[] { new ValueTuple<HiddenDispatchPhase, int>((HiddenDispatchPhase)a, b) };
            return unchecked(values.Count + a + b);
        }
        public static int InternalCopy(int a, int b)
        {
            ICollection<ValueTuple<HiddenDispatchPhase, int>> values = new[] { new ValueTuple<HiddenDispatchPhase, int>((HiddenDispatchPhase)a, b) };
            var copied = new ValueTuple<HiddenDispatchPhase, int>[2]; values.CopyTo(copied, 1);
            return unchecked((int)copied[1].Item1 + copied[1].Item2 + copied[0].Item2);
        }
        public static int PrivateRead(int a, int b)
        {
            IList<ValueTuple<PrivatePhase, int>> values = new[] { new ValueTuple<PrivatePhase, int>((PrivatePhase)a, b) };
            var item = values[0]; return unchecked((int)item.Item1 + item.Item2);
        }
        public static int CopyFault(int a, int b)
        {
            ICollection<ValueTuple<HiddenDispatchPhase, int>> values = new[] { new ValueTuple<HiddenDispatchPhase, int>((HiddenDispatchPhase)a, b) };
            try { values.CopyTo(new ValueTuple<HiddenDispatchPhase, int>[1], a); return b; }
            catch (ArgumentOutOfRangeException) { return 13; }
            catch (ArgumentException) { return 17; }
        }
    }
}

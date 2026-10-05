using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ValueVectorBox { public int Number; public ValueVectorBox(int number) { Number = number; } }
    public struct ValueVectorCell { public int Number; public long Wide; public ValueVectorBox Reference; }
    public struct ValueVectorBucket<T> { public T Value; public int Marker; }
    public struct ValueVectorEnvelope { public ValueVectorCell[] Values; }
    public sealed class ValueVectorHolder { public ValueVectorBucket<ValueVectorCell>[] Values; }
    public struct ValueVectorEmpty { }
    public static class ValueVectorProbe
    {
        public static int NarrowAllocationOnly(int number, int mode) => new bool[1].Length;
        public static int WideAllocationOnly(int number, int mode) => new long[1].Length;
        public static void Bump(ref ValueVectorCell cell, int amount)
        {
            cell.Number = unchecked(cell.Number + amount);
            cell.Wide = unchecked(cell.Wide + amount);
            cell.Reference = new ValueVectorBox(amount);
        }
        public static int Copy(int number, int amount)
        {
            var cells = new ValueVectorCell[2];
            ValueVectorCell cell = default; cell.Number = number; cell.Wide = ((long)number << 32) + amount;
            cell.Reference = new ValueVectorBox(number); cells[0] = cell;
            var copy = cells[0]; Bump(ref cells[0], amount); cells[1] = copy;
            if (cells[0].Reference == cells[1].Reference) return -700;
            if (cell.Reference != cells[1].Reference) return -701;
            return unchecked(cells[0].Number + cells[1].Number + cells[0].Reference.Number + cells[1].Reference.Number
                + (int)(cells[0].Wide >> 32) + (int)cells[1].Wide);
        }
        public static int Generic(int number, int amount)
        {
            var holder = new ValueVectorHolder(); holder.Values = new ValueVectorBucket<ValueVectorCell>[2];
            ValueVectorEnvelope envelope = default; envelope.Values = new ValueVectorCell[1];
            ValueVectorCell cell = default; cell.Number = number; cell.Reference = new ValueVectorBox(amount);
            ValueVectorBucket<ValueVectorCell> bucket = default; bucket.Value = cell; bucket.Marker = amount;
            holder.Values[1] = bucket; envelope.Values[0] = holder.Values[1].Value;
            Bump(ref envelope.Values[0], 1);
            return unchecked(holder.Values[1].Value.Number + envelope.Values[0].Number + holder.Values[1].Marker + holder.Values[1].Value.Reference.Number);
        }
        public static int Faults(int number, int mode)
        {
            try {
                ValueVectorCell[] cells = null;
                if (mode >= 0) cells = new ValueVectorCell[number < 0 ? -1 : 2];
                ValueVectorCell cell = default; cell.Number = number;
                cells[mode] = cell;
                Bump(ref cells[mode], 1);
                return cells[mode].Number;
            }
            catch (NullReferenceException) { return -4; }
            catch (IndexOutOfRangeException) { return -10; }
            catch (OverflowException) { return -2; }
        }
        public static void Clear(ref ValueVectorEmpty value) { value = default; }
        public static int Consume(ValueVectorEmpty value) { return 17; }
        public static int Empty(int number, int mode)
        {
            try {
                var cells = new ValueVectorEmpty[2]; cells[mode] = default;
                int result = Consume(cells[mode]); Clear(ref cells[mode]); return result + cells.Length;
            }
            catch (IndexOutOfRangeException) { return -10; }
        }
    }
}

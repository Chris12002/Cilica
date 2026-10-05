using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ValueArrayInterfaceProbe
    {
        public static int Copy(int number, int amount)
        {
            var array = new ValueVectorBucket<ValueVectorCell>[2];
            IList<ValueVectorBucket<ValueVectorCell>> write = array;
            IReadOnlyList<ValueVectorBucket<ValueVectorCell>> read = array;
            ValueVectorCell cell = default; cell.Number = number; cell.Wide = ((long)number << 32) + amount;
            cell.Reference = new ValueVectorBox(amount);
            ValueVectorBucket<ValueVectorCell> bucket = default; bucket.Value = cell; bucket.Marker = amount;
            write[1] = bucket;
            bucket.Value.Number = amount;
            var copy = read[1];
            copy.Value.Reference = new ValueVectorBox(number);
            if (read[1].Value.Reference == copy.Value.Reference) return -700;
            object identity = array;
            if (identity is IReadOnlyList<ValueVectorCell>) return -701;
            if (identity is IReadOnlyList<object>) return -702;
            return unchecked(read[1].Value.Number + read[1].Value.Reference.Number + (int)(copy.Value.Wide >> 32) + write.Count + read.Count);
        }
        public static int Faults(int number, int mode)
        {
            try {
                IList<ValueVectorCell> list = null;
                if (mode >= 0) list = new ValueVectorCell[2];
                ValueVectorCell value = default; value.Number = number;
                if (number < 0) list.Add(value);
                list[mode] = value; return list[mode].Number;
            }
            catch (NullReferenceException) { return -4; }
            catch (ArgumentOutOfRangeException) { return -11; }
            catch (NotSupportedException) { return -12; }
        }
        public static int Empty(int number, int mode)
        {
            try {
                IList<ValueVectorEmpty> list = new ValueVectorEmpty[2];
                list[mode] = default; var value = list[mode];
                if (number < 0) list.Add(value);
                return ValueVectorProbe.Consume(value) + list.Count;
            }
            catch (ArgumentOutOfRangeException) { return -11; }
            catch (NotSupportedException) { return -12; }
        }
        public static int Lists(int number, int amount)
        {
            var list = new List<ValueVectorCell>();
            ValueVectorCell value = default; value.Number = number; value.Reference = new ValueVectorBox(amount);
            list.Add(value); list.Add(value); list.Add(value); list.Add(value); list.Add(value);
            var read = list.AsReadOnly(); value.Number = amount; list[0] = value;
            return unchecked(read[0].Number + read[4].Number + read[4].Reference.Number + read.Count);
        }
    }
}

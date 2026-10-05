using System;
using System.Collections.Generic;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public static class MilestoneSixCompositionProbe
    {
        public static int CollectionsAndUnwind(int number, int mode)
        {
            var list = new List<ValueVectorCell>();
            var view = list.AsReadOnly();
            var index = new Dictionary<int, ValueVectorCell>();
            var text = new StringBuilder(); int result = 0;
            for (int i = 0; i < 7; ++i) {
                ValueVectorCell value = default; value.Number = unchecked(number + i);
                value.Wide = ((long)number << 32) + i; value.Reference = new ValueVectorBox(mode);
                list.Add(value); index.TryAdd(i, value);
                try {
                    if (i == (mode & 3)) throw new InvalidOperationException("injected");
                    if (i == 6) throw new ArgumentException("last");
                    value.Number = unchecked(value.Number + 13); list[i] = value;
                    text.Append("ok");
                }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException) {
                    var allocation = new ValueVectorCell[2]; allocation[1] = value;
                    text.Append(error.Message);
                    result = unchecked(result + allocation[1].Reference.Number);
                }
                finally {
                    ValueVectorCell original;
                    if (!index.TryGetValue(i, out original)) ThrowMissing();
                    result = unchecked(result + original.Number + view[i].Number + original.Reference.Number + (int)(original.Wide >> 32));
                }
            }
            var formatted = new FormattingValue(); formatted.Text = text.ToString();
            string snapshot = string.Format("<{0}>", formatted);
            for (int i = 0; i < snapshot.Length; ++i) result = unchecked(result * 31 + snapshot[i]);
            return unchecked(result + view.Count + index.Count);
        }
        private static void ThrowMissing() { throw new InvalidOperationException("missing"); }
    }
}

using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ConstructorJoined
    {
        public readonly ValueVectorBox Value;
        public readonly string Text;
        public ConstructorJoined(ValueVectorBox value, string text)
        {
            Value = value ?? throw new ArgumentNullException("value");
            Text = text ?? throw new ArgumentNullException("text");
        }
    }
    public static class ConstructorJoinProbe
    {
        public static int Flow(int number, int mode)
        {
            try {
                ValueVectorBox value = null;
                if (mode >= 0) value = new ValueVectorBox(number);
                string text = null;
                if (mode != 0) text = "joined";
                var joined = new ConstructorJoined(value, text);
                var allocation = new ValueVectorBox(mode);
                return unchecked(joined.Value.Number + joined.Text.Length + allocation.Number);
            }
            catch (ArgumentNullException error) { return error.ParamName.Length; }
        }
    }
}

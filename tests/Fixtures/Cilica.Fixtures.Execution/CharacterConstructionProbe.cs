using System;
namespace Cilica.Fixtures.Execution
{
public static class CharacterConstructionProbe
{
    public static int Single(int value, int unused) => char.ToString((char)value)[0];
    public static int Identity(int value, int other)
    {
        char unit = (char)value;
        string first = char.ToString(unit), second = unit.ToString();
        return (object)first != (object)second && first.Length == 1 && second.Length == 1
            && first[0] == unit && second[0] == unit ? unit : -1;
    }
    public static int Caught(int value, int fallback)
    {
        try { return char.ToString((char)value).Length; }
        catch (OutOfMemoryException) { return fallback; }
    }
}

}

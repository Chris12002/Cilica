using System;
using System.Collections.Generic;

namespace Cilica.Fixtures.Execution
{

public interface ITypeMarker { }
public class TypeAlpha : ITypeMarker { }
public sealed class TypeBeta : TypeAlpha { }
public sealed class TypeBox<T> { }
public struct TypeUncomparedValue { public int Number; }
public abstract class TypeAbstractBase { public TypeAbstractBase() { } public virtual int Read() => 11; }
public sealed class TypeConcrete : TypeAbstractBase { public override int Read() => 29; }

public static class RuntimeTypeProbe
{
    public static int Identity(int choice, int ignored)
    {
        object value = choice < 0 ? new TypeBeta() : new TypeAlpha();
        var expected = choice < 0 ? typeof(TypeBeta) : typeof(TypeAlpha);
        var actual = value.GetType();
        int result = actual == expected ? 1 : 0;
        if ((object)actual == (object)expected) result += 2;
        if (actual.Equals((object)expected)) result += 4;
        if (actual.Equals(expected)) result += 8;
        if (actual.GetHashCode() == expected.GetHashCode()) result += 16;
        if (actual != typeof(object) && actual != typeof(ITypeMarker)) result += 32;
        if (typeof(TypeBox<int>) != typeof(TypeBox<string>)) result += 64;
        if (new TypeBox<int>().GetType() == typeof(TypeBox<int>)) result += 128;
        if (actual.GetType() == expected.GetType() && actual.GetType() != typeof(Type)) result += 256;
        TypeAbstractBase inherited = new TypeConcrete();
        if (inherited.Read() == 29 && inherited.GetType() == typeof(TypeConcrete) && typeof(TypeAbstractBase) != inherited.GetType()) result += 512;
        return result;
    }

    public static int HandlesAndArrays(int choice, int ignored)
    {
        var handle = (choice < 0 ? typeof(TypeAlpha) : typeof(TypeBeta)).TypeHandle;
        var copy = handle;
        int result = Type.GetTypeFromHandle(copy) == (choice < 0 ? typeof(TypeAlpha) : typeof(TypeBeta)) ? 1 : 0;
        if (Type.GetTypeFromHandle(default) == null) result += 2;
        object array = new TypeBeta[2];
        if (array.GetType() == typeof(TypeBeta[]) && array.GetType() != typeof(TypeAlpha[])) result += 4;
        if (new int[1].GetType() == typeof(int[])) result += 8;
        if ("abc".GetType() == typeof(string)) result += 16;
        object missing = null;
        try { missing!.GetType(); }
        catch (NullReferenceException) { result += 32; }
        return result;
    }

    public static int Dictionary(int choice, int value)
    {
        var table = new Dictionary<Type, int>();
        table.TryAdd(typeof(TypeAlpha), choice);
        table.TryAdd(typeof(TypeBeta), value);
        table[typeof(TypeBox<int>)] = 27;
        table[typeof(TypeBox<string>)] = 41;
        var key = new TypeAlpha().GetType();
        table.TryGetValue(key, out var original);
        table[key] = unchecked(original + 3);
        table.TryGetValue(typeof(TypeAlpha), out var alpha);
        table.TryGetValue(new TypeBeta().GetType(), out var beta);
        table.TryGetValue(typeof(TypeBox<int>), out var firstBox);
        table.TryGetValue(typeof(TypeBox<string>), out var secondBox);
        int result = unchecked(alpha + beta + firstBox + secondBox);
        if (!table.ContainsKey(typeof(object))) result = unchecked(result + 5);
        try { table.TryAdd(null!, 1); }
        catch (ArgumentNullException) { result = unchecked(result + 7); }
        return result;
    }

    private static Type ClosedType<T>() => typeof(T);

    public static int LiteralOnly(int choice, int ignored)
    {
        int result = ClosedType<string>() == typeof(string) ? 1 : 0;
        if (ClosedType<object>() == typeof(object)) result += 2;
        if (ClosedType<TypeBox<int>>() == typeof(TypeBox<int>)) result += 4;
        if (new TypeUncomparedValue { Number = choice }.GetType() == new TypeUncomparedValue().GetType()) result += 8;
        return result;
    }

    public static int UnsupportedStringHash(int a, int b) => "text".GetHashCode();
    public static int UnsupportedArrayHash(int a, int b) => new int[1].GetHashCode();
    public static int UnsupportedObjectAliasEquality(int a, int b)
    {
        object left = a < 0 ? "text" : new TypeAlpha();
        object right = b < 0 ? "text" : new TypeAlpha();
        return left.Equals(right) ? 1 : 0;
    }

    public static int ScalarTokens(int a, int b) => typeof(int) == typeof(long) ? a : b;
    public static int UnsupportedReflection(int a, int b) => typeof(TypeAlpha).GetMethods().Length;
    public static int UnsupportedDefaultValueEquality(int a, int b)
    {
        object left = new TypeUncomparedValue { Number = a };
        object right = new TypeUncomparedValue { Number = b };
        return left.Equals(right) ? 1 : 0;
    }
}
}

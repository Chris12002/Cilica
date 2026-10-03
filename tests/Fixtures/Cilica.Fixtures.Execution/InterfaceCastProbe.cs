#nullable enable
namespace Cilica.Fixtures.Execution
{
    public interface ICastTargetOnly { }
    public sealed class InterfaceCastUnrelated { }
    public static class InterfaceCastProbe
    {
        public static object? Create(int kind)
        {
            if (kind < 0) return null;
            if (kind == 0) return new ExactInterfaceDiamond();
            if (kind == 1) return new ExactInterfaceExplicitInherited();
            if (kind == 2) return new ExactInterfaceOverride();
            return new InterfaceCastUnrelated();
        }
        public static IExactRead? Checked(object? value) { return (IExactRead?)value; }
        public static IExactDiamond? Tested(object? value) { return value as IExactDiamond; }
        public static int Read(IExactRead? reader, int value) { return reader!.Read(value); }
        public static int Order(int kind, int divisor) { return Read(Checked(Create(kind)), 12 / divisor); }
        public static int ReverseOrder(int kind, int divisor) { return Take(12 / divisor, Checked(Create(kind))); }
        public static int Take(int value, IExactRead? reader) { return reader!.Read(value); }

        public static int Tests(int kind, int value)
        {
            object? original = Create(kind);
            IExactRead? reader = original as IExactRead;
            IExactDiamond? diamond = Tested(original);
            int result = 0;
            if (reader != null) result += 1;
            if (diamond != null) result += 2;
            if (original is IExactSecond) result += 4;
            if (original is ICastTargetOnly) result += 8;
            if (original is IExactLeft) result += 16;
            IExactRead? held = null;
            if (reader != null) held = (IExactRead)original!;
            // The cast result and original object must both survive later allocations and loop edges.
            for (int i = 0; i < 3; i++)
            {
                var later = new InterfaceCastUnrelated();
                if (reader != null)
                {
                    IExactRead again = held!;
                    object widened = again;
                    ExactInterfaceBase? parent = widened as ExactInterfaceBase;
                    if (again == original && again == reader && later != original) result = unchecked(result + again.Read(value));
                    if (parent != null) result += 32;
                }
            }
            return result;
        }
    }
}

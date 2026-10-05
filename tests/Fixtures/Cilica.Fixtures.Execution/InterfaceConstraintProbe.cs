using System;

namespace Cilica.Fixtures.Execution
{
    public interface IConstraintMark<T> { }
    public interface IConstraintChild<T> : IConstraintMark<T> { }
    public class ConstraintParent<T> : IConstraintChild<T> { }
    public class ConstraintChild<T> : ConstraintParent<T> { }
    public class ConstraintContainer<T> where T : IConstraintMark<int> { }
    public class SelfConstraintContainer<T> where T : IEquatable<T> { }
    public class UnmanagedConstraintContainer<T> where T : unmanaged { }
    public class ConstraintHost<T>
    {
        public static int Accept<U>() where U : IConstraintMark<T> => 17;
    }
    public static class InterfaceConstraintProbe
    {
        private static bool Equal<T>(ref T left, T right) where T : struct, IEquatable<T> => left.Equals(right);
        public static int Equality(int input, int mode)
        {
            var left = new RuntimeEquatableValue { Number = input };
            var right = new RuntimeEquatableValue { Number = mode };
            return Equal(ref left, right) ? 29 : 31;
        }
    }
}

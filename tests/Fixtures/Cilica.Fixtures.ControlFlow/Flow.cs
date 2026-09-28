// Input data for method body tests. Each method exercises a control-flow or
// exception-handling shape; the code is never run.
using System;
using System.Collections.Generic;

namespace Cilica.Fixtures.ControlFlow
{
    public static class Flow
    {
        private static int _filterCalls;

        public static int Loop(int count)
        {
            int total = 0;
            for (int index = 0; index < count; index++)
            {
                total += index;
            }
            return total;
        }

        public static string Switch(int value)
        {
            switch (value)
            {
                case 0: return "zero";
                case 1: return "one";
                case 2: return "two";
                case 3: return "three";
                default: return "many";
            }
        }

        public static int TryCatchFinally(int[] values)
        {
            int result = 0;
            try
            {
                result = values[0];
            }
            catch (IndexOutOfRangeException)
            {
                result = -1;
            }
            finally
            {
                result++;
            }
            return result;
        }

        // Stage A in ADR 0004: only type tests on the exception object.
        public static bool TypeTestFilter(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                return false;
            }
        }

        // Calls a method, so its evaluation order is observable.
        public static bool CallingFilter(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception exception) when (Record(exception))
            {
                return false;
            }
        }

        // Reads shared state, which an inner finally block could change.
        public static bool StateFilter(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception) when (_filterCalls > 0)
            {
                return false;
            }
        }

        public static void Rethrow(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static int Using(IDisposable resource)
        {
            using (resource)
            {
                return 1;
            }
        }

        public static int Foreach(List<int> values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }
            return total;
        }

        public static void Locked(object gate, ref int counter)
        {
            lock (gate)
            {
                counter++;
            }
        }

        public static long Checked(int left, int right) => checked((long)left * right);

        public static Type TypeToken() => typeof(List<string>);

        public static T Largest<T>(T[] items)
            where T : IComparable<T>
        {
            T best = items[0];
            foreach (T item in items)
            {
                if (item.CompareTo(best) > 0)
                {
                    best = item;
                }
            }
            return best;
        }

        public static unsafe int StackAllocation()
        {
            int* values = stackalloc int[4];
            values[0] = 1;
            return values[0];
        }

        public static unsafe int CallPointer(delegate*<int, int> function) => function(3);

        private static bool Record(Exception exception)
        {
            _filterCalls++;
            return exception is InvalidOperationException;
        }
    }
}

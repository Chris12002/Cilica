using System;
using System.Globalization;
namespace Cilica.Fixtures.Execution
{
    public interface IReadOnlyValue { int Read(int value); }
    public readonly struct ReadOnlyValue : IReadOnlyValue
    {
        public readonly long Number;
        public readonly HeapPayload Payload;
        public ReadOnlyValue(int value) { Number = (long)value * 2147483647L; Payload = new HeapPayload(value); }
        public int Read(int value)
        {
            var pressure = new HeapPayload(value); Payload.Number = unchecked(Payload.Number + pressure.Number);
            return unchecked((int)Number + (int)(Number >> 32) + Payload.Number);
        }
    }
    public struct MixedReadOnlyValue
    {
        public int Number;
        public MixedReadOnlyValue(int value) { Number = value; }
        public readonly int Read(int value) => unchecked(Number + value);
        public int Mutate(int value) { Number = unchecked(Number + value); return Number; }
    }
    public readonly struct ReadOnlyContainer<T>
    {
        public readonly T Value;
        public ReadOnlyContainer(T value) { Value = value; }
        public T Read() => Value;
    }
    public sealed class ReadOnlyReceiverHolder
    {
        public readonly ReadOnlyValue Value;
        public readonly MixedReadOnlyValue Mixed;
        public readonly int Number;
        public readonly Guid Id;
        public readonly int? Optional;
        public readonly ReadOnlyContainer<HeapPayload> Container;
        public ReadOnlyReceiverHolder(int value)
        {
            Value = new ReadOnlyValue(value); Mixed = new MixedReadOnlyValue(value); Number = value;
            Id = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            Optional = value < 0 ? default(int?) : value;
            Container = new ReadOnlyContainer<HeapPayload>(new HeapPayload(value));
        }
    }
    public static class ReadOnlyReceiverProbe
    {
        public static int StructReceiver(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return unchecked(holder.Value.Read(b) + holder.Value.Payload.Number); }
        public static int MemberReceiver(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return holder.Mixed.Read(b); }
        public static int DefensiveCopy(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return unchecked(holder.Mixed.Mutate(b) + holder.Mixed.Number); }
        public static int BoxedReceiver(int a, int b) { IReadOnlyValue value = new ReadOnlyValue(a); return unchecked(value.Read(b) + value.Read(a)); }
        public static int Primitive(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return unchecked(holder.Number.CompareTo(b) + holder.Number.ToString(CultureInfo.InvariantCulture).Length); }
        public static int Nullable(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return unchecked(holder.Optional.GetValueOrDefault(b) + (holder.Optional.HasValue ? holder.Optional.Value : b)); }
        public static int GuidFields(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); return unchecked(holder.Id.GetHashCode() + holder.Id.ToString().Length + (holder.Id.Equals(holder.Id) ? b : 17)); }
        public static int ClosedGeneric(int a, int b) { var holder = new ReadOnlyReceiverHolder(a); var pressure = new HeapPayload(b); return unchecked(holder.Container.Read().Number + pressure.Number); }
        public static int NullReceiver(int a, int b)
        { ReadOnlyReceiverHolder holder = a < 0 ? null : new ReadOnlyReceiverHolder(a); try { return holder.Value.Read(a / b); } catch (NullReferenceException) { return 11; } catch (DivideByZeroException) { return 13; } catch (OverflowException) { return 17; } }
    }
}

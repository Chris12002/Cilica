using System;

namespace Cilica.Fixtures.Execution
{
    public struct EmbeddingPoint { public int X; public long Y; }
    public struct EmbeddingPacket { public EmbeddingPoint Point; public int Tag; }
    public struct EmbeddingReferencePacket { public EmbeddingRecordOther Link; }
    public sealed class EmbeddingRecordOther { public int Value; }
    public sealed class EmbeddingRecordState { public static int Calls; }
    public static class EmbeddingRecordProbe
    {
        public static EmbeddingPacket Make(int value) => new EmbeddingPacket { Point = new EmbeddingPoint { X = value, Y = long.MaxValue }, Tag = 7 };
        public static EmbeddingPacket Transform(EmbeddingPacket value, int delta)
        {
            EmbeddingRecordState.Calls++;
            value.Point.X = unchecked(value.Point.X + delta);
            value.Point.Y = unchecked(value.Point.Y + delta);
            if (delta < 0) throw new InvalidOperationException("record failure");
            value.Tag = unchecked(value.Tag + new EmbeddingRecordOther { Value = delta }.Value);
            return value;
        }
        public static long Score(EmbeddingPacket value) => unchecked(value.Point.Y + value.Point.X + value.Tag);
        public static EmbeddingPacket Mixed(EmbeddingRecordOther other, EmbeddingPacket value, long extra)
        {
            value = Transform(value, other.Value);
            value.Point.Y = unchecked(value.Point.Y + extra);
            return value;
        }
        public static EmbeddingRecordOther Other(int value) => new EmbeddingRecordOther { Value = value };
        public static int Calls() => EmbeddingRecordState.Calls;
        public static void Change(ref EmbeddingPacket value) => value.Tag++;
        public static EmbeddingReferencePacket Reference(EmbeddingReferencePacket value) => value;
        public static int Root(int mode, int input)
        {
            var value = Make(input);
            if (mode == 0) return unchecked((int)Score(Transform(value, input)));
            if (mode == 1) return unchecked((int)Score(Mixed(Other(input), value, input)));
            if (mode == 2) return Calls();
            if (mode == 3) { Change(ref value); return value.Tag; }
            return Reference(new EmbeddingReferencePacket { Link = Other(input) }).Link.Value;
        }
    }
}

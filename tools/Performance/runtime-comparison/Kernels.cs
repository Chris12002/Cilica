// Independently authored future comparison inputs. Not compiled or executed in
// this investigation; not enrolled in a build, benchmark or CI project.
namespace Cilica.Performance.Proposals
{
    public sealed class Cell
    {
        public int Value;
        public Cell Next;
        public Cell(int value, Cell next) { Value = value; Next = next; }
    }

    public sealed class State
    {
        public Cell Head;
        public Cell Oldest;
        public Cell Scratch;
    }

    public struct Payload
    {
        public Cell Reference;
        public int Tag;
    }

    public static class Kernels
    {
        // Setup belongs outside the steady compute interval. Keep the returned
        // State rooted for every subsequent call. Proposed sizes: 8,64,512,2048.
        public static State Setup(int size, int seed)
        {
            if (size < 8 || size > 2048)
                throw new System.ArgumentOutOfRangeException("size");
            var state = new State();
            for (int i = 0; i < size; ++i)
            {
                var cell = new Cell(unchecked(seed + i), state.Head);
                state.Head = cell;
                if (i == 0) state.Oldest = cell;
            }
            return state;
        }

        // K1: no managed allocation. Oldest is near the allocation inventory tail.
        public static int ReadOldest(State state, int rounds)
        {
            var cell = state.Oldest;
            int sum = 0;
            for (int i = 0; i < rounds; ++i) sum = unchecked(sum + cell.Value);
            return sum;
        }

        // K2: one new Cell per source-level iteration, published through State.
        // Verify actual allocation counts/generated code; do not assume an
        // optimizer must retain every allocation merely from this source shape.
        public static int Churn(State state, int rounds)
        {
            int sum = 0;
            for (int i = 0; i < rounds; ++i)
            {
                state.Scratch = new Cell(i, state.Oldest);
                sum = unchecked(sum + state.Scratch.Value);
            }
            return sum;
        }

        // K3a/K3b: roots used after an allocating call. Source locals are not a
        // promise of eight physical roots: record each backend's actual lowering.
        public static int HoldEightAliases(State state, int rounds)
        {
            var a = state.Oldest; var b = a; var c = a; var d = a;
            var e = a; var f = a; var g = a; var h = a;
            int sum = Churn(state, rounds);
            return unchecked(sum + a.Value + b.Value + c.Value + d.Value
                                 + e.Value + f.Value + g.Value + h.Value);
        }

        public static int HoldEightDistinct(State state, int rounds)
        {
            var a = state.Head; var b = a.Next; var c = b.Next; var d = c.Next;
            var e = d.Next; var f = e.Next; var g = f.Next; var h = g.Next;
            int sum = Churn(state, rounds);
            return unchecked(sum + a.Value + b.Value + c.Value + d.Value
                                 + e.Value + f.Value + g.Value + h.Value);
        }

        // K4: only existing references are copied. The harness supplies a
        // non-empty preallocated vector; initialization is outside timing.
        public static int Rewrite(State state, Cell[] slots, int rounds)
        {
            int sum = 0;
            for (int i = 0; i < rounds; ++i)
            {
                int index = i % slots.Length;
                slots[index] = (i & 1) == 0 ? state.Head : state.Oldest;
                sum = unchecked(sum + slots[index].Value);
            }
            return sum;
        }

        // K5: a reference leaf inside an inline value must survive allocations.
        public static int InlineRoot(State state, int rounds)
        {
            Payload payload;
            payload.Reference = state.Oldest;
            payload.Tag = 17;
            int sum = Churn(state, rounds);
            return unchecked(sum + payload.Reference.Value + payload.Tag);
        }
    }
}

using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public interface IRegistryHandler<TState, TEvent>
    {
        int Id { get; }
        TEvent Execute(TState state, int command);
    }
    public sealed class RegistryPayload
    {
        public int Value;
        public RegistryPayload(int value) { Value = value; }
    }
    public sealed class RegistryState { public int Total, Version; }
    public sealed class RegistryEvent
    {
        public int Kind, Before, After, Sequence;
        public RegistryPayload Payload;
        public RegistryState State;
    }
    public sealed class AddRegistryHandler : IRegistryHandler<RegistryState, RegistryEvent>
    {
        private readonly RegistryPayload bias;
        public AddRegistryHandler(int value) { bias = new RegistryPayload(value); }
        public int Id => 1;
        public RegistryEvent Execute(RegistryState state, int command)
        {
            int before = state.Total;
            state.Total = unchecked(before + command + bias.Value);
            ++state.Version;
            var result = new RegistryEvent { Kind = Id, Before = before, After = state.Total,
                Sequence = state.Version, State = state, Payload = new RegistryPayload(command) };
            var pressure = new RegistryPayload(unchecked(command + 7));
            result.Payload.Value = unchecked(result.Payload.Value + pressure.Value);
            return result;
        }
    }
    public sealed class ScaleRegistryHandler : IRegistryHandler<RegistryState, RegistryEvent>
    {
        private readonly RegistryPayload bias;
        public ScaleRegistryHandler(int value) { bias = new RegistryPayload(value); }
        int IRegistryHandler<RegistryState, RegistryEvent>.Id => 2;
        RegistryEvent IRegistryHandler<RegistryState, RegistryEvent>.Execute(RegistryState state, int command)
        {
            int before = state.Total;
            state.Total = unchecked(before * ((command & 3) + 1) + bias.Value);
            ++state.Version;
            return new RegistryEvent { Kind = 2, Before = before, After = state.Total,
                Sequence = state.Version, State = state, Payload = new RegistryPayload(command) };
        }
    }
    public sealed class RegistryKernel<TState, TEvent>
    {
        public readonly Dictionary<int, IRegistryHandler<TState, TEvent>> Handlers = new Dictionary<int, IRegistryHandler<TState, TEvent>>();
        public int LastPressure;
        public void Register(IRegistryHandler<TState, TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            Handlers.Add(handler.Id, handler);
        }
        public TEvent Step(int id, TState state, int command)
        {
            if (!Handlers.TryGetValue(id, out var handler)) throw new KeyNotFoundException("handler");
            TEvent result = handler.Execute(state, command);
            var pressure = new RegistryPayload(unchecked(command + 11));
            LastPressure = pressure.Value;
            return result;
        }
    }
    public static class HandlerRegistryProbe
    {
        private static int Hash(string text)
        { int h = 17; for (int i = 0; i < text.Length; ++i) h = unchecked(h * 31 + text[i]); return h; }
        private static int Observe(RegistryEvent item, RegistryState state)
        {
            int h = item.Kind;
            h = unchecked(h * 31 + item.Before); h = unchecked(h * 31 + item.After);
            h = unchecked(h * 31 + item.Sequence); h = unchecked(h * 31 + item.Payload.Value);
            return unchecked(h * 31 + (item.State == state ? 1 : 0));
        }
        public static int Transcript(int a, int b)
        {
            var kernel = new RegistryKernel<RegistryState, RegistryEvent>();
            kernel.Register(new AddRegistryHandler(a)); kernel.Register(new ScaleRegistryHandler(b));
            var state = new RegistryState { Total = a };
            int result = 17;
            for (int i = 0; i < 8; ++i)
            {
                RegistryEvent item = kernel.Step((i & 1) + 1, state, unchecked(b + i));
                var pressure = new RegistryPayload(unchecked(a + i));
                result = unchecked(result * 31 + Observe(item, state) + pressure.Value + kernel.LastPressure);
            }
            return unchecked(result * 31 + state.Total + state.Version);
        }
        public static int Mutation(int a, int b)
        {
            var kernel = new RegistryKernel<RegistryState, RegistryEvent>();
            kernel.Register(new AddRegistryHandler(a)); kernel.Register(new ScaleRegistryHandler(b));
            var state = new RegistryState { Total = a };
            RegistryEvent first = kernel.Step(1, state, b);
            kernel.Handlers.Remove(1); kernel.Handlers[2] = new ScaleRegistryHandler(a);
            RegistryEvent second = kernel.Step(2, state, b);
            kernel.Handlers.Clear(); kernel.Register(new AddRegistryHandler(b));
            RegistryEvent third = kernel.Step(1, state, a);
            return unchecked(Observe(first, state) * 31 + Observe(second, state) * 17 + Observe(third, state) + kernel.Handlers.Count);
        }
        public static int Faults(int a, int b)
        {
            var kernel = new RegistryKernel<RegistryState, RegistryEvent>();
            kernel.Register(new AddRegistryHandler(a)); int result = 0;
            try { kernel.Register(null); } catch (ArgumentNullException e) { result = unchecked(result + Hash(e.Message) + Hash(e.ParamName)); }
            try { kernel.Register(new AddRegistryHandler(b)); } catch (ArgumentException e) { result = unchecked(result * 31 + Hash(e.Message)); }
            try { kernel.Step(7, new RegistryState(), b); } catch (KeyNotFoundException e) { result = unchecked(result * 31 + Hash(e.Message)); }
            kernel.Handlers[2] = null;
            try { kernel.Step(2, new RegistryState(), b); } catch (NullReferenceException) { result = unchecked(result * 31 + 19); }
            try { kernel.Step(1, null, b); } catch (NullReferenceException) { result = unchecked(result * 31 + 23); }
            return unchecked(result + kernel.Handlers.Count);
        }
        public static int Returned(int a, int b)
        {
            var kernel = new RegistryKernel<RegistryState, RegistryEvent>(); kernel.Register(new AddRegistryHandler(a));
            var state = new RegistryState { Total = b }; RegistryEvent item = kernel.Step(1, state, a);
            kernel.Handlers.Clear(); state = null;
            for (int i = 0; i < 6; ++i) kernel.LastPressure = new RegistryPayload(unchecked(b + i)).Value;
            return unchecked(Observe(item, item.State) + item.State.Total + kernel.LastPressure);
        }
        public static RegistryKernel<RegistryState, RegistryEvent> CreateTracked()
        {
            var kernel = new RegistryKernel<RegistryState, RegistryEvent>();
            kernel.Register(new AddRegistryHandler(3)); kernel.Register(new ScaleRegistryHandler(7)); return kernel;
        }
        public static RegistryEvent EmitTracked(RegistryKernel<RegistryState, RegistryEvent> kernel) => kernel.Step(1, new RegistryState(), 5);
        public static void RemoveTracked(RegistryKernel<RegistryState, RegistryEvent> kernel) => kernel.Handlers.Remove(1);
        public static void ReplaceTracked(RegistryKernel<RegistryState, RegistryEvent> kernel) => kernel.Handlers[2] = new ScaleRegistryHandler(9);
        public static void ClearTracked(RegistryKernel<RegistryState, RegistryEvent> kernel) => kernel.Handlers.Clear();
        public static int Tracked(int a, int b)
        {
            var kernel = CreateTracked(); var item = EmitTracked(kernel); RemoveTracked(kernel);
            ReplaceTracked(kernel); ClearTracked(kernel); return Observe(item, item.State) + kernel.Handlers.Count;
        }
    }
}

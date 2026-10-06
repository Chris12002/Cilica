using System;
using System.Collections.Generic;

namespace Cilica.Fixtures.Execution
{
    public struct EmbeddingSessionView { public int Steps; public long Total; public int TextLength; }
    public sealed class EmbeddingSession
    {
        public string Text = "";
        public long Total = 9007199254740993L;
        public List<int> Values = new List<int>();
    }
    public static class EmbeddingSessionProbe
    {
        public static EmbeddingSession Open(string label) => new EmbeddingSession { Text = label };
        public static EmbeddingSessionView Apply(EmbeddingSession session, int delta)
        {
            if (delta < 0) throw new InvalidOperationException("negative delta");
            session.Values.Add(delta);
            session.Total = unchecked(session.Total + delta);
            session.Text = string.Concat(string.Concat(session.Text, ":"),
                delta.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Read(session);
        }
        public static EmbeddingSessionView Read(EmbeddingSession session) => new EmbeddingSessionView {
            Steps = session.Values.Count, Total = session.Total, TextLength = session.Text.Length
        };
        public static string Snapshot(EmbeddingSession session) => session.Text;
        public static EmbeddingSessionView Echo(EmbeddingSessionView value) => value;
        public static int Root(int delta)
        {
            var session = Open("seed");
            var view = Apply(session, delta);
            return view.Steps + Echo(Read(session)).TextLength + Snapshot(session).Length;
        }
    }
}

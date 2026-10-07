using System;
namespace Cilica.Fixtures.Execution
{
    public static class GuidParseProbe
    {
        private static int Outcome(string text,int seed)
        {
            try { var guid=Guid.Parse(text);return GuidTextProbe.Hash(guid.ToString("D"),seed); }
            catch(ArgumentNullException e) { return GuidTextProbe.Hash(e.Message,GuidTextProbe.Hash(e.ParamName,seed+1)); }
            catch(FormatException e) { return GuidTextProbe.Hash(e.Message,seed+2); }
            catch(OverflowException e) { return GuidTextProbe.Hash(e.Message,seed+3); }
        }
        public static int RoundTrip(int a,int b)
        { var original=GuidTextProbe.Value(a,b);var parsed=Guid.Parse(original.ToString(GuidTextProbe.Format(b)));return parsed.Equals(original)?1:0; }
        public static int Repeated(int a,int b)
        { var original=GuidTextProbe.Value(a,b);string text=original.ToString("X");var parsed=original;for(int i=0;i<9;i++)parsed=Guid.Parse(text);return parsed.Equals(original)?1:0; }
        public static int Storage(int a,int b)
        {
            var original=GuidTextProbe.Value(a,b);var parsed=Guid.Parse(original.ToString("D"));
            var holder=new GuidTextHolder();holder.Value=parsed;var values=new Guid[1];values[0]=parsed;
            Guid? nullable=parsed;object boxed=parsed;
            return holder.Value.Equals(original)&&values[0].Equals(original)&&nullable.Value.Equals(original)&&((Guid)boxed).Equals(original)?1:0;
        }
        public static int Whitespace(int a,int b)
        {
            var chars=new char[1];chars[0]=unchecked((char)a);var space=new string(chars);
            return Outcome(space+"00112233-4455-6677-8899-aabbccddeeff"+space,b);
        }
        public static int Mutation(int a,int b)
        {
            string text=GuidTextProbe.Value(a,b).ToString(GuidTextProbe.Format(a));var chars=new char[text.Length];
            for(int i=0;i<text.Length;i++) chars[i]=text[i];
            chars[(int)((uint)a%(uint)text.Length)]=unchecked((char)b);
            return Outcome(new string(chars),a);
        }
        public static int Corpus(int a,int b) => Outcome(GuidTextProbe.Pick(a),b);
    }
}

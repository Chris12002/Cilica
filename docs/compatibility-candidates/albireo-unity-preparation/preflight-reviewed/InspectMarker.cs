using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Collections.Generic;
// Owned read-only metadata inspection. Does not load or invoke the inspected assembly.
class InspectMarker {
 static int Main(string[] args) {
  var ops=new Dictionary<ushort,OpCode>();
  foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))
   if(f.GetValue(null) is OpCode op) ops[unchecked((ushort)op.Value)]=op;
  using var stream=File.OpenRead(args[0]);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();int begins=0,ends=0;
  foreach(var h in md.MethodDefinitions) {
   var method=md.GetMethodDefinition(h);if(method.RelativeVirtualAddress==0)continue;
   var bytes=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();int i=0;
   while(i<bytes.Length) {
    int at=i;ushort value=bytes[i++];if(value==0xfe)value=(ushort)(0xfe00|bytes[i++]);var op=ops[value];
    if(op==OpCodes.Call) {
     int token=BitConverter.ToInt32(bytes,i);var handle=MetadataTokens.EntityHandle(token);
     if(handle.Kind==HandleKind.MemberReference) {
      var m=md.GetMemberReference((MemberReferenceHandle)handle);string name=md.GetString(m.Name);
      if(m.Parent.Kind==HandleKind.TypeReference) {
       var t=md.GetTypeReference((TypeReferenceHandle)m.Parent);
       if(md.GetString(t.Namespace)=="UnityEngine.Profiling"&&md.GetString(t.Name)=="Profiler"&&(name=="BeginSample"||name=="EndSample")) {
        Console.WriteLine($"{md.GetString(method.Name)} IL_{at:x4}: call UnityEngine.Profiling.Profiler.{name}");if(name=="BeginSample")begins++;else ends++;
       }
      }
     }
    }
    i+=op.OperandType switch {
     OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
     OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,
     OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(bytes,i),_=>4};
   }
  }
  Console.WriteLine($"begin={begins}; end={ends}; metadata-only inspection");return begins==1&&ends==1?0:1;
 }
}

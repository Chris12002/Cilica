	.method family hidebysig static
		void BuildModificationData (
			class DiffPlex.Model.ModificationData A,
			class DiffPlex.Model.ModificationData B
		) cil managed
	{
		// Method begins at RVA 0x2530
		// Header size: 12
		// Code size: 80 (0x50)
		.maxstack 8
		.locals init (
			[0] valuetype Cilica.SectionTiming.SectionClock/Scope,
			[1] int32,
			[2] int32,
			[3] int32[],
			[4] int32[]
		)

		IL_0000: ldloca.s 0
		IL_0002: ldc.i4.3
		IL_0003: call instance void Cilica.SectionTiming.SectionClock/Scope::.ctor(int32)
		.try
		{
			IL_0008: ldarg.0
			IL_0009: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_000e: ldlen
			IL_000f: conv.i4
			IL_0010: stloc.1
			IL_0011: ldarg.1
			IL_0012: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_0017: ldlen
			IL_0018: conv.i4
			IL_0019: stloc.2
			IL_001a: ldloc.2
			IL_001b: ldloc.1
			IL_001c: add
			IL_001d: ldc.i4.1
			IL_001e: add
			IL_001f: dup
			IL_0020: ldc.i4.1
			IL_0021: add
			IL_0022: newarr [netstandard]System.Int32
			IL_0027: stloc.3
			IL_0028: ldc.i4.1
			IL_0029: add
			IL_002a: newarr [netstandard]System.Int32
			IL_002f: stloc.s 4
			IL_0031: ldarg.0
			IL_0032: ldc.i4.0
			IL_0033: ldloc.1
			IL_0034: ldarg.1
			IL_0035: ldc.i4.0
			IL_0036: ldloc.2
			IL_0037: ldloc.3
			IL_0038: ldloc.s 4
			IL_003a: call void DiffPlex.Differ::BuildModificationData(class DiffPlex.Model.ModificationData, int32, int32, class DiffPlex.Model.ModificationData, int32, int32, int32[], int32[])
			IL_003f: leave.s IL_004f
		} // end .try
		finally
		{
			IL_0041: ldloca.s 0
			IL_0043: constrained. Cilica.SectionTiming.SectionClock/Scope
			IL_0049: callvirt instance void [netstandard]System.IDisposable::Dispose()
			IL_004e: endfinally
		} // end handler

		IL_004f: ret
	} // end of method Differ::BuildModificationData

	.method private hidebysig static
		void BuildPieceHashes (
			class [netstandard]System.Collections.Generic.IDictionary`2<string, int32> pieceHash,
			class DiffPlex.Model.ModificationData data,
			bool ignoreWhitespace,
			class DiffPlex.IChunker chunker
		) cil managed
	{
		// Method begins at RVA 0x2780
		// Header size: 12
		// Code size: 215 (0xd7)
		.maxstack 3
		.locals init (
			[0] valuetype Cilica.SectionTiming.SectionClock/Scope,
			[1] class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>,
			[2] int32,
			[3] string,
			[4] int32
		)

		IL_0000: ldloca.s 0
		IL_0002: ldc.i4.2
		IL_0003: call instance void Cilica.SectionTiming.SectionClock/Scope::.ctor(int32)
		.try
		{
			IL_0008: ldarg.1
			IL_0009: callvirt instance string DiffPlex.Model.ModificationData::get_RawData()
			IL_000e: call bool [netstandard]System.String::IsNullOrEmpty(string)
			IL_0013: brfalse.s IL_003b

			IL_0015: ldarg.1
			IL_0016: call !!0[] [netstandard]System.Array::Empty<string>()
			IL_001b: callvirt instance void DiffPlex.Model.ModificationData::set_Pieces(class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>)
			IL_0020: ldarg.1
			IL_0021: call !!0[] [netstandard]System.Array::Empty<int32>()
			IL_0026: callvirt instance void DiffPlex.Model.ModificationData::set_HashedPieces(int32[])
			IL_002b: ldarg.1
			IL_002c: call !!0[] [netstandard]System.Array::Empty<bool>()
			IL_0031: callvirt instance void DiffPlex.Model.ModificationData::set_Modifications(bool[])
			IL_0036: leave IL_00d6

			IL_003b: ldarg.3
			IL_003c: ldarg.1
			IL_003d: callvirt instance string DiffPlex.Model.ModificationData::get_RawData()
			IL_0042: callvirt instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> DiffPlex.IChunker::Chunk(string)
			IL_0047: stloc.1
			IL_0048: ldarg.1
			IL_0049: ldloc.1
			IL_004a: callvirt instance void DiffPlex.Model.ModificationData::set_Pieces(class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>)
			IL_004f: ldarg.1
			IL_0050: ldloc.1
			IL_0051: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
			IL_0056: newarr [netstandard]System.Int32
			IL_005b: callvirt instance void DiffPlex.Model.ModificationData::set_HashedPieces(int32[])
			IL_0060: ldarg.1
			IL_0061: ldloc.1
			IL_0062: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
			IL_0067: newarr [netstandard]System.Boolean
			IL_006c: callvirt instance void DiffPlex.Model.ModificationData::set_Modifications(bool[])
			IL_0071: ldc.i4.0
			IL_0072: stloc.2
			IL_0073: br.s IL_00bd
			// loop start (head: IL_00bd)
				IL_0075: ldloc.1
				IL_0076: ldloc.2
				IL_0077: callvirt instance !0 class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>::get_Item(int32)
				IL_007c: stloc.3
				IL_007d: ldarg.2
				IL_007e: brfalse.s IL_0087

				IL_0080: ldloc.3
				IL_0081: callvirt instance string [netstandard]System.String::Trim()
				IL_0086: stloc.3

				IL_0087: ldarg.0
				IL_0088: ldloc.3
				IL_0089: ldloca.s 4
				IL_008b: callvirt instance bool class [netstandard]System.Collections.Generic.IDictionary`2<string, int32>::TryGetValue(!0, !1&)
				IL_0090: brfalse.s IL_009e

				IL_0092: ldarg.1
				IL_0093: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
				IL_0098: ldloc.2
				IL_0099: ldloc.s 4
				IL_009b: stelem.i4
				IL_009c: br.s IL_00b9

				IL_009e: ldarg.1
				IL_009f: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
				IL_00a4: ldloc.2
				IL_00a5: ldarg.0
				IL_00a6: callvirt instance int32 class [netstandard]System.Collections.Generic.ICollection`1<valuetype [netstandard]System.Collections.Generic.KeyValuePair`2<string, int32>>::get_Count()
				IL_00ab: stelem.i4
				IL_00ac: ldarg.0
				IL_00ad: ldloc.3
				IL_00ae: ldarg.0
				IL_00af: callvirt instance int32 class [netstandard]System.Collections.Generic.ICollection`1<valuetype [netstandard]System.Collections.Generic.KeyValuePair`2<string, int32>>::get_Count()
				IL_00b4: callvirt instance void class [netstandard]System.Collections.Generic.IDictionary`2<string, int32>::set_Item(!0, !1)

				IL_00b9: ldloc.2
				IL_00ba: ldc.i4.1
				IL_00bb: add
				IL_00bc: stloc.2

				IL_00bd: ldloc.2
				IL_00be: ldloc.1
				IL_00bf: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
				IL_00c4: blt.s IL_0075
			// end loop

			IL_00c6: leave.s IL_00d6
		} // end .try
		finally
		{
			IL_00c8: ldloca.s 0
			IL_00ca: constrained. Cilica.SectionTiming.SectionClock/Scope
			IL_00d0: callvirt instance void [netstandard]System.IDisposable::Dispose()
			IL_00d5: endfinally
		} // end handler

		IL_00d6: ret
	} // end of method Differ::BuildPieceHashes

	.method public final hidebysig newslot virtual
		instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> Chunk (
			string text
		) cil managed
	{
		// Method begins at RVA 0x2894
		// Header size: 12
		// Code size: 77 (0x4d)
		.maxstack 4
		.locals init (
			[0] valuetype Cilica.SectionTiming.SectionClock/Scope,
			[1] string[],
			[2] int32,
			[3] char,
			[4] class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>
		)

		IL_0000: ldloca.s 0
		IL_0002: ldc.i4.1
		IL_0003: call instance void Cilica.SectionTiming.SectionClock/Scope::.ctor(int32)
		.try
		{
			IL_0008: ldarg.1
			IL_0009: callvirt instance int32 [netstandard]System.String::get_Length()
			IL_000e: newarr [netstandard]System.String
			IL_0013: stloc.1
			IL_0014: ldc.i4.0
			IL_0015: stloc.2
			IL_0016: br.s IL_002e
			// loop start (head: IL_002e)
				IL_0018: ldloc.1
				IL_0019: ldloc.2
				IL_001a: ldarg.1
				IL_001b: ldloc.2
				IL_001c: callvirt instance char [netstandard]System.String::get_Chars(int32)
				IL_0021: stloc.3
				IL_0022: ldloca.s 3
				IL_0024: call instance string [netstandard]System.Char::ToString()
				IL_0029: stelem.ref
				IL_002a: ldloc.2
				IL_002b: ldc.i4.1
				IL_002c: add
				IL_002d: stloc.2

				IL_002e: ldloc.2
				IL_002f: ldarg.1
				IL_0030: callvirt instance int32 [netstandard]System.String::get_Length()
				IL_0035: blt.s IL_0018
			// end loop

			IL_0037: ldloc.1
			IL_0038: stloc.s 4
			IL_003a: leave.s IL_004a
		} // end .try
		finally
		{
			IL_003c: ldloca.s 0
			IL_003e: constrained. Cilica.SectionTiming.SectionClock/Scope
			IL_0044: callvirt instance void [netstandard]System.IDisposable::Dispose()
			IL_0049: endfinally
		} // end handler

		IL_004a: ldloc.s 4
		IL_004c: ret
	} // end of method CharacterChunker::Chunk

	.method public final hidebysig newslot virtual
		instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> Chunk (
			string text
		) cil managed
	{
		// Method begins at RVA 0x2a64
		// Header size: 12
		// Code size: 40 (0x28)
		.maxstack 3
		.locals init (
			[0] valuetype Cilica.SectionTiming.SectionClock/Scope,
			[1] class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>
		)

		IL_0000: ldloca.s 0
		IL_0002: ldc.i4.1
		IL_0003: call instance void Cilica.SectionTiming.SectionClock/Scope::.ctor(int32)
		.try
		{
			IL_0008: ldarg.1
			IL_0009: ldarg.0
			IL_000a: ldfld string[] DiffPlex.Chunkers.LineChunker::lineSeparators
			IL_000f: ldc.i4.0
			IL_0010: callvirt instance string[] [netstandard]System.String::Split(string[], valuetype [netstandard]System.StringSplitOptions)
			IL_0015: stloc.1
			IL_0016: leave.s IL_0026
		} // end .try
		finally
		{
			IL_0018: ldloca.s 0
			IL_001a: constrained. Cilica.SectionTiming.SectionClock/Scope
			IL_0020: callvirt instance void [netstandard]System.IDisposable::Dispose()
			IL_0025: endfinally
		} // end handler

		IL_0026: ldloc.1
		IL_0027: ret
	} // end of method LineChunker::Chunk

	.method private hidebysig static
		class DiffPlex.Model.DiffResult Diff (
			int32 work
		) cil managed
	{
		// Method begins at RVA 0x3074
		// Header size: 12
		// Code size: 80 (0x50)
		.maxstack 4
		.locals init (
			[0] valuetype Cilica.SectionTiming.SectionClock/Scope,
			[1] class DiffPlex.Differ,
			[2] class DiffPlex.Model.DiffResult
		)

		IL_0000: ldloca.s 0
		IL_0002: ldc.i4.0
		IL_0003: call instance void Cilica.SectionTiming.SectionClock/Scope::.ctor(int32)
		.try
		{
			IL_0008: newobj instance void DiffPlex.Differ::.ctor()
			IL_000d: stloc.1
			IL_000e: ldarg.0
			IL_000f: brfalse.s IL_0028

			IL_0011: ldloc.1
			IL_0012: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
			IL_0017: ldc.i4.2
			IL_0018: ldelem.ref
			IL_0019: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
			IL_001e: ldc.i4.3
			IL_001f: ldelem.ref
			IL_0020: ldc.i4.0
			IL_0021: callvirt instance class DiffPlex.Model.DiffResult DiffPlex.Differ::CreateLineDiffs(string, string, bool)
			IL_0026: br.s IL_003d

			IL_0028: ldloc.1
			IL_0029: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
			IL_002e: ldc.i4.0
			IL_002f: ldelem.ref
			IL_0030: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
			IL_0035: ldc.i4.1
			IL_0036: ldelem.ref
			IL_0037: ldc.i4.0
			IL_0038: callvirt instance class DiffPlex.Model.DiffResult DiffPlex.Differ::CreateCharacterDiffs(string, string, bool)

			IL_003d: stloc.2
			IL_003e: leave.s IL_004e
		} // end .try
		finally
		{
			IL_0040: ldloca.s 0
			IL_0042: constrained. Cilica.SectionTiming.SectionClock/Scope
			IL_0048: callvirt instance void [netstandard]System.IDisposable::Dispose()
			IL_004d: endfinally
		} // end handler

		IL_004e: ldloc.2
		IL_004f: ret
	} // end of method BenchProbe::Diff

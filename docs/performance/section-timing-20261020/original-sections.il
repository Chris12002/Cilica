	.method family hidebysig static
		void BuildModificationData (
			class DiffPlex.Model.ModificationData A,
			class DiffPlex.Model.ModificationData B
		) cil managed
	{
		// Method begins at RVA 0x2530
		// Header size: 12
		// Code size: 54 (0x36)
		.maxstack 8
		.locals init (
			[0] int32,
			[1] int32,
			[2] int32[],
			[3] int32[]
		)

		IL_0000: ldarg.0
		IL_0001: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
		IL_0006: ldlen
		IL_0007: conv.i4
		IL_0008: stloc.0
		IL_0009: ldarg.1
		IL_000a: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
		IL_000f: ldlen
		IL_0010: conv.i4
		IL_0011: stloc.1
		IL_0012: ldloc.1
		IL_0013: ldloc.0
		IL_0014: add
		IL_0015: ldc.i4.1
		IL_0016: add
		IL_0017: dup
		IL_0018: ldc.i4.1
		IL_0019: add
		IL_001a: newarr [netstandard]System.Int32
		IL_001f: stloc.2
		IL_0020: ldc.i4.1
		IL_0021: add
		IL_0022: newarr [netstandard]System.Int32
		IL_0027: stloc.3
		IL_0028: ldarg.0
		IL_0029: ldc.i4.0
		IL_002a: ldloc.0
		IL_002b: ldarg.1
		IL_002c: ldc.i4.0
		IL_002d: ldloc.1
		IL_002e: ldloc.2
		IL_002f: ldloc.3
		IL_0030: call void DiffPlex.Differ::BuildModificationData(class DiffPlex.Model.ModificationData, int32, int32, class DiffPlex.Model.ModificationData, int32, int32, int32[], int32[])
		IL_0035: ret
	} // end of method Differ::BuildModificationData

	.method private hidebysig static
		void BuildPieceHashes (
			class [netstandard]System.Collections.Generic.IDictionary`2<string, int32> pieceHash,
			class DiffPlex.Model.ModificationData data,
			bool ignoreWhitespace,
			class DiffPlex.IChunker chunker
		) cil managed
	{
		// Method begins at RVA 0x2758
		// Header size: 12
		// Code size: 186 (0xba)
		.maxstack 3
		.locals init (
			[0] class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>,
			[1] int32,
			[2] string,
			[3] int32
		)

		IL_0000: ldarg.1
		IL_0001: callvirt instance string DiffPlex.Model.ModificationData::get_RawData()
		IL_0006: call bool [netstandard]System.String::IsNullOrEmpty(string)
		IL_000b: brfalse.s IL_002f

		IL_000d: ldarg.1
		IL_000e: call !!0[] [netstandard]System.Array::Empty<string>()
		IL_0013: callvirt instance void DiffPlex.Model.ModificationData::set_Pieces(class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>)
		IL_0018: ldarg.1
		IL_0019: call !!0[] [netstandard]System.Array::Empty<int32>()
		IL_001e: callvirt instance void DiffPlex.Model.ModificationData::set_HashedPieces(int32[])
		IL_0023: ldarg.1
		IL_0024: call !!0[] [netstandard]System.Array::Empty<bool>()
		IL_0029: callvirt instance void DiffPlex.Model.ModificationData::set_Modifications(bool[])
		IL_002e: ret

		IL_002f: ldarg.3
		IL_0030: ldarg.1
		IL_0031: callvirt instance string DiffPlex.Model.ModificationData::get_RawData()
		IL_0036: callvirt instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> DiffPlex.IChunker::Chunk(string)
		IL_003b: stloc.0
		IL_003c: ldarg.1
		IL_003d: ldloc.0
		IL_003e: callvirt instance void DiffPlex.Model.ModificationData::set_Pieces(class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>)
		IL_0043: ldarg.1
		IL_0044: ldloc.0
		IL_0045: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
		IL_004a: newarr [netstandard]System.Int32
		IL_004f: callvirt instance void DiffPlex.Model.ModificationData::set_HashedPieces(int32[])
		IL_0054: ldarg.1
		IL_0055: ldloc.0
		IL_0056: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
		IL_005b: newarr [netstandard]System.Boolean
		IL_0060: callvirt instance void DiffPlex.Model.ModificationData::set_Modifications(bool[])
		IL_0065: ldc.i4.0
		IL_0066: stloc.1
		IL_0067: br.s IL_00b0
		// loop start (head: IL_00b0)
			IL_0069: ldloc.0
			IL_006a: ldloc.1
			IL_006b: callvirt instance !0 class [netstandard]System.Collections.Generic.IReadOnlyList`1<string>::get_Item(int32)
			IL_0070: stloc.2
			IL_0071: ldarg.2
			IL_0072: brfalse.s IL_007b

			IL_0074: ldloc.2
			IL_0075: callvirt instance string [netstandard]System.String::Trim()
			IL_007a: stloc.2

			IL_007b: ldarg.0
			IL_007c: ldloc.2
			IL_007d: ldloca.s 3
			IL_007f: callvirt instance bool class [netstandard]System.Collections.Generic.IDictionary`2<string, int32>::TryGetValue(!0, !1&)
			IL_0084: brfalse.s IL_0091

			IL_0086: ldarg.1
			IL_0087: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_008c: ldloc.1
			IL_008d: ldloc.3
			IL_008e: stelem.i4
			IL_008f: br.s IL_00ac

			IL_0091: ldarg.1
			IL_0092: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_0097: ldloc.1
			IL_0098: ldarg.0
			IL_0099: callvirt instance int32 class [netstandard]System.Collections.Generic.ICollection`1<valuetype [netstandard]System.Collections.Generic.KeyValuePair`2<string, int32>>::get_Count()
			IL_009e: stelem.i4
			IL_009f: ldarg.0
			IL_00a0: ldloc.2
			IL_00a1: ldarg.0
			IL_00a2: callvirt instance int32 class [netstandard]System.Collections.Generic.ICollection`1<valuetype [netstandard]System.Collections.Generic.KeyValuePair`2<string, int32>>::get_Count()
			IL_00a7: callvirt instance void class [netstandard]System.Collections.Generic.IDictionary`2<string, int32>::set_Item(!0, !1)

			IL_00ac: ldloc.1
			IL_00ad: ldc.i4.1
			IL_00ae: add
			IL_00af: stloc.1

			IL_00b0: ldloc.1
			IL_00b1: ldloc.0
			IL_00b2: callvirt instance int32 class [netstandard]System.Collections.Generic.IReadOnlyCollection`1<string>::get_Count()
			IL_00b7: blt.s IL_0069
		// end loop

		IL_00b9: ret
	} // end of method Differ::BuildPieceHashes

	.method public final hidebysig newslot virtual
		instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> Chunk (
			string text
		) cil managed
	{
		// Method begins at RVA 0x2840
		// Header size: 12
		// Code size: 49 (0x31)
		.maxstack 4
		.locals init (
			[0] string[],
			[1] int32,
			[2] char
		)

		IL_0000: ldarg.1
		IL_0001: callvirt instance int32 [netstandard]System.String::get_Length()
		IL_0006: newarr [netstandard]System.String
		IL_000b: stloc.0
		IL_000c: ldc.i4.0
		IL_000d: stloc.1
		IL_000e: br.s IL_0026
		// loop start (head: IL_0026)
			IL_0010: ldloc.0
			IL_0011: ldloc.1
			IL_0012: ldarg.1
			IL_0013: ldloc.1
			IL_0014: callvirt instance char [netstandard]System.String::get_Chars(int32)
			IL_0019: stloc.2
			IL_001a: ldloca.s 2
			IL_001c: call instance string [netstandard]System.Char::ToString()
			IL_0021: stelem.ref
			IL_0022: ldloc.1
			IL_0023: ldc.i4.1
			IL_0024: add
			IL_0025: stloc.1

			IL_0026: ldloc.1
			IL_0027: ldarg.1
			IL_0028: callvirt instance int32 [netstandard]System.String::get_Length()
			IL_002d: blt.s IL_0010
		// end loop

		IL_002f: ldloc.0
		IL_0030: ret
	} // end of method CharacterChunker::Chunk

	.method public final hidebysig newslot virtual
		instance class [netstandard]System.Collections.Generic.IReadOnlyList`1<string> Chunk (
			string text
		) cil managed
	{
		// Method begins at RVA 0x29df
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldarg.1
		IL_0001: ldarg.0
		IL_0002: ldfld string[] DiffPlex.Chunkers.LineChunker::lineSeparators
		IL_0007: ldc.i4.0
		IL_0008: callvirt instance string[] [netstandard]System.String::Split(string[], valuetype [netstandard]System.StringSplitOptions)
		IL_000d: ret
	} // end of method LineChunker::Chunk

	.method private hidebysig static
		class DiffPlex.Model.DiffResult Diff (
			int32 work
		) cil managed
	{
		// Method begins at RVA 0x2d28
		// Header size: 12
		// Code size: 53 (0x35)
		.maxstack 4
		.locals init (
			[0] class DiffPlex.Differ
		)

		IL_0000: newobj instance void DiffPlex.Differ::.ctor()
		IL_0005: stloc.0
		IL_0006: ldarg.0
		IL_0007: brfalse.s IL_001f

		IL_0009: ldloc.0
		IL_000a: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
		IL_000f: ldc.i4.2
		IL_0010: ldelem.ref
		IL_0011: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
		IL_0016: ldc.i4.3
		IL_0017: ldelem.ref
		IL_0018: ldc.i4.0
		IL_0019: callvirt instance class DiffPlex.Model.DiffResult DiffPlex.Differ::CreateLineDiffs(string, string, bool)
		IL_001e: ret

		IL_001f: ldloc.0
		IL_0020: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
		IL_0025: ldc.i4.0
		IL_0026: ldelem.ref
		IL_0027: ldsfld string[] Cilica.CandidateBenchmark.BenchProbe::Inputs
		IL_002c: ldc.i4.1
		IL_002d: ldelem.ref
		IL_002e: ldc.i4.0
		IL_002f: callvirt instance class DiffPlex.Model.DiffResult DiffPlex.Differ::CreateCharacterDiffs(string, string, bool)
		IL_0034: ret
	} // end of method BenchProbe::Diff

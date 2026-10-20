	.method family hidebysig static
		class DiffPlex.Model.EditLengthResult CalculateEditLength (
			int32[] A,
			int32 startA,
			int32 endA,
			int32[] B,
			int32 startB,
			int32 endB
		) cil managed
	{
		// Method begins at RVA 0x224c
		// Header size: 12
		// Code size: 46 (0x2e)
		.maxstack 8
		.locals init (
			[0] int32,
			[1] int32[],
			[2] int32[]
		)

		IL_0000: ldarg.2
		IL_0001: ldarg.1
		IL_0002: sub
		IL_0003: stloc.0
		IL_0004: ldarg.s endB
		IL_0006: ldarg.s startB
		IL_0008: sub
		IL_0009: ldloc.0
		IL_000a: add
		IL_000b: ldc.i4.1
		IL_000c: add
		IL_000d: dup
		IL_000e: ldc.i4.1
		IL_000f: add
		IL_0010: newarr [netstandard]System.Int32
		IL_0015: stloc.1
		IL_0016: ldc.i4.1
		IL_0017: add
		IL_0018: newarr [netstandard]System.Int32
		IL_001d: stloc.2
		IL_001e: ldarg.0
		IL_001f: ldarg.1
		IL_0020: ldarg.2
		IL_0021: ldarg.3
		IL_0022: ldarg.s startB
		IL_0024: ldarg.s endB
		IL_0026: ldloc.1
		IL_0027: ldloc.2
		IL_0028: call class DiffPlex.Model.EditLengthResult DiffPlex.Differ::CalculateEditLength(int32[], int32, int32, int32[], int32, int32, int32[], int32[])
		IL_002d: ret
	} // end of method Differ::CalculateEditLength

	.method private hidebysig static
		class DiffPlex.Model.EditLengthResult CalculateEditLength (
			int32[] A,
			int32 startA,
			int32 endA,
			int32[] B,
			int32 startB,
			int32 endB,
			int32[] forwardDiagonal,
			int32[] reverseDiagonal
		) cil managed
	{
		// Method begins at RVA 0x2288
		// Header size: 12
		// Code size: 665 (0x299)
		.maxstack 4
		.locals init (
			[0] int32,
			[1] int32,
			[2] int32,
			[3] int32,
			[4] bool,
			[5] int32,
			[6] valuetype DiffPlex.Model.Edit,
			[7] int32,
			[8] int32,
			[9] int32,
			[10] int32,
			[11] int32,
			[12] int32,
			[13] int32,
			[14] int32,
			[15] int32,
			[16] int32,
			[17] int32,
			[18] int32,
			[19] int32,
			[20] int32,
			[21] int32,
			[22] int32
		)

		IL_0000: ldarg.0
		IL_0001: brtrue.s IL_000e

		IL_0003: ldstr "A"
		IL_0008: newobj instance void [netstandard]System.ArgumentNullException::.ctor(string)
		IL_000d: throw

		IL_000e: ldarg.3
		IL_000f: brtrue.s IL_001c

		IL_0011: ldstr "B"
		IL_0016: newobj instance void [netstandard]System.ArgumentNullException::.ctor(string)
		IL_001b: throw

		IL_001c: ldarg.0
		IL_001d: ldlen
		IL_001e: brtrue.s IL_002a

		IL_0020: ldarg.3
		IL_0021: ldlen
		IL_0022: brtrue.s IL_002a

		IL_0024: newobj instance void DiffPlex.Model.EditLengthResult::.ctor()
		IL_0029: ret

		IL_002a: ldarg.2
		IL_002b: ldarg.1
		IL_002c: sub
		IL_002d: stloc.0
		IL_002e: ldarg.s endB
		IL_0030: ldarg.s startB
		IL_0032: sub
		IL_0033: stloc.1
		IL_0034: ldloc.1
		IL_0035: ldloc.0
		IL_0036: add
		IL_0037: ldc.i4.1
		IL_0038: add
		IL_0039: ldc.i4.2
		IL_003a: div
		IL_003b: stloc.2
		IL_003c: ldloc.0
		IL_003d: ldloc.1
		IL_003e: sub
		IL_003f: stloc.3
		IL_0040: ldloc.3
		IL_0041: ldc.i4.2
		IL_0042: rem
		IL_0043: ldc.i4.0
		IL_0044: ceq
		IL_0046: stloc.s 4
		IL_0048: ldarg.s forwardDiagonal
		IL_004a: ldc.i4.1
		IL_004b: ldloc.2
		IL_004c: add
		IL_004d: ldc.i4.0
		IL_004e: stelem.i4
		IL_004f: ldarg.s reverseDiagonal
		IL_0051: ldc.i4.1
		IL_0052: ldloc.2
		IL_0053: add
		IL_0054: ldloc.0
		IL_0055: ldc.i4.1
		IL_0056: add
		IL_0057: stelem.i4
		IL_0058: ldc.i4.0
		IL_0059: stloc.s 5
		IL_005b: br IL_0286
		// loop start (head: IL_0286)
			IL_0060: ldloc.s 5
			IL_0062: neg
			IL_0063: stloc.s 7
			IL_0065: br IL_0166
			// loop start (head: IL_0166)
				IL_006a: ldloc.s 7
				IL_006c: ldloc.2
				IL_006d: add
				IL_006e: stloc.s 8
				IL_0070: ldloc.s 7
				IL_0072: ldloc.s 5
				IL_0074: neg
				IL_0075: beq.s IL_008d

				IL_0077: ldloc.s 7
				IL_0079: ldloc.s 5
				IL_007b: beq.s IL_009b

				IL_007d: ldarg.s forwardDiagonal
				IL_007f: ldloc.s 8
				IL_0081: ldc.i4.1
				IL_0082: sub
				IL_0083: ldelem.i4
				IL_0084: ldarg.s forwardDiagonal
				IL_0086: ldloc.s 8
				IL_0088: ldc.i4.1
				IL_0089: add
				IL_008a: ldelem.i4
				IL_008b: bge.s IL_009b

				IL_008d: ldarg.s forwardDiagonal
				IL_008f: ldloc.s 8
				IL_0091: ldc.i4.1
				IL_0092: add
				IL_0093: ldelem.i4
				IL_0094: stloc.s 9
				IL_0096: ldc.i4.3
				IL_0097: stloc.s 6
				IL_0099: br.s IL_00a9

				IL_009b: ldarg.s forwardDiagonal
				IL_009d: ldloc.s 8
				IL_009f: ldc.i4.1
				IL_00a0: sub
				IL_00a1: ldelem.i4
				IL_00a2: ldc.i4.1
				IL_00a3: add
				IL_00a4: stloc.s 9
				IL_00a6: ldc.i4.1
				IL_00a7: stloc.s 6

				IL_00a9: ldloc.s 9
				IL_00ab: ldloc.s 7
				IL_00ad: sub
				IL_00ae: stloc.s 10
				IL_00b0: ldloc.s 9
				IL_00b2: stloc.s 11
				IL_00b4: ldloc.s 10
				IL_00b6: stloc.s 12
				IL_00b8: br.s IL_00c6
				// loop start (head: IL_00c6)
					IL_00ba: ldloc.s 9
					IL_00bc: ldc.i4.1
					IL_00bd: add
					IL_00be: stloc.s 9
					IL_00c0: ldloc.s 10
					IL_00c2: ldc.i4.1
					IL_00c3: add
					IL_00c4: stloc.s 10

					IL_00c6: ldloc.s 9
					IL_00c8: ldloc.0
					IL_00c9: bge.s IL_00df

					IL_00cb: ldloc.s 10
					IL_00cd: ldloc.1
					IL_00ce: bge.s IL_00df

					IL_00d0: ldarg.0
					IL_00d1: ldloc.s 9
					IL_00d3: ldarg.1
					IL_00d4: add
					IL_00d5: ldelem.i4
					IL_00d6: ldarg.3
					IL_00d7: ldloc.s 10
					IL_00d9: ldarg.s startB
					IL_00db: add
					IL_00dc: ldelem.i4
					IL_00dd: beq.s IL_00ba
				// end loop

				IL_00df: ldarg.s forwardDiagonal
				IL_00e1: ldloc.s 8
				IL_00e3: ldloc.s 9
				IL_00e5: stelem.i4
				IL_00e6: ldloc.s 4
				IL_00e8: brtrue.s IL_0160

				IL_00ea: ldloc.s 7
				IL_00ec: ldloc.3
				IL_00ed: sub
				IL_00ee: ldloc.s 5
				IL_00f0: neg
				IL_00f1: ldc.i4.1
				IL_00f2: add
				IL_00f3: blt.s IL_0160

				IL_00f5: ldloc.s 7
				IL_00f7: ldloc.3
				IL_00f8: sub
				IL_00f9: ldloc.s 5
				IL_00fb: ldc.i4.1
				IL_00fc: sub
				IL_00fd: bgt.s IL_0160

				IL_00ff: ldloc.s 7
				IL_0101: ldloc.3
				IL_0102: sub
				IL_0103: ldloc.2
				IL_0104: add
				IL_0105: stloc.s 13
				IL_0107: ldarg.s reverseDiagonal
				IL_0109: ldloc.s 13
				IL_010b: ldelem.i4
				IL_010c: dup
				IL_010d: ldloc.s 7
				IL_010f: sub
				IL_0110: stloc.s 14
				IL_0112: ldloc.s 9
				IL_0114: bgt.s IL_0160

				IL_0116: ldloc.s 14
				IL_0118: ldloc.s 10
				IL_011a: bgt.s IL_0160

				IL_011c: newobj instance void DiffPlex.Model.EditLengthResult::.ctor()
				IL_0121: dup
				IL_0122: ldc.i4.2
				IL_0123: ldloc.s 5
				IL_0125: mul
				IL_0126: ldc.i4.1
				IL_0127: sub
				IL_0128: callvirt instance void DiffPlex.Model.EditLengthResult::set_EditLength(int32)
				IL_012d: dup
				IL_012e: ldloc.s 11
				IL_0130: ldarg.1
				IL_0131: add
				IL_0132: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartX(int32)
				IL_0137: dup
				IL_0138: ldloc.s 12
				IL_013a: ldarg.s startB
				IL_013c: add
				IL_013d: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartY(int32)
				IL_0142: dup
				IL_0143: ldloc.s 9
				IL_0145: ldarg.1
				IL_0146: add
				IL_0147: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndX(int32)
				IL_014c: dup
				IL_014d: ldloc.s 10
				IL_014f: ldarg.s startB
				IL_0151: add
				IL_0152: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndY(int32)
				IL_0157: dup
				IL_0158: ldloc.s 6
				IL_015a: callvirt instance void DiffPlex.Model.EditLengthResult::set_LastEdit(valuetype DiffPlex.Model.Edit)
				IL_015f: ret

				IL_0160: ldloc.s 7
				IL_0162: ldc.i4.2
				IL_0163: add
				IL_0164: stloc.s 7

				IL_0166: ldloc.s 7
				IL_0168: ldloc.s 5
				IL_016a: ble IL_006a
			// end loop

			IL_016f: ldloc.s 5
			IL_0171: neg
			IL_0172: stloc.s 15
			IL_0174: br IL_0277
			// loop start (head: IL_0277)
				IL_0179: ldloc.s 15
				IL_017b: ldloc.2
				IL_017c: add
				IL_017d: stloc.s 16
				IL_017f: ldloc.s 15
				IL_0181: ldloc.s 5
				IL_0183: neg
				IL_0184: beq.s IL_019c

				IL_0186: ldloc.s 15
				IL_0188: ldloc.s 5
				IL_018a: beq.s IL_01ac

				IL_018c: ldarg.s reverseDiagonal
				IL_018e: ldloc.s 16
				IL_0190: ldc.i4.1
				IL_0191: add
				IL_0192: ldelem.i4
				IL_0193: ldarg.s reverseDiagonal
				IL_0195: ldloc.s 16
				IL_0197: ldc.i4.1
				IL_0198: sub
				IL_0199: ldelem.i4
				IL_019a: bgt.s IL_01ac

				IL_019c: ldarg.s reverseDiagonal
				IL_019e: ldloc.s 16
				IL_01a0: ldc.i4.1
				IL_01a1: add
				IL_01a2: ldelem.i4
				IL_01a3: ldc.i4.1
				IL_01a4: sub
				IL_01a5: stloc.s 17
				IL_01a7: ldc.i4.2
				IL_01a8: stloc.s 6
				IL_01aa: br.s IL_01b8

				IL_01ac: ldarg.s reverseDiagonal
				IL_01ae: ldloc.s 16
				IL_01b0: ldc.i4.1
				IL_01b1: sub
				IL_01b2: ldelem.i4
				IL_01b3: stloc.s 17
				IL_01b5: ldc.i4.4
				IL_01b6: stloc.s 6

				IL_01b8: ldloc.s 17
				IL_01ba: ldloc.s 15
				IL_01bc: ldloc.3
				IL_01bd: add
				IL_01be: sub
				IL_01bf: stloc.s 18
				IL_01c1: ldloc.s 17
				IL_01c3: stloc.s 19
				IL_01c5: ldloc.s 18
				IL_01c7: stloc.s 20
				IL_01c9: br.s IL_01d7
				// loop start (head: IL_01d7)
					IL_01cb: ldloc.s 17
					IL_01cd: ldc.i4.1
					IL_01ce: sub
					IL_01cf: stloc.s 17
					IL_01d1: ldloc.s 18
					IL_01d3: ldc.i4.1
					IL_01d4: sub
					IL_01d5: stloc.s 18

					IL_01d7: ldloc.s 17
					IL_01d9: ldc.i4.0
					IL_01da: ble.s IL_01f4

					IL_01dc: ldloc.s 18
					IL_01de: ldc.i4.0
					IL_01df: ble.s IL_01f4

					IL_01e1: ldarg.0
					IL_01e2: ldarg.1
					IL_01e3: ldloc.s 17
					IL_01e5: add
					IL_01e6: ldc.i4.1
					IL_01e7: sub
					IL_01e8: ldelem.i4
					IL_01e9: ldarg.3
					IL_01ea: ldarg.s startB
					IL_01ec: ldloc.s 18
					IL_01ee: add
					IL_01ef: ldc.i4.1
					IL_01f0: sub
					IL_01f1: ldelem.i4
					IL_01f2: beq.s IL_01cb
				// end loop

				IL_01f4: ldarg.s reverseDiagonal
				IL_01f6: ldloc.s 16
				IL_01f8: ldloc.s 17
				IL_01fa: stelem.i4
				IL_01fb: ldloc.s 4
				IL_01fd: brfalse.s IL_0271

				IL_01ff: ldloc.s 15
				IL_0201: ldloc.3
				IL_0202: add
				IL_0203: ldloc.s 5
				IL_0205: neg
				IL_0206: blt.s IL_0271

				IL_0208: ldloc.s 15
				IL_020a: ldloc.3
				IL_020b: add
				IL_020c: ldloc.s 5
				IL_020e: bgt.s IL_0271

				IL_0210: ldloc.s 15
				IL_0212: ldloc.3
				IL_0213: add
				IL_0214: ldloc.2
				IL_0215: add
				IL_0216: stloc.s 21
				IL_0218: ldarg.s forwardDiagonal
				IL_021a: ldloc.s 21
				IL_021c: ldelem.i4
				IL_021d: dup
				IL_021e: ldloc.s 15
				IL_0220: ldloc.3
				IL_0221: add
				IL_0222: sub
				IL_0223: stloc.s 22
				IL_0225: ldloc.s 17
				IL_0227: blt.s IL_0271

				IL_0229: ldloc.s 22
				IL_022b: ldloc.s 18
				IL_022d: blt.s IL_0271

				IL_022f: newobj instance void DiffPlex.Model.EditLengthResult::.ctor()
				IL_0234: dup
				IL_0235: ldc.i4.2
				IL_0236: ldloc.s 5
				IL_0238: mul
				IL_0239: callvirt instance void DiffPlex.Model.EditLengthResult::set_EditLength(int32)
				IL_023e: dup
				IL_023f: ldloc.s 17
				IL_0241: ldarg.1
				IL_0242: add
				IL_0243: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartX(int32)
				IL_0248: dup
				IL_0249: ldloc.s 18
				IL_024b: ldarg.s startB
				IL_024d: add
				IL_024e: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartY(int32)
				IL_0253: dup
				IL_0254: ldloc.s 19
				IL_0256: ldarg.1
				IL_0257: add
				IL_0258: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndX(int32)
				IL_025d: dup
				IL_025e: ldloc.s 20
				IL_0260: ldarg.s startB
				IL_0262: add
				IL_0263: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndY(int32)
				IL_0268: dup
				IL_0269: ldloc.s 6
				IL_026b: callvirt instance void DiffPlex.Model.EditLengthResult::set_LastEdit(valuetype DiffPlex.Model.Edit)
				IL_0270: ret

				IL_0271: ldloc.s 15
				IL_0273: ldc.i4.2
				IL_0274: add
				IL_0275: stloc.s 15

				IL_0277: ldloc.s 15
				IL_0279: ldloc.s 5
				IL_027b: ble IL_0179
			// end loop

			IL_0280: ldloc.s 5
			IL_0282: ldc.i4.1
			IL_0283: add
			IL_0284: stloc.s 5

			IL_0286: ldloc.s 5
			IL_0288: ldloc.2
			IL_0289: ble IL_0060
		// end loop

		IL_028e: ldstr "Should never get here"
		IL_0293: newobj instance void [netstandard]System.Exception::.ctor(string)
		IL_0298: throw
	} // end of method Differ::CalculateEditLength

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
		void BuildModificationData (
			class DiffPlex.Model.ModificationData A,
			int32 startA,
			int32 endA,
			class DiffPlex.Model.ModificationData B,
			int32 startB,
			int32 endB,
			int32[] forwardDiagonal,
			int32[] reverseDiagonal
		) cil managed
	{
		// Method begins at RVA 0x2574
		// Header size: 12
		// Code size: 472 (0x1d8)
		.maxstack 8
		.locals init (
			[0] int32,
			[1] int32,
			[2] class DiffPlex.Model.EditLengthResult,
			[3] int32,
			[4] int32,
			[5] int32
		)

		IL_0000: br.s IL_000d
		// loop start (head: IL_000d)
			IL_0002: ldarg.1
			IL_0003: ldc.i4.1
			IL_0004: add
			IL_0005: starg.s startA
			IL_0007: ldarg.s startB
			IL_0009: ldc.i4.1
			IL_000a: add
			IL_000b: starg.s startB

			IL_000d: ldarg.1
			IL_000e: ldarg.2
			IL_000f: bge.s IL_0040

			IL_0011: ldarg.s startB
			IL_0013: ldarg.s endB
			IL_0015: bge.s IL_0040

			IL_0017: ldarg.0
			IL_0018: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_001d: ldarg.1
			IL_001e: ldelema [netstandard]System.Int32
			IL_0023: ldarg.3
			IL_0024: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_0029: ldarg.s startB
			IL_002b: ldelem.i4
			IL_002c: call instance bool [netstandard]System.Int32::Equals(int32)
			IL_0031: brtrue.s IL_0002
		// end loop

		IL_0033: br.s IL_0040
		// loop start (head: IL_0040)
			IL_0035: ldarg.2
			IL_0036: ldc.i4.1
			IL_0037: sub
			IL_0038: starg.s endA
			IL_003a: ldarg.s endB
			IL_003c: ldc.i4.1
			IL_003d: sub
			IL_003e: starg.s endB

			IL_0040: ldarg.1
			IL_0041: ldarg.2
			IL_0042: bge.s IL_006a

			IL_0044: ldarg.s startB
			IL_0046: ldarg.s endB
			IL_0048: bge.s IL_006a

			IL_004a: ldarg.0
			IL_004b: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_0050: ldarg.2
			IL_0051: ldc.i4.1
			IL_0052: sub
			IL_0053: ldelema [netstandard]System.Int32
			IL_0058: ldarg.3
			IL_0059: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
			IL_005e: ldarg.s endB
			IL_0060: ldc.i4.1
			IL_0061: sub
			IL_0062: ldelem.i4
			IL_0063: call instance bool [netstandard]System.Int32::Equals(int32)
			IL_0068: brtrue.s IL_0035
		// end loop

		IL_006a: ldarg.2
		IL_006b: ldarg.1
		IL_006c: sub
		IL_006d: stloc.0
		IL_006e: ldarg.s endB
		IL_0070: ldarg.s startB
		IL_0072: sub
		IL_0073: stloc.1
		IL_0074: ldloc.0
		IL_0075: ldc.i4.0
		IL_0076: ble IL_0198

		IL_007b: ldloc.1
		IL_007c: ldc.i4.0
		IL_007d: ble IL_0198

		IL_0082: ldarg.0
		IL_0083: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
		IL_0088: ldarg.1
		IL_0089: ldarg.2
		IL_008a: ldarg.3
		IL_008b: callvirt instance int32[] DiffPlex.Model.ModificationData::get_HashedPieces()
		IL_0090: ldarg.s startB
		IL_0092: ldarg.s endB
		IL_0094: ldarg.s forwardDiagonal
		IL_0096: ldarg.s reverseDiagonal
		IL_0098: call class DiffPlex.Model.EditLengthResult DiffPlex.Differ::CalculateEditLength(int32[], int32, int32, int32[], int32, int32, int32[], int32[])
		IL_009d: stloc.2
		IL_009e: ldloc.2
		IL_009f: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EditLength()
		IL_00a4: ldc.i4.0
		IL_00a5: bgt.s IL_00a8

		IL_00a7: ret

		IL_00a8: ldloc.2
		IL_00a9: callvirt instance valuetype DiffPlex.Model.Edit DiffPlex.Model.EditLengthResult::get_LastEdit()
		IL_00ae: ldc.i4.1
		IL_00af: bne.un.s IL_00da

		IL_00b1: ldloc.2
		IL_00b2: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartX()
		IL_00b7: ldc.i4.1
		IL_00b8: sub
		IL_00b9: ldarg.1
		IL_00ba: ble.s IL_00da

		IL_00bc: ldarg.0
		IL_00bd: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
		IL_00c2: ldloc.2
		IL_00c3: dup
		IL_00c4: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartX()
		IL_00c9: ldc.i4.1
		IL_00ca: sub
		IL_00cb: stloc.3
		IL_00cc: ldloc.3
		IL_00cd: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartX(int32)
		IL_00d2: ldloc.3
		IL_00d3: ldc.i4.1
		IL_00d4: stelem.i1
		IL_00d5: br IL_0163

		IL_00da: ldloc.2
		IL_00db: callvirt instance valuetype DiffPlex.Model.Edit DiffPlex.Model.EditLengthResult::get_LastEdit()
		IL_00e0: ldc.i4.3
		IL_00e1: bne.un.s IL_010a

		IL_00e3: ldloc.2
		IL_00e4: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartY()
		IL_00e9: ldc.i4.1
		IL_00ea: sub
		IL_00eb: ldarg.s startB
		IL_00ed: ble.s IL_010a

		IL_00ef: ldarg.3
		IL_00f0: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
		IL_00f5: ldloc.2
		IL_00f6: dup
		IL_00f7: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartY()
		IL_00fc: ldc.i4.1
		IL_00fd: sub
		IL_00fe: stloc.3
		IL_00ff: ldloc.3
		IL_0100: callvirt instance void DiffPlex.Model.EditLengthResult::set_StartY(int32)
		IL_0105: ldloc.3
		IL_0106: ldc.i4.1
		IL_0107: stelem.i1
		IL_0108: br.s IL_0163

		IL_010a: ldloc.2
		IL_010b: callvirt instance valuetype DiffPlex.Model.Edit DiffPlex.Model.EditLengthResult::get_LastEdit()
		IL_0110: ldc.i4.2
		IL_0111: bne.un.s IL_0137

		IL_0113: ldloc.2
		IL_0114: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndX()
		IL_0119: ldarg.2
		IL_011a: bge.s IL_0137

		IL_011c: ldarg.0
		IL_011d: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
		IL_0122: ldloc.2
		IL_0123: dup
		IL_0124: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndX()
		IL_0129: stloc.3
		IL_012a: ldloc.3
		IL_012b: ldc.i4.1
		IL_012c: add
		IL_012d: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndX(int32)
		IL_0132: ldloc.3
		IL_0133: ldc.i4.1
		IL_0134: stelem.i1
		IL_0135: br.s IL_0163

		IL_0137: ldloc.2
		IL_0138: callvirt instance valuetype DiffPlex.Model.Edit DiffPlex.Model.EditLengthResult::get_LastEdit()
		IL_013d: ldc.i4.4
		IL_013e: bne.un.s IL_0163

		IL_0140: ldloc.2
		IL_0141: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndY()
		IL_0146: ldarg.s endB
		IL_0148: bge.s IL_0163

		IL_014a: ldarg.3
		IL_014b: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
		IL_0150: ldloc.2
		IL_0151: dup
		IL_0152: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndY()
		IL_0157: stloc.3
		IL_0158: ldloc.3
		IL_0159: ldc.i4.1
		IL_015a: add
		IL_015b: callvirt instance void DiffPlex.Model.EditLengthResult::set_EndY(int32)
		IL_0160: ldloc.3
		IL_0161: ldc.i4.1
		IL_0162: stelem.i1

		IL_0163: ldarg.0
		IL_0164: ldarg.1
		IL_0165: ldloc.2
		IL_0166: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartX()
		IL_016b: ldarg.3
		IL_016c: ldarg.s startB
		IL_016e: ldloc.2
		IL_016f: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_StartY()
		IL_0174: ldarg.s forwardDiagonal
		IL_0176: ldarg.s reverseDiagonal
		IL_0178: call void DiffPlex.Differ::BuildModificationData(class DiffPlex.Model.ModificationData, int32, int32, class DiffPlex.Model.ModificationData, int32, int32, int32[], int32[])
		IL_017d: ldarg.0
		IL_017e: ldloc.2
		IL_017f: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndX()
		IL_0184: ldarg.2
		IL_0185: ldarg.3
		IL_0186: ldloc.2
		IL_0187: callvirt instance int32 DiffPlex.Model.EditLengthResult::get_EndY()
		IL_018c: ldarg.s endB
		IL_018e: ldarg.s forwardDiagonal
		IL_0190: ldarg.s reverseDiagonal
		IL_0192: call void DiffPlex.Differ::BuildModificationData(class DiffPlex.Model.ModificationData, int32, int32, class DiffPlex.Model.ModificationData, int32, int32, int32[], int32[])
		IL_0197: ret

		IL_0198: ldloc.0
		IL_0199: ldc.i4.0
		IL_019a: ble.s IL_01b7

		IL_019c: ldarg.1
		IL_019d: stloc.s 4
		IL_019f: br.s IL_01b1
		// loop start (head: IL_01b1)
			IL_01a1: ldarg.0
			IL_01a2: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
			IL_01a7: ldloc.s 4
			IL_01a9: ldc.i4.1
			IL_01aa: stelem.i1
			IL_01ab: ldloc.s 4
			IL_01ad: ldc.i4.1
			IL_01ae: add
			IL_01af: stloc.s 4

			IL_01b1: ldloc.s 4
			IL_01b3: ldarg.2
			IL_01b4: blt.s IL_01a1
		// end loop

		IL_01b6: ret

		IL_01b7: ldloc.1
		IL_01b8: ldc.i4.0
		IL_01b9: ble.s IL_01d7

		IL_01bb: ldarg.s startB
		IL_01bd: stloc.s 5
		IL_01bf: br.s IL_01d1
		// loop start (head: IL_01d1)
			IL_01c1: ldarg.3
			IL_01c2: callvirt instance bool[] DiffPlex.Model.ModificationData::get_Modifications()
			IL_01c7: ldloc.s 5
			IL_01c9: ldc.i4.1
			IL_01ca: stelem.i1
			IL_01cb: ldloc.s 5
			IL_01cd: ldc.i4.1
			IL_01ce: add
			IL_01cf: stloc.s 5

			IL_01d1: ldloc.s 5
			IL_01d3: ldarg.s endB
			IL_01d5: blt.s IL_01c1
		// end loop

		IL_01d7: ret
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

// Original disassembly lines 36160-36203
	.method private hidebysig
		instance string CopySubstring (
			int32 startIndex,
			int32 length
		) cil managed
	{
		// Method begins at RVA 0xd838
		// Header size: 12
		// Code size: 38 (0x26)
		.maxstack 5
		.locals init (
			[0] int32[],
			[1] int32
		)

		IL_0000: ldarg.2
		IL_0001: newarr System.Int32
		IL_0006: stloc.0
		IL_0007: ldc.i4.0
		IL_0008: stloc.1
		IL_0009: br.s IL_001b
		// loop start (head: IL_001b)
			IL_000b: ldloc.0
			IL_000c: ldloc.1
			IL_000d: ldarg.0
			IL_000e: ldarg.1
			IL_000f: ldloc.1
			IL_0010: add
			IL_0011: call instance char System.String::get_Chars(int32)
			IL_0016: stelem.i4
			IL_0017: ldloc.1
			IL_0018: ldc.i4.1
			IL_0019: add
			IL_001a: stloc.1

			IL_001b: ldloc.1
			IL_001c: ldarg.2
			IL_001d: blt.s IL_000b
		// end loop

		IL_001f: ldloc.0
		IL_0020: call string Cilica.Runtime.StringConstruction::FromCodeUnits(int32[])
		IL_0025: ret
	} // end of method String::CopySubstring

// Original disassembly lines 36793-36980
	.method public hidebysig
		instance string[] Split (
			char separator,
			valuetype System.StringSplitOptions options
		) cil managed
	{
		// Method begins at RVA 0xdbbc
		// Header size: 12
		// Code size: 244 (0xf4)
		.maxstack 7
		.locals init (
			[0] int32,
			[1] int32,
			[2] int32,
			[3] int32,
			[4] int32,
			[5] string[],
			[6] int32,
			[7] int32,
			[8] int32
		)

		IL_0000: ldarg.2
		IL_0001: stloc.0
		IL_0002: ldloc.0
		IL_0003: ldc.i4.s -4
		IL_0005: and
		IL_0006: brfalse.s IL_0018

		IL_0008: ldstr "Value of flags is invalid."
		IL_000d: ldstr "options"
		IL_0012: newobj instance void System.ArgumentException::.ctor(string, string)
		IL_0017: throw

		IL_0018: ldarg.0
		IL_0019: call instance int32 System.String::get_Length()
		IL_001e: stloc.1
		IL_001f: ldc.i4.0
		IL_0020: stloc.2
		IL_0021: ldc.i4.0
		IL_0022: stloc.3
		IL_0023: ldc.i4.0
		IL_0024: stloc.s 4
		IL_0026: ldc.i4.0
		IL_0027: stloc.s 7
		IL_0029: br.s IL_0057
		// loop start (head: IL_0057)
			IL_002b: ldarg.0
			IL_002c: ldloc.s 7
			IL_002e: call instance char System.String::get_Chars(int32)
			IL_0033: ldarg.1
			IL_0034: bne.un.s IL_0051

			IL_0036: ldloc.s 4
			IL_0038: ldc.i4.1
			IL_0039: add
			IL_003a: stloc.s 4
			IL_003c: ldarg.0
			IL_003d: ldloc.2
			IL_003e: ldloc.s 7
			IL_0040: ldloc.0
			IL_0041: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
			IL_0046: brfalse.s IL_004c

			IL_0048: ldloc.3
			IL_0049: ldc.i4.1
			IL_004a: add
			IL_004b: stloc.3

			IL_004c: ldloc.s 7
			IL_004e: ldc.i4.1
			IL_004f: add
			IL_0050: stloc.2

			IL_0051: ldloc.s 7
			IL_0053: ldc.i4.1
			IL_0054: add
			IL_0055: stloc.s 7

			IL_0057: ldloc.s 7
			IL_0059: ldloc.1
			IL_005a: blt.s IL_002b
		// end loop

		IL_005c: ldarg.0
		IL_005d: ldloc.2
		IL_005e: ldloc.1
		IL_005f: ldloc.0
		IL_0060: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
		IL_0065: brfalse.s IL_006b

		IL_0067: ldloc.3
		IL_0068: ldc.i4.1
		IL_0069: add
		IL_006a: stloc.3

		IL_006b: ldloc.s 4
		IL_006d: brtrue.s IL_008b

		IL_006f: ldloc.3
		IL_0070: brtrue.s IL_0078

		IL_0072: call !!0[] System.Array::Empty<string>()
		IL_0077: ret

		IL_0078: ldc.i4.1
		IL_0079: newarr System.String
		IL_007e: dup
		IL_007f: ldc.i4.0
		IL_0080: ldarg.0
		IL_0081: ldc.i4.0
		IL_0082: ldloc.1
		IL_0083: ldloc.0
		IL_0084: call instance string System.String::SplitEntry(int32, int32, int32)
		IL_0089: stelem.ref
		IL_008a: ret

		IL_008b: ldloc.3
		IL_008c: newarr System.String
		IL_0091: stloc.s 5
		IL_0093: ldc.i4.0
		IL_0094: stloc.2
		IL_0095: ldc.i4.0
		IL_0096: stloc.s 6
		IL_0098: ldc.i4.0
		IL_0099: stloc.s 8
		IL_009b: br.s IL_00d3
		// loop start (head: IL_00d3)
			IL_009d: ldarg.0
			IL_009e: ldloc.s 8
			IL_00a0: call instance char System.String::get_Chars(int32)
			IL_00a5: ldarg.1
			IL_00a6: bne.un.s IL_00cd

			IL_00a8: ldarg.0
			IL_00a9: ldloc.2
			IL_00aa: ldloc.s 8
			IL_00ac: ldloc.0
			IL_00ad: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
			IL_00b2: brfalse.s IL_00c8

			IL_00b4: ldloc.s 5
			IL_00b6: ldloc.s 6
			IL_00b8: dup
			IL_00b9: ldc.i4.1
			IL_00ba: add
			IL_00bb: stloc.s 6
			IL_00bd: ldarg.0
			IL_00be: ldloc.2
			IL_00bf: ldloc.s 8
			IL_00c1: ldloc.0
			IL_00c2: call instance string System.String::SplitEntry(int32, int32, int32)
			IL_00c7: stelem.ref

			IL_00c8: ldloc.s 8
			IL_00ca: ldc.i4.1
			IL_00cb: add
			IL_00cc: stloc.2

			IL_00cd: ldloc.s 8
			IL_00cf: ldc.i4.1
			IL_00d0: add
			IL_00d1: stloc.s 8

			IL_00d3: ldloc.s 8
			IL_00d5: ldloc.1
			IL_00d6: blt.s IL_009d
		// end loop

		IL_00d8: ldarg.0
		IL_00d9: ldloc.2
		IL_00da: ldloc.1
		IL_00db: ldloc.0
		IL_00dc: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
		IL_00e1: brfalse.s IL_00f1

		IL_00e3: ldloc.s 5
		IL_00e5: ldloc.s 6
		IL_00e7: ldarg.0
		IL_00e8: ldloc.2
		IL_00e9: ldloc.1
		IL_00ea: ldloc.0
		IL_00eb: call instance string System.String::SplitEntry(int32, int32, int32)
		IL_00f0: stelem.ref

		IL_00f1: ldloc.s 5
		IL_00f3: ret
	} // end of method String::Split

// Original disassembly lines 36982-37281
	.method public hidebysig
		instance string[] Split (
			string[] separator,
			valuetype System.StringSplitOptions options
		) cil managed
	{
		// Method begins at RVA 0xdcbc
		// Header size: 12
		// Code size: 400 (0x190)
		.maxstack 7
		.locals init (
			[0] int32,
			[1] int32,
			[2] int32,
			[3] int32,
			[4] int32,
			[5] int32[],
			[6] int32,
			[7] string[],
			[8] int32,
			[9] int32,
			[10] int32,
			[11] int32,
			[12] int32,
			[13] int32,
			[14] int32
		)

		IL_0000: ldarg.2
		IL_0001: stloc.0
		IL_0002: ldloc.0
		IL_0003: ldc.i4.s -4
		IL_0005: and
		IL_0006: brfalse.s IL_0018

		IL_0008: ldstr "Value of flags is invalid."
		IL_000d: ldstr "options"
		IL_0012: newobj instance void System.ArgumentException::.ctor(string, string)
		IL_0017: throw

		IL_0018: ldarg.0
		IL_0019: call instance int32 System.String::get_Length()
		IL_001e: stloc.1
		IL_001f: ldc.i4.0
		IL_0020: stloc.2
		IL_0021: ldc.i4.0
		IL_0022: stloc.3
		IL_0023: ldc.i4.0
		IL_0024: stloc.s 4
		IL_0026: ldnull
		IL_0027: stloc.s 5
		IL_0029: ldc.i4.0
		IL_002a: stloc.s 6
		IL_002c: ldc.i4.0
		IL_002d: stloc.s 9
		IL_002f: br.s IL_009e
		// loop start (head: IL_009e)
			IL_0031: ldarg.0
			IL_0032: ldarg.1
			IL_0033: ldloc.s 9
			IL_0035: call instance int32 System.String::MatchStringSeparator(string[], int32)
			IL_003a: stloc.s 10
			IL_003c: ldloc.s 10
			IL_003e: brtrue.s IL_0048

			IL_0040: ldloc.s 9
			IL_0042: ldc.i4.1
			IL_0043: add
			IL_0044: stloc.s 9
			IL_0046: br.s IL_009e

			IL_0048: ldloc.s 4
			IL_004a: ldc.i4.1
			IL_004b: add
			IL_004c: stloc.s 4
			IL_004e: ldloc.s 5
			IL_0050: brtrue.s IL_0065

			IL_0052: ldloc.1
			IL_0053: ldc.i4.s 64
			IL_0055: blt.s IL_005b

			IL_0057: ldc.i4.s 64
			IL_0059: br.s IL_005c

			IL_005b: ldloc.1

			IL_005c: ldc.i4.2
			IL_005d: mul
			IL_005e: newarr System.Int32
			IL_0063: stloc.s 5

			IL_0065: ldloc.s 6
			IL_0067: ldloc.s 5
			IL_0069: ldlen
			IL_006a: conv.i4
			IL_006b: bge.s IL_0085

			IL_006d: ldloc.s 5
			IL_006f: ldloc.s 6
			IL_0071: dup
			IL_0072: ldc.i4.1
			IL_0073: add
			IL_0074: stloc.s 6
			IL_0076: ldloc.s 9
			IL_0078: stelem.i4
			IL_0079: ldloc.s 5
			IL_007b: ldloc.s 6
			IL_007d: dup
			IL_007e: ldc.i4.1
			IL_007f: add
			IL_0080: stloc.s 6
			IL_0082: ldloc.s 10
			IL_0084: stelem.i4

			IL_0085: ldarg.0
			IL_0086: ldloc.2
			IL_0087: ldloc.s 9
			IL_0089: ldloc.0
			IL_008a: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
			IL_008f: brfalse.s IL_0095

			IL_0091: ldloc.3
			IL_0092: ldc.i4.1
			IL_0093: add
			IL_0094: stloc.3

			IL_0095: ldloc.s 9
			IL_0097: ldloc.s 10
			IL_0099: add
			IL_009a: stloc.2
			IL_009b: ldloc.2
			IL_009c: stloc.s 9

			IL_009e: ldloc.s 9
			IL_00a0: ldloc.1
			IL_00a1: blt.s IL_0031
		// end loop

		IL_00a3: ldarg.0
		IL_00a4: ldloc.2
		IL_00a5: ldloc.1
		IL_00a6: ldloc.0
		IL_00a7: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
		IL_00ac: brfalse.s IL_00b2

		IL_00ae: ldloc.3
		IL_00af: ldc.i4.1
		IL_00b0: add
		IL_00b1: stloc.3

		IL_00b2: ldloc.s 4
		IL_00b4: brtrue.s IL_00d2

		IL_00b6: ldloc.3
		IL_00b7: brtrue.s IL_00bf

		IL_00b9: call !!0[] System.Array::Empty<string>()
		IL_00be: ret

		IL_00bf: ldc.i4.1
		IL_00c0: newarr System.String
		IL_00c5: dup
		IL_00c6: ldc.i4.0
		IL_00c7: ldarg.0
		IL_00c8: ldc.i4.0
		IL_00c9: ldloc.1
		IL_00ca: ldloc.0
		IL_00cb: call instance string System.String::SplitEntry(int32, int32, int32)
		IL_00d0: stelem.ref
		IL_00d1: ret

		IL_00d2: ldloc.3
		IL_00d3: newarr System.String
		IL_00d8: stloc.s 7
		IL_00da: ldc.i4.0
		IL_00db: stloc.2
		IL_00dc: ldc.i4.0
		IL_00dd: stloc.s 8
		IL_00df: ldc.i4.0
		IL_00e0: stloc.s 11
		IL_00e2: br.s IL_011c
		// loop start (head: IL_011c)
			IL_00e4: ldloc.s 5
			IL_00e6: ldloc.s 11
			IL_00e8: ldelem.i4
			IL_00e9: stloc.s 12
			IL_00eb: ldarg.0
			IL_00ec: ldloc.2
			IL_00ed: ldloc.s 12
			IL_00ef: ldloc.0
			IL_00f0: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
			IL_00f5: brfalse.s IL_010b

			IL_00f7: ldloc.s 7
			IL_00f9: ldloc.s 8
			IL_00fb: dup
			IL_00fc: ldc.i4.1
			IL_00fd: add
			IL_00fe: stloc.s 8
			IL_0100: ldarg.0
			IL_0101: ldloc.2
			IL_0102: ldloc.s 12
			IL_0104: ldloc.0
			IL_0105: call instance string System.String::SplitEntry(int32, int32, int32)
			IL_010a: stelem.ref

			IL_010b: ldloc.s 12
			IL_010d: ldloc.s 5
			IL_010f: ldloc.s 11
			IL_0111: ldc.i4.1
			IL_0112: add
			IL_0113: ldelem.i4
			IL_0114: add
			IL_0115: stloc.2
			IL_0116: ldloc.s 11
			IL_0118: ldc.i4.2
			IL_0119: add
			IL_011a: stloc.s 11

			IL_011c: ldloc.s 11
			IL_011e: ldloc.s 6
			IL_0120: blt.s IL_00e4
		// end loop

		IL_0122: ldloc.s 4
		IL_0124: ldloc.s 6
		IL_0126: ldc.i4.2
		IL_0127: div
		IL_0128: ble.s IL_0174

		IL_012a: ldloc.2
		IL_012b: stloc.s 13
		IL_012d: br.s IL_016f
		// loop start (head: IL_016f)
			IL_012f: ldarg.0
			IL_0130: ldarg.1
			IL_0131: ldloc.s 13
			IL_0133: call instance int32 System.String::MatchStringSeparator(string[], int32)
			IL_0138: stloc.s 14
			IL_013a: ldloc.s 14
			IL_013c: brtrue.s IL_0146

			IL_013e: ldloc.s 13
			IL_0140: ldc.i4.1
			IL_0141: add
			IL_0142: stloc.s 13
			IL_0144: br.s IL_016f

			IL_0146: ldarg.0
			IL_0147: ldloc.2
			IL_0148: ldloc.s 13
			IL_014a: ldloc.0
			IL_014b: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
			IL_0150: brfalse.s IL_0166

			IL_0152: ldloc.s 7
			IL_0154: ldloc.s 8
			IL_0156: dup
			IL_0157: ldc.i4.1
			IL_0158: add
			IL_0159: stloc.s 8
			IL_015b: ldarg.0
			IL_015c: ldloc.2
			IL_015d: ldloc.s 13
			IL_015f: ldloc.0
			IL_0160: call instance string System.String::SplitEntry(int32, int32, int32)
			IL_0165: stelem.ref

			IL_0166: ldloc.s 13
			IL_0168: ldloc.s 14
			IL_016a: add
			IL_016b: stloc.2
			IL_016c: ldloc.2
			IL_016d: stloc.s 13

			IL_016f: ldloc.s 13
			IL_0171: ldloc.1
			IL_0172: blt.s IL_012f
		// end loop

		IL_0174: ldarg.0
		IL_0175: ldloc.2
		IL_0176: ldloc.1
		IL_0177: ldloc.0
		IL_0178: call instance bool System.String::SplitEntryPresent(int32, int32, int32)
		IL_017d: brfalse.s IL_018d

		IL_017f: ldloc.s 7
		IL_0181: ldloc.s 8
		IL_0183: ldarg.0
		IL_0184: ldloc.2
		IL_0185: ldloc.1
		IL_0186: ldloc.0
		IL_0187: call instance string System.String::SplitEntry(int32, int32, int32)
		IL_018c: stelem.ref

		IL_018d: ldloc.s 7
		IL_018f: ret
	} // end of method String::Split

// Original disassembly lines 37283-37391
	.method private hidebysig
		instance int32 MatchStringSeparator (
			string[] separator,
			int32 index
		) cil managed
	{
		// Method begins at RVA 0xde58
		// Header size: 12
		// Code size: 130 (0x82)
		.maxstack 3
		.locals init (
			[0] int32,
			[1] int32,
			[2] string,
			[3] int32
		)

		IL_0000: ldarg.1
		IL_0001: brtrue.s IL_0013

		IL_0003: ldarg.0
		IL_0004: ldarg.2
		IL_0005: call instance char System.String::get_Chars(int32)
		IL_000a: call bool Cilica.Runtime.UnicodeWhitespace::Contains(int32)
		IL_000f: ldc.i4.0
		IL_0010: cgt.un
		IL_0012: ret

		IL_0013: ldarg.1
		IL_0014: ldlen
		IL_0015: conv.i4
		IL_0016: stloc.0
		IL_0017: ldloc.0
		IL_0018: brtrue.s IL_002a

		IL_001a: ldarg.0
		IL_001b: ldarg.2
		IL_001c: call instance char System.String::get_Chars(int32)
		IL_0021: call bool Cilica.Runtime.UnicodeWhitespace::Contains(int32)
		IL_0026: ldc.i4.0
		IL_0027: cgt.un
		IL_0029: ret

		IL_002a: ldc.i4.0
		IL_002b: stloc.1
		IL_002c: br.s IL_007c
		// loop start (head: IL_007c)
			IL_002e: ldarg.1
			IL_002f: ldloc.1
			IL_0030: ldelem.ref
			IL_0031: stloc.2
			IL_0032: ldloc.2
			IL_0033: call bool System.String::IsNullOrEmpty(string)
			IL_0038: brtrue.s IL_0078

			IL_003a: ldloc.2
			IL_003b: callvirt instance int32 System.String::get_Length()
			IL_0040: ldarg.0
			IL_0041: call instance int32 System.String::get_Length()
			IL_0046: ldarg.2
			IL_0047: sub
			IL_0048: bgt.s IL_0078

			IL_004a: ldc.i4.0
			IL_004b: stloc.3
			IL_004c: br.s IL_0052
			// loop start (head: IL_0052)
				IL_004e: ldloc.3
				IL_004f: ldc.i4.1
				IL_0050: add
				IL_0051: stloc.3

				IL_0052: ldloc.3
				IL_0053: ldloc.2
				IL_0054: callvirt instance int32 System.String::get_Length()
				IL_0059: bge.s IL_006d

				IL_005b: ldarg.0
				IL_005c: ldarg.2
				IL_005d: ldloc.3
				IL_005e: add
				IL_005f: call instance char System.String::get_Chars(int32)
				IL_0064: ldloc.2
				IL_0065: ldloc.3
				IL_0066: callvirt instance char System.String::get_Chars(int32)
				IL_006b: beq.s IL_004e
			// end loop

			IL_006d: ldloc.3
			IL_006e: ldloc.2
			IL_006f: callvirt instance int32 System.String::get_Length()
			IL_0074: bne.un.s IL_0078

			IL_0076: ldloc.3
			IL_0077: ret

			IL_0078: ldloc.1
			IL_0079: ldc.i4.1
			IL_007a: add
			IL_007b: stloc.1

			IL_007c: ldloc.1
			IL_007d: ldloc.0
			IL_007e: blt.s IL_002e
		// end loop

		IL_0080: ldc.i4.0
		IL_0081: ret
	} // end of method String::MatchStringSeparator

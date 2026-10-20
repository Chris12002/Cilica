	.method private hidebysig
		instance class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue> Find (
			!TKey key,
			[out] int32& found
		) cil managed
	{
		// Method begins at RVA 0x10a68
		// Header size: 12
		// Code size: 139 (0x8b)
		.maxstack 3
		.locals init (
			[0] uint32,
			[1] int32,
			[2] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>,
			[3] int32
		)

		IL_0000: ldarg.2
		IL_0001: ldc.i4.m1
		IL_0002: stind.i4
		IL_0003: ldarg.1
		IL_0004: call void class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::CheckKey(!0)
		IL_0009: ldarg.0
		IL_000a: ldfld int32[] class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::buckets
		IL_000f: brtrue.s IL_0013

		IL_0011: ldnull
		IL_0012: ret

		IL_0013: ldarg.0
		IL_0014: ldarg.1
		IL_0015: call instance uint32 class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::Hash(!0)
		IL_001a: stloc.0
		IL_001b: ldarg.0
		IL_001c: ldfld int32[] class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::buckets
		IL_0021: ldloc.0
		IL_0022: ldarg.0
		IL_0023: ldfld int32[] class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::buckets
		IL_0028: ldlen
		IL_0029: conv.i4
		IL_002a: rem.un
		IL_002b: ldelem.i4
		IL_002c: ldc.i4.1
		IL_002d: sub
		IL_002e: stloc.1
		IL_002f: ldarg.0
		IL_0030: ldfld class Cilica.Runtime.DictionaryEntries`2<!0, !1> class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::entries
		IL_0035: stloc.2
		IL_0036: ldc.i4.0
		IL_0037: stloc.3
		IL_0038: br.s IL_0080
		// loop start (head: IL_0080)
			IL_003a: ldloc.2
			IL_003b: ldfld uint32[] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Hashes
			IL_0040: ldloc.1
			IL_0041: ldelem.u4
			IL_0042: ldloc.0
			IL_0043: bne.un.s IL_005f

			IL_0045: ldarg.0
			IL_0046: ldloc.2
			IL_0047: ldfld !0[] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Keys
			IL_004c: ldloc.1
			IL_004d: ldelem !TKey
			IL_0052: ldarg.1
			IL_0053: call instance bool class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::Equal(!0, !0)
			IL_0058: brfalse.s IL_005f

			IL_005a: ldarg.2
			IL_005b: ldloc.1
			IL_005c: stind.i4
			IL_005d: ldloc.2
			IL_005e: ret

			IL_005f: ldloc.2
			IL_0060: ldfld int32[] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Links
			IL_0065: ldloc.1
			IL_0066: ldelem.i4
			IL_0067: stloc.1
			IL_0068: ldloc.3
			IL_0069: ldc.i4.1
			IL_006a: add
			IL_006b: dup
			IL_006c: stloc.3
			IL_006d: ldloc.2
			IL_006e: ldfld int32 class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Length
			IL_0073: ble.s IL_0080

			IL_0075: ldstr "Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct."
			IL_007a: newobj instance void System.InvalidOperationException::.ctor(string)
			IL_007f: throw

			IL_0080: ldloc.1
			IL_0081: ldloc.2
			IL_0082: ldfld int32 class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Length
			IL_0087: blt.un.s IL_003a
		// end loop

		IL_0089: ldnull
		IL_008a: ret
	} // end of method Dictionary`2::Find

	.method public final hidebysig newslot virtual
		instance bool TryGetValue (
			!TKey key,
			[out] !TValue& 'value'
		) cil managed
	{
		// Method begins at RVA 0x10b00
		// Header size: 12
		// Code size: 42 (0x2a)
		.maxstack 3
		.locals init (
			[0] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>,
			[1] int32
		)

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: ldloca.s 1
		IL_0004: call instance class Cilica.Runtime.DictionaryEntries`2<!0, !1> class System.Collections.Generic.Dictionary`2<!TKey, !TValue>::Find(!0, int32&)
		IL_0009: stloc.0
		IL_000a: ldloc.0
		IL_000b: brfalse.s IL_0021

		IL_000d: ldarg.2
		IL_000e: ldloc.0
		IL_000f: ldfld !1[] class Cilica.Runtime.DictionaryEntries`2<!TKey, !TValue>::Values
		IL_0014: ldloc.1
		IL_0015: ldelem !TValue
		IL_001a: stobj !TValue
		IL_001f: ldc.i4.1
		IL_0020: ret

		IL_0021: ldarg.2
		IL_0022: initobj !TValue
		IL_0028: ldc.i4.0
		IL_0029: ret
	} // end of method Dictionary`2::TryGetValue

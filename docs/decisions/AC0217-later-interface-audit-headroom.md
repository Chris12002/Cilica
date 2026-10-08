# ADR AC0217: Later interface-body audit headroom

Recorded 2026-10-08 after AC8.72. This changes ignored diagnostic copies only.

The Stack-enabled snapshot first exhausts its 3,072 cached-type allowance. With
4,096 types it then needs 3 fields while only 2 remain of 6,144 (6,142 already charged).
Raise diagnostic fields to 8,192. The next scan exhausts 65,536 aggregate call arguments.
A crossing counter in the 131,072-argument copy records 65,539 at
RedirectResolutionSpellEdit.Commit IL_0124: 10,825 bodies, 20,576 parameter slots,
11,312 locals and 249,512 instructions. It then drains 11,330 bodies and completes
another interface-planning pass, which needs 13,778 combined identities against 12,288:
15 abstract declarations, 953 interface declarations, 5,030 mapping facts and 53 exception
declarations (these sets overlap). Explicit types 3,112; instructions 259,735;
parameters 21,668; locals 11,747. These are partial discovery counters, not emitted layouts.

Raise diagnostic methods to 16,384. To avoid repeatedly rebuilding near-full audits,
explicitly add proactive headroom for instructions 262,144→393,216, locals 12,288→16,384
and metadata 4,194,304→6,291,456. These three old limits were not exhausted in the last
run. Existing parameters 32,768 and fields 8,192 remain. All subprocesses retain the
60-second bound; per-method limits remain. Production types/methods/parameters/locals/
fields stay 256/512/4096/4096/4096; instructions, call arguments and metadata stay 65,536.
No compiler profile, owned core source, runtime, input or native executable changes.

| Diagnostic evidence | Engine charged work | Engine time | Next stop |
| --- | ---: | ---: | --- |
| alchemy-types-4096 | 2,674,945 | 10,261ms | field declarations |
| alchemy-fields-8192 | 2,745,261 | 10,413ms | call arguments |
| alchemy-call-arguments-131072 | 3,189,679 | 17,666ms | interface method identities |
| alchemy-interface-headroom | 3,223,372 | 17,386ms | Int32.Parse(String,IFormatProvider) |

The final diagnostic is CIL3010 at ActivationCountDamageOperation.Run IL_01a9,
method 0x06000a87/member0x0a00059d. Frozen source confirms
int.Parse(saved[0], CultureInfo.InvariantCulture); the current Int32 API has only
its explicit-style Parse overload. This is a semantic surface gap requiring a
separate implementation and exact provider/grant/exception tests, not more quotas.
StateLookup stays 15 methods/11,616 charged units (467/447/456/442ms across these runs).

All copies use the same reviewed 954fdbc+Stack snapshot, core
3bfd9b049a315bb035971b1a2250f0f10744a57c6f114662fb7876d9e219b600 and manifest
54a344e7c5d70aeff6c202c31b8a79841e98125809837fda3dc254f55da946c9.
The later character-trimming/array changes are not in these receipts. Six frozen
assembly hashes remain verified by AC8.72. Review confirms only diagnostic budget
constants/counters change; no tests mirror this documentation-only slice.

Correct the earlier emission interpretation: the refresh harness has an emitter branch,
but its production-limit runs reject both roots before that branch. The larger diagnostic
copies remove the emitter branch entirely, so successful StateLookup validation does not
emit C++. The saved rows confirm Emission=null. Neither root is emitted, compiled or
executed in these scans. The initial AC8.73 audit flag stateLookupCppEmission=true was
incorrect; AC8.74 corrects the receipts and notes. Full closure admission, native engine
execution, canonical fingerprints and saved-game replay remain open.

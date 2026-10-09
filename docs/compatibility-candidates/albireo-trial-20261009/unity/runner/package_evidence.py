"""Package retained results and source locations only; no candidate/build/player execution."""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
from build import ROOT, APP, digest, verify_inputs

PREFIX = Path('docs/compatibility-candidates/albireo-trial-20261009/unity')
OUT = ROOT/'integration'/PREFIX
CPP = ROOT/'capture/project/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp'

def put(name, value):
    p = OUT/name; p.parent.mkdir(parents=True, exist_ok=True)
    with p.open('x') as stream: json.dump(value, stream, indent=2, ensure_ascii=True); stream.write('\n')
    return ref(name)
def ref(name): return {'path':str(PREFIX/name), 'sha256':digest(OUT/name)}
def copy(source, name):
    p = OUT/name; p.parent.mkdir(parents=True, exist_ok=True); assert not p.exists()
    shutil.copyfile(ROOT/source, p); assert digest(p) == digest(ROOT/source)
    return ref(name)
def original(path): return {'path':str(path),'sha256':digest(path),'bytes':path.stat().st_size}

def main():
    h = verify_inputs(); OUT.mkdir(parents=True); fp = h['input_fingerprint']
    copies = {
       'handoff-verification.json':'handoff-verification.json', 'handoff/expected.json':'oracle-expected.json',
       'verify/result.json':'verify-result.json', 'verify/comparison.json':'verify-comparison.json',
       'verify/launch-receipt.json':'verify-launch.json', 'verify/player.log':'verify-player.log',
       'profile/result.json':'profile-result.json', 'profile/comparison.json':'profile-comparison.json',
       'profile/launch-receipt.json':'profile-launch.json', 'profile/player.log':'profile-player.log',
       'profile/profile.raw':'profile.raw', 'inspection/markers.json':'markers.json',
       'inspection/settings-readback.json':'settings-readback.json', 'inspection/result.json':'inspection-result.json',
       'inspection/inputs.json':'inspection-inputs.json', 'inspection/inspect.log.receipt.json':'inspection-command.json',
       'build/build-provenance.json':'build-provenance.json', 'build/unity-build.log.receipt.json':'build-command.json',
       'build/create.log.receipt.json':'project-create-command.json', 'build/manifest-restoration.json':'manifest-restoration.json',
       'capture/manifest.json':'capture-manifest.json', 'casing-metadata/result.json':'casing-table-data.json',
       'readiness/results.json':'readiness.json',
       'projects/CilicaAlbireo/Packages/packages-lock.json':'packages-lock.json',
       'projects/CilicaAlbireo/Packages/manifest.json':'packages-manifest.json'}
    for source, name in copies.items(): copy(source, name)
    # Owned postprocessors are retained for review, never added to the frozen player.
    for name in ['verify_handoff.py','build.py','capture.py','player.py','inspect.py',
                 'ProfileInspection.cs','SettingsReadback.cs','InspectCasingData.cs','package_evidence.py']:
        copy(name,'runner/'+name)
    spec = importlib.util.spec_from_file_location('transcript',ROOT/'frozen/tools/CompatibilityCandidates/albireo-unity/transcript.py')
    transcript = importlib.util.module_from_spec(spec); spec.loader.exec_module(transcript)
    expected = json.loads((OUT/'oracle-expected.json').read_text())
    verify = json.loads((OUT/'verify-result.json').read_text()); profile = json.loads((OUT/'profile-result.json').read_text())
    def normalized(value):
        v = value['values']; return transcript.normalize([v[i:i+3] for i in range(0,len(v),3)],fp)
    unity_expected = normalized(verify)
    assert unity_expected['checksum'] == verify['checksum'] and normalized(profile) == unity_expected
    assert transcript.compare(unity_expected,profile) == 857
    def cases(value):
        result = {}
        for i in range(0,len(value['values']),3):
            s,index,v = value['values'][i:i+3]; result.setdefault(s,{})[index] = v
        return result
    e, u = cases(expected), cases(verify)
    matches = [s for s in range(32) if e[s] == u[s]]
    assert matches == [s for s in range(32) if s not in (0,17)]
    def describe(values):
        length = max(0,values[1]); message_start = 6+length
        return {'transcript_length':values[-1], 'status':values[0], 'byte_length':values[1],
          'reference_identity':values[2], 'output_bytes':[values[i] for i in range(6,6+length)],
          'message':''.join(chr(values[i]) for i in range(message_start,message_start+values[3])),
          'parameter':''.join(chr(values[i]) for i in range(message_start+values[3],message_start+values[3]+values[4]))}
    differences = {'input_fingerprint':fp,'status':'failed','well_formed_transcripts':True,
      'oracle_observations':851,'unity_observations':857,'matching_cases':matches,'matching_observations':790,
      'differences':[{'scenario':s,'oracle':describe(e[s]),'unity':describe(u[s])} for s in [0,17]],
      'qualification':'Same pinned input DLL and workload; two actual semantic differences. No normalization or revised oracle. Full Unity equivalence failed.'}
    put('differences.json',differences)
    assert all(e[s] == u[s] for s in [2,8,29,31])
    logs = [(ROOT/'profile/player.log'),(ROOT/'inspection/inspect.log')]
    warning_hits = []
    for log in logs:
        for i,line in enumerate(log.read_text(errors='replace').splitlines(),1):
            if re.search(r'(profil.{0,60}(skip|paus|overflow|buffer.full))|((skip|paus|overflow).{0,60}profil)',line,re.I):
                warning_hits.append({'path':str(log),'line':i,'text':line[:500]})
    put('profile-validation.json',{'input_fingerprint':fp,'kind':'diagnostic','status':'recorded',
      'selected_cases':[2,8,29,31],'selected_cases_match_oracle':True,'full_transcript_matches_verify_run':True,
      'full_dotnet_equivalence':False,'iterations':128,'warm_iterations':8,'marker_batches':3,
      'wrapper_invocations_per_marker':512,'decode_calls_per_marker':1024,'raw_bytes':(OUT/'profile.raw').stat().st_size,
      'raw_closed_below_64MiB':True,'profiler_buffer_bytes':33554432,'warning_scan_hits':warning_hits,
      'qualification':'Development IL2CPP mixed wrapper batches include source construction and two Decode calls per Probe.Run. Process architecture not independently recorded. Host not reserved quiet. Allocation samples are cumulative GC.Alloc under each marker, not live heap, RSS, native memory or execution peak. No competitive or architecture-specific timing claim.'})
    # Preserve both actual compiler branches; API architecture=1 was not ARM64-only.
    dag = ROOT/'capture/project/Library/Bee/Player75a3e897.dag.json'; commands=[]
    for index,node in enumerate(json.loads(dag.read_text())['Nodes']):
        action=node.get('Action','')
        if isinstance(action,str) and 'Albireo.SelectedDecode.cpp' in action:
            commands.append({'dag_node_index':index,'action':action})
    assert len(commands)==2 and all('-O3' in c['action'] for c in commands)
    assert any('-arch arm64' in c['action'] for c in commands) and any('-arch x86_64' in c['action'] for c in commands)
    native=json.loads((OUT/'build-provenance.json').read_text())['native_sha256']
    # Parse fat Mach-O headers directly as data to verify every retained binary.
    architecture_rows=[]
    for name,sha in native.items():
        path=APP/name; assert digest(path)==sha
        data=path.read_bytes(); assert data[:4]==bytes.fromhex('cafebabe')
        count=int.from_bytes(data[4:8],'big'); types=[int.from_bytes(data[8+20*i:12+20*i],'big') for i in range(count)]
        assert sorted(types)==[0x01000007,0x0100000c]
        architecture_rows.append({'path':str(path),'sha256':sha,'slices':['x86_64','arm64']})
    put('actual-architecture-and-commands.json',{'input_fingerprint':fp,'dag':original(dag),'candidate_compile_commands':commands,
      'native_files':architecture_rows,'actual_artifact':'universal x86_64+arm64','process_architecture':'not independently recorded',
      'planned_vs_observed':'Frozen plan called this ARM64; unchanged builder SetArchitecture(Standalone,1) actually emits universal. Readback1 alone is not ARM64-only proof. No rebuild or template change.'})
    sources=[]
    def finding(key,path,start,end,symbol,text,decision):
        lines=path.read_text().splitlines(); assert 0<start<=end<=len(lines)
        evidence={'source':original(path),'line_start':start,'line_end':end,'symbol':symbol,
          'finding':text,'decision':decision,'excerpt':'\n'.join(lines[start-1:end])}
        evidence_ref=put('source-evidence/'+key+'.json',evidence)
        sources.append({'evidence':evidence_ref,'symbol':symbol,'finding':text,'gap_ids':['decode-closure-validation'],
                        'original':original(path),'line_start':start,'line_end':end})
    finding('decode-prefix',CPP/'Albireo.SelectedDecode.cpp',803,850,'Base32.Decode',
       'Null is tested before empty, trimming, invariant uppercase and char-array copying. The readonly string input lowers to String_t** and is dereferenced before these calls.',
       'Retain exact fault order and readonly reference semantics; unchanged candidate and owned32-case adapter exercised these paths.')
    finding('cached-predicate',CPP/'Albireo.SelectedDecode.cpp',850,905,'Base32.Decode predicate/cache/allocation',
       'The predicate delegate is cached with a publication barrier; Any validates the char array before checked output-size arithmetic and byte-array allocation.',
       'Do not substitute publication barriers for Cilica instance ownership or root validation. No cache/runtime change proposed.')
    finding('any-dispatch',CPP/'GenericMethods__4.cpp',20998,21095,'Enumerable.Any<Char>',
       'This Unity Any path uses GetEnumerator, Current, MoveNext and finally Dispose interface calls plus the predicate delegate.',
       'Cilica already has a managed array path; this is a structural comparison, not a speed ratio.')
    finding('char-enumerator',CPP/'GenericMethods__2.cpp',12517,12549,'Array.InternalArray__IEnumerable_GetEnumerator<Char>',
       'Nonempty char arrays construct and box an InternalEnumerator; the empty path reuses a cached enumerator.',
       'Compare against owned array/enumeration regressions; exact selected candidate has already passed .NET/Cilica.')
    finding('uppercase-call',CPP/'mscorlib.cpp',28441,28463,'String.ToUpperInvariant',
       'String casing obtains invariant CultureInfo/TextInfo and dispatches the string uppercase method.',
       'Follow the actual invariant path rather than assuming all runtime casing tables agree.')
    finding('uppercase-string-allocation',CPP/'mscorlib__15.cpp',8510,8526,'TextInfo.ToUpperInternal(String)',
       'Nonempty strings allocate a new string before code-unit conversion; the generated implementation later dispatches the per-character uppercase operation.',
       'Cilica preserves its pinned .NET unchanged-ASCII identity fast path; no Unity behavior copied.')
    finding('invariant-branch',CPP/'mscorlib__15.cpp',8728,8740,'TextInfo.ToUpperInternal(Char) invariant branch',
       'Invariant culture jumps over the explicit noninvariant long-s special case to IL_017c.',
       'Together with emitted table data this explains case17; Cilica must retain its pinned .NET mapping.')
    finding('invariant-table',CPP/'mscorlib__15.cpp',9053,9081,'TextInfo invariant range_00e0_0586',
       'The invariant range indexes code_unit-224. Metadata-only inspection proves entry159 for383/U+017F contains383, leaving long s unchanged.',
       'Retain semantic mismatch. FieldRVA proof and the unchanged input transcript are independent evidence; no production correction indicated.')
    finding('casing-table-initializer',CPP/'mscorlib__16.cpp',3096,3103,'TextInfoToUpperData..cctor',
       'The1191-character table initializes from exact FieldRVA4E0B9E...; the2382-byte data hash matches that identifier.',
       'Read emitted mscorlib only as metadata/data; no assembly loading or extra candidate execution.')
    finding('argument-message',CPP/'mscorlib__1.cpp',17170,17212,'ArgumentException.Message',
       'Unity appends Environment.NewLine and formatted parameter-name text. Raw case0 message differs from pinned .NET punctuation/layout.',
       'Preserve every UTF-16 observation and failed correctness outcome; do not normalize messages.')
    finding('dispatch-helper',ROOT/'capture/runtime-reference/codegen/il2cpp-codegen.h',717,727,'IL2CPP virtual/interface invoke data',
       'Class calls select the vtable slot; interface calls use ClassInlines interface lookup.',
       'Reference shape only. Cilica precise ownership and validated dispatch remain required.')
    finding('cilica-array-any',ROOT/'cilica-reference/library/Cilica.Core/Enumerable.cs',220,233,'Cilica Any<TSource>(source,predicate) at b8f4628',
       'After source/predicate validation the owned library detects arrays and directly indexes live elements, avoiding the boxed-enumerator path visible in Unity.',
       'An existing structural advantage worth a future fresh matched performance check; no new runtime optimization or measured speedup.')
    finding('cilica-invariant',ROOT/'cilica-reference/library/Cilica.Core/StringInvariantCasing.cs',41,78,'Cilica String.ToUpperInvariant at b8f4628',
       'Cilica retains unchanged ASCII identity, handles surrogate pairs and explicitly maps U+017F to U+0053 for invariant uppercase.',
       'Existing frozen .NET differential trial passes; Unity disagreement is not a Cilica defect.')
    finding('cilica-roots',ROOT/'cilica-reference/src/Cilica.Compiler/ObjectCppRuntime.cs',280,310,'Cilica valid_roots / allocate_string at b8f4628',
       'The tested frozen baseline validates published root ownership before allocation and charges bounded managed storage.',
       'This old test pin predates the separate lazy-prefix proposal. No claim that current optimized runtime was exercised by Albireo; no safeguards removed.')
    put('source-inspection.json',{'input_fingerprint':fp,'status':'inspected','locations':sources,
      'cilica_reference_commit':'b8f462870351be165692401d90340c019fadc3ae',
      'decision':'No production implementation change. Keep pinned .NET semantics; Unity is a qualified code-generation/profile reference with two actual divergences.',
      'limits':'Static structural comparison plus executed selected workload. Not full upstream test coverage, no Encode execution, no speed ratio or current-head optimization result.'})
    put('external-evidence.json',{'root':str(ROOT),'capture':str(ROOT/'capture'), 'capture_manifest':original(ROOT/'capture/manifest.json'),
      'app':str(APP),'native_hashes':native,'source_location_count':len(sources),
      'logs':[original(ROOT/'build/unity-build.log'),original(ROOT/'inspection/inspect.log')],
      'preservation':'Keep original project, app, full capture, partial capture and CLI-created default-state archives. No cleanup performed. Raw Editor/license logs stay local, not in the compact repository handoff.',
      'active_owned_jobs':False})
    put('decisions.json',{'input_fingerprint':fp,'inspection_status':'inspected','profile_status':'measured','profile_kind':'diagnostic',
      'unity_status':'captured','unity_correctness':'failed','full_completion':'blocked',
      'reason':'Unity case0 exception text and case17 invariant casing differ from the pinned .NET/Cilica oracle despite matching input identity.',
      'profile_qualification':'Selected4cases match oracle and raw3markers are verified, while full32-case equality fails. Mixed Development wrapper batches, not pure Decode timing; universal artifact, process architecture unrecorded, no quiet-host or competitive claim.',
      'production_changes':'None. Preserve .NET contract; do not change Cilica to emulate Unity.',
      'next_action':'Integrate exact evidence and keep workflow completion blocked on semantic divergence. No more candidate execution requested.'})
    def envelope(name,stage,outcome,names):
        return put(name,{'stage':stage,'outcome':outcome,'input_fingerprint':fp,'artifacts':[ref(n) for n in names]})
    envelopes={
      'unity-capture':envelope('unity-capture-envelope.json','unity-capture','passed',[
        'capture-manifest.json','external-evidence.json','build-provenance.json','build-command.json',
        'settings-readback.json','actual-architecture-and-commands.json','manifest-restoration.json','packages-lock.json','handoff-verification.json']),
      'unity-correctness':envelope('unity-correctness-envelope.json','unity-correctness','failed',[
        'oracle-expected.json','verify-result.json','verify-launch.json','verify-player.log','differences.json']),
      'profile':envelope('profile-envelope.json','profile','recorded',[
        'profile.raw','profile-result.json','profile-launch.json','profile-player.log','markers.json','inspection-inputs.json',
        'inspection-result.json','profile-validation.json','actual-architecture-and-commands.json'])}
    put('handoff.json',{'input_fingerprint':fp,'repository_destination':str(PREFIX),'envelopes':envelopes,
      'settings':ref('settings-readback.json'),'actual_settings_qualification':ref('actual-architecture-and-commands.json'),
      'inspection':ref('source-inspection.json'),'decision':ref('decisions.json'),'locations':sources,
      'unity_status':'captured','correctness_status':'failed','profile_status':'measured','profile_kind':'diagnostic',
      'profile_roots':['Base32.Decode(in string)'],'full_gate':'blocked by genuine same-input Unity semantic mismatch',
      'external_preservation':ref('external-evidence.json')})
    manifest={'input_fingerprint':fp,'files':{str(p.relative_to(ROOT/'integration')):digest(p) for p in sorted(OUT.rglob('*')) if p.is_file()}}
    (ROOT/'integration-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    # Recheck all outgoing artifacts, including every envelope's raw artifact reference.
    for name,sha in manifest['files'].items(): assert digest(ROOT/'integration'/name)==sha
    for receipt in envelopes.values():
        record=json.loads((ROOT/'integration'/receipt['path']).read_text())
        for artifact in record['artifacts']: assert digest(ROOT/'integration'/artifact['path'])==artifact['sha256']
    print(json.dumps({'status':'packaged','files':len(manifest['files']),'bytes':sum(p.stat().st_size for p in OUT.rglob('*') if p.is_file()),
       'source_locations':len(sources),'full_gate':'blocked','package':str(OUT),'manifest_sha256':digest(ROOT/'integration-manifest.json')}))

if __name__ == '__main__': main()

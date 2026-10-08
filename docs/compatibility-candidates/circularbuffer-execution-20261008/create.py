import json,pathlib,subprocess
b=pathlib.Path(__file__).resolve().parent;d=['docker','--config','/tmp/cilica-circularbuffer-approved-20261008/docker-config','--host','unix:///Users/christian/.docker/run/docker.sock']
id=json.loads(subprocess.check_output(d+['image','inspect',json.loads((b/'new-image-id.json').read_text())['id']]))[0]
(b/'input-image.json').write_text(json.dumps(id,indent=2)+'\n')
args=['create','--name','cilica-circularbuffer-approved-20261008','--network','none','--user','10001:10001','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges','--cpus','2','--memory','8g','--memory-swap','8g','--pids-limit','256','--init','--log-driver','none','--tmpfs','/work:rw,exec,nosuid,nodev,size=9g,uid=10001,gid=10001,mode=0700','--tmpfs','/tmp:rw,exec,nosuid,nodev,size=1g,uid=10001,gid=10001,mode=0700','--tmpfs','/evidence:rw,noexec,nosuid,nodev,size=1g,uid=10001,gid=10001,mode=0700']
env={'HOME':'/work/home','DOTNET_CLI_HOME':'/work/home','NUGET_PACKAGES':'/work/nuget','TMPDIR':'/tmp','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_PROCESSOR_COUNT':'2','DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','NUGET_CERT_REVOCATION_MODE':'offline','CILICA_CXX':'clang++-18','CILICA_CSC_DIRECTORY':'/usr/share/dotnet/sdk/10.0.401/Roslyn/bincore','CILICA_APPROVED_CIRCULARBUFFER_PIN':'9ebb911dfe27a62c2793d0b02c642f79bbbf524b','CILICA_CIRCULARBUFFER_ASSEMBLY':'/work/out/CircularBuffer.SelectedRoots.dll'}
env['CILICA_NATIVE_EVIDENCE_DIRECTORY']='/evidence/native-hashes'
for k,v in env.items():args+=['--env',k+'='+v]
args+=[id['Id']];(b/'create-arguments.json').write_text(json.dumps(args,indent=2)+'\n')
subprocess.run(d+args,check=True)

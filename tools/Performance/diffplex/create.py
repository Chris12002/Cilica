import json,pathlib,subprocess
b=pathlib.Path(__file__).resolve().parent;d=['docker','--config',str(b/'docker-config'),'--host','unix:///Users/christian/.docker/run/docker.sock']
id=json.loads(subprocess.check_output(d+['image','inspect',(b/'image-id.txt').read_text().strip()]))[0]
(b/'input-image.json').write_text(json.dumps(id,indent=2)+'\n')
args=['create','--name','cilica-performance-fingerprint-20261008','--network','none','--user','10001:10001','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges','--cpus','2','--memory','8g','--memory-swap','8g','--pids-limit','256','--init','--log-driver','none','--tmpfs','/work:rw,exec,nosuid,nodev,size=9g,uid=10001,gid=10001,mode=0700','--tmpfs','/tmp:rw,exec,nosuid,nodev,size=1g,uid=10001,gid=10001,mode=0700','--tmpfs','/evidence:rw,noexec,nosuid,nodev,size=1g,uid=10001,gid=10001,mode=0700']
env={'HOME':'/work/home','DOTNET_CLI_HOME':'/work/home','NUGET_PACKAGES':'/work/nuget','TMPDIR':'/tmp','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_PROCESSOR_COUNT':'2','DOTNET_GENERATE_ASPNET_CERTIFICATE':'false','NUGET_CERT_REVOCATION_MODE':'offline','CILICA_CXX':'clang++-18','CILICA_CSC_DIRECTORY':'/usr/share/dotnet/sdk/10.0.401/Roslyn/bincore','CILICA_APPROVED_DIFFPLEX_PIN':'f500e73f28e28813f942f1f16dec5a34b40e48e3','CILICA_DIFFPLEX_ASSEMBLY':'/work/out/DiffPlex.SelectedRoots.dll'}
for k,v in env.items():args+=['--env',k+'='+v]
args+=[id['Id']];(b/'create-arguments.json').write_text(json.dumps(args,indent=2)+'\n')
subprocess.run(d+args,check=True)

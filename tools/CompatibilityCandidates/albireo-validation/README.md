# Proposed Albireo.Base32 Decode validation

Awaiting explicit approval. These files live outside the ordinary test build.
Do not interpret opt-in environment variables as authorization or a sandbox.

1. Export the Cilica commit pinned by the proposal to a fresh disposable input
   directory, adding only the hashed owned overlay from that proposal.
2. Fetch only the two candidate blobs (Base32.cs and LICENSE.txt), verify exact Git
   blob hashes and retain the licence. Never import the candidate csproj/targets.
3. Copy the verified 24-package offline feed and these inputs into an image based
   on the already inspected immutable tooling image. The Docker build copies data
   only, uses `--network none`, and runs no third-party script.
4. In a nonroot, credential-free, network-none container with the proposal's
   read-only root, tmpfs and CPU/memory/PID limits, copy input data to /work.
   Clear inherited package sources. Restore/build the owned SelectedDecode.csproj
   using only the local offline feed, CandidateSource=/work/input and checked
   arithmetic. Do not import upstream projects, analyzers or build hooks.
5. Copy ApprovedAlbireoExecutionTests.cs into only the disposable Cilica export's
   test folder; restore/build its known owned test project from the same feed.
   Set CILICA_APPROVED_ALBIREO_PIN to the exact approved pin,
   CILICA_ALBIREO_ASSEMBLY to the built SelectedDecode DLL, and
   CILICA_NATIVE_EVIDENCE_DIRECTORY to an initially empty /evidence/native directory.
6. Invoke only the explicit ApprovedAlbireoExecutionTests class with invariant
   culture, serial execution and `-explicit only`. It translates the selected root
   before invoking the oracle; metadata/native failures stop the run and remain
   evidence. Normal .NET runs solely in this container. Encode is unreachable.
7. Enforce the 1,800-second supervisor deadline and 30-second native subprocess
   limit. Extract logs/receipts without host mounts, record complete observations,
   and retain hashes/approval/versions before verified task-only cleanup.

The committed probe has 32 bounded scenarios: RFC4648 examples/lowercase, null and
empty, omitted/excess/internal padding, incomplete groups, invalid alphabet/NUL/
space, invariant long s and dotless/dotted i, lone/paired surrogates, repeated bits
and 256/257-unit strings. It records every output byte and exception text unit,
including identity of repeated empty results. Conditional lambda-cache roots are
checked for stable survivors; this candidate run does not replace the precise
owned GC tests retained in batch017. The full Unicode casing sweep is batch018.

No candidate download, build, oracle invocation or native execution occurred while
preparing this proposal. Existing tooling image and offline packages were inspected
and verified read-only. The owned adapter/test source was reviewed but is unbuilt;
its initial compile/translation failures, if any, must be retained without widening
this approved source/root/isolation scope.

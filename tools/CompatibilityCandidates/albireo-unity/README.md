# Albireo Unity preparation

Owned candidate-specific adapters; no runner and no execution permission.
See [the exact plan](../../../docs/compatibility-candidates/albireo-unity-plan.md)
for input pins, known host limitations, approval and completion gates.

`stage_bundle.py` copies an opaque, hash-pinned isolated-build DLL and the exact
MIT licence into a fresh template bundle. It does not load the DLL, create a Unity
project or launch a tool. Use a canonical physical output path without symlink
parents. `transcript.py` converts the complete isolated oracle into the expected
Unity transcript and compares all observations. It never invokes candidate code.
The fingerprint argument must come from the reviewed workflow input manifest;
passing an arbitrary hash does not establish the manifest's identity.

No upstream project, test, generator, analyzer or binary is included here.
The template contains owned code only. The candidate is supplied later under
separate approval. Unity import/build/profile and capture matcher adaptation
remain untested and pending; do not invoke the DiffPlex prepare script.

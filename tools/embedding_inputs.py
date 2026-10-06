"""Validate one emitted v3 source/header/manifest set for platform packaging."""
import hashlib
import json
import re


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def read_inputs(source, header, manifest):
    source_bytes, header_bytes = source.read_bytes(), header.read_bytes()
    document = json.loads(manifest.read_bytes())
    contract = document["contract"]
    if document["profile"] != "embedding-object-handles-v3" or contract["abiVersion"] != 3:
        raise ValueError("Only emitted embedding-object-handles-v3 modules are supported.")
    # The emitter's canonical contract contains ASCII identifiers and numeric shapes.
    canonical = json.dumps(contract, ensure_ascii=True, separators=(",", ":")).encode("ascii")
    fingerprint = sha256(canonical)
    if fingerprint != document["contractHash"]:
        raise ValueError("Manifest contract fingerprint does not match its canonical schema.")
    name = contract["name"]
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,47}", name):
        raise ValueError("Invalid emitted module name.")
    prefix = "cilica_" + name + "_abi_v3"
    macro = (prefix.upper() + '_CONTRACT_HASH "' + fingerprint + '"').encode("ascii")
    if macro not in header_bytes or not source_bytes.startswith(header_bytes):
        raise ValueError("Source, header and manifest are not the same emitted artifact.")
    return document, {"source": sha256(source_bytes), "header": sha256(header_bytes),
                      "manifest": sha256(manifest.read_bytes())}

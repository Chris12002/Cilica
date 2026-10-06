import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import build_xcframework as build


class PackageInputTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.schema = {"abiVersion": 3, "name": "probe", "schemaVersion": 1,
                       "references": [], "exports": []}
        self.write(self.schema)

    def write(self, schema):
        fingerprint = hashlib.sha256(json.dumps(schema, separators=(",", ":")).encode()).hexdigest()
        self.manifest = self.root / "contract.json"
        self.manifest.write_text(json.dumps({"profile": "embedding-object-handles-v3",
                                            "contractHash": fingerprint, "contract": schema}))
        self.header = self.root / "api.h"
        self.header.write_text('#define CILICA_PROBE_ABI_V3_CONTRACT_HASH "' + fingerprint + '"\n')
        self.source = self.root / "engine.cpp"
        self.source.write_bytes(self.header.read_bytes() + b"// generated body\n")

    def test_mixed_artifact_and_changed_schema_are_rejected(self):
        document, hashes = build.read_inputs(self.source, self.header, self.manifest)
        self.assertEqual(document["contract"]["name"], "probe")
        self.assertEqual(len(hashes["source"]), 64)
        changed = copy.deepcopy(document)
        changed["contract"]["schemaVersion"] = 2
        self.manifest.write_text(json.dumps(changed))
        with self.assertRaisesRegex(ValueError, "fingerprint"):
            build.read_inputs(self.source, self.header, self.manifest)
        self.write(self.schema)
        self.source.write_text("unrelated module")
        with self.assertRaisesRegex(ValueError, "same emitted artifact"):
            build.read_inputs(self.source, self.header, self.manifest)

    def test_existing_output_and_unsafe_module_fail_before_external_commands(self):
        args = ["--source", str(self.source), "--header", str(self.header), "--manifest", str(self.manifest),
                "--output", str(self.root), "--module", "Probe"]
        with patch.object(build.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "already exists"):
                build.main(args)
            with self.assertRaisesRegex(ValueError, "identifier"):
                build.main(args[:-1] + ["../Probe"])
            run.assert_not_called()

    def test_wrong_toolchain_does_not_create_output(self):
        output = self.root / "new"
        with patch.object(build.subprocess, "run") as run:
            run.return_value.stdout = "Xcode 0.0\nBuild version different\n"
            with self.assertRaisesRegex(ValueError, "toolchain pin"):
                build.main(["--source", str(self.source), "--header", str(self.header),
                            "--manifest", str(self.manifest), "--output", str(output), "--module", "Probe"])
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()

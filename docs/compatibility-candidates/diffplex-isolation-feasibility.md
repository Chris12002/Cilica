# Approved DiffPlex execution: isolation feasibility

> **Setup and execution update (2026-10-08):** The user subsequently approved Docker/tooling setup. The offline image and both builds succeeded; the explicit test stopped at CIL3003 (exact netstandard 2.0 contract absent). Zero of 64 scenarios ran. See [retained execution report](diffplex-execution-20261008.md). The account below describes the earlier state.

Checked read-only on 2026-10-08. The user approved the pinned selected-source test
at 07:17:49 UTC: “Yes, sounds good for a real test of Cilica”. Approval covers
commit f500e73f28e28813f942f1f16dec5a34b40e48e3, the 14-source recipe and authored
probe, offline isolation without credentials/live files, 2 CPUs, 8 GiB and 30
minutes. The existing plan also proposes process/disk/output caps. Provisioning
images, installation, new network access and other repositories are excluded.

**Execution has not started. No 64-scenario candidate result exists.** The approved
Cilica baseline is 268024e94879acdb004228afa360e1222e2068b8. git cat-file confirms
that revision exists. Its SelectedRoots.csproj, Probe.cs and explicit driver match
the current files exactly. Export that baseline when an approved environment is
available; do not include the later CRC or GUID work implicitly.

Observed prerequisites:

- Docker Desktop is installed at /Applications/Docker.app; its CLI is Docker
  29.1.3, build f52814d. /usr/local/bin/docker links into that application.
- The explicit local endpoint unix:///Users/christian/.docker/run/docker.sock
  has no socket. Read-only `docker --host ... version --format '{{json .Server}}'`
  and `image ls --format '{{.ID}} {{.Repository}}:{{.Tag}}'` cannot connect.
  /var/run/docker.sock points at the same absent endpoint. No remote context used.
- An existing Docker VM disk was observed by file metadata only. It was not opened
  or mounted. Image contents and availability remain unknown while the daemon is
  unavailable; no suitable offline image has been verified.
- Host SDKs: .NET 9.0.306 and 10.0.400; runtimes 9.0.10 and 10.0.11. Host Clang is
  Apple Clang 17, arm64-apple-darwin24.6.0. These macOS binaries do not establish a
  Linux container toolchain. Python3 is installed on the host.
- Configured global package cache: /Users/christian/.nuget/packages/. The requested
  NETStandard.Library 2.0.3 directory is absent. Existing host packages include
  Microsoft.NET.Test.Sdk 18.8.1, xunit.v3 3.2.2, runner 3.1.5 and banned analyzers 5.6.0.
  No approved complete offline feed or guest SDK/Clang image has been identified.

Next prerequisite decision: make an existing approved isolation runtime available
and inspect its local images, then identify a credential-free offline image with
the SDK, Clang, references and package closure. An offline NETStandard.Library 2.0.3
reference package is needed unless such an image already contains it. Starting
services, supplying/provisioning an image or acquiring missing packages is outside
this read-only check and may need a separate decision. No tools or permissions were
changed, no daemon/image/container was started, and no credentials were accessed.
Do not replace the recipe with host execution or network-enabled restore.

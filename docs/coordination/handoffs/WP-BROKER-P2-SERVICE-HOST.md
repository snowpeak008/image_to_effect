# WP-BROKER-P2-SERVICE-HOST handoff

Status: **STOPPED** — `DORMANT_SCM_SERVICE_RUNTIME_FOUNDATION_ONLY`. The new executable is standalone framework-dependent and remains fail closed. It neither installs, registers, starts, configures, nor deletes an SCM service, and it has no listener, policy activation, project access, Worker/Desktop/Unity integration, or authority capability.

Model: `gpt-5.6-terra`; reasoning: `max`.

Implementation began with the first retained source write at `2026-08-27T07:12:37.2249250Z`. Final validation/evidence closeout completed at `2026-08-27T07:23:20.3234061Z`.

## Scope and bounded model

Only the registered roots, exact solution membership, and this handoff were authored:

- `services/VFXComposer.Broker.ServiceHost/**`
- `services/VFXComposer.Broker.ServiceHost.Tests/**`
- `VFXComposer.sln` — two project declarations, their Debug/Release configurations, and their existing `services` solution-folder nesting only.
- `docs/coordination/handoffs/WP-BROKER-P2-SERVICE-HOST.md`

The product has no Broker project reference and makes no call to `BrokerPolicy` or any activating policy path. Its managed lifecycle admits only `Stopped -> StartPending -> Running -> StopPending -> Stopped`, rejects other graph edges, bounds each pending report to checkpoint `1` and a `5000 ms` wait hint, and makes STOP/shutdown idempotent. The current host never calls the `Running` edge. It reports `StartPending`, `StopPending`, then `Stopped` with `ERROR_SERVICE_SPECIFIC_ERROR` / service-specific code `23` when the policy/issuer is unavailable.

`Program` sends exactly `W24FS001` to stderr (8 bytes, no stdout) and returns `23` for a direct non-SCM launch. The Windows adapter contains only `StartServiceCtrlDispatcherW`, `RegisterServiceCtrlHandlerExW`, `SetServiceStatus`, and `GetLastError`; callbacks are rooted in host fields, callback exceptions are contained, and all native handle/state material is internal/non-serializable.

## Baseline and frozen-byte replay

The required pre-edit replay passed. Both service-host roots were absent at that time, and the frozen pre-edit solution SHA-256 was `52f5773cf00f230d451cb73ba5228735987ea8bfa7c6f894dc441a746df2604e`.

| Frozen root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 32 | `e5b7fad903c584422c8c5afa2e810a9fdfeebf34b3d765a527aea935a5f9f3ac` |
| `services/VFXComposer.Broker.Tests` | 14 | `f345f0ba8b8b32619a8bf6fde1cd9702a3bd390f89b488e6c49a9066b23456e2` |
| `services/VFXComposer.Broker.HandleProbe` | 4 | `5224ef3128a6ab240ab1192d221a78bbc40a006c7a13210a6e17f11990eeabf8` |
| `src/VFXComposer.Protocol` | 67 | `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56` |
| `src/VFXComposer.Protocol.Tests` | 21 | `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5` |
| `docs/schemas/desktop` | 33 | `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9` |

All four frozen Unity files replayed exactly: `VfxStudioWindow.cs` `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`; `VfxStudioModels.cs` `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`; `W24S6EditorIntegrationTests.cs` `d80c05fbb16119ac7ed832a2057388670a598971f7445067fd8958350d2c2ad6`; `W24S6StudioModelsTests.cs` `28188ffcbd31471876c137be3023aa668f93327a506dd4862c06d355e372d33d`.

The final replay is in `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/final/manifests/baseline-and-frozen-replay.txt` (`a993dc0f01ef97ef8ede0d97a06debaaef4702d509a6b2779d145e23bd95831f`), with `overallPassed=True`.

## Provenance recovery

A fresh evidence-only recovery reproduced the claimed pre-edit solution bytes directly from the frozen current solution. It removed exactly the two complete project blocks (including their two `EndProject` lines), eight configuration lines, and two nesting lines, preserving UTF-8 BOM and LF-only bytes. The 5,639-byte reconstruction hashes to `52f5773cf00f230d451cb73ba5228735987ea8bfa7c6f894dc441a746df2604e`; reapplying the same 1,276-byte delta at the three unique anchors recreates the existing 6,915-byte solution byte-for-byte at `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`.

The durable recovery receipt is `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/provenance-recovery/README.md` (`f41b098e64a1ed1d305f858cc1edfca745370d80c86f23424d89db5708a8c9be`); the exact delta is `solution-membership.diff` (`9f6a45c6c3b3194016326aa97d71d64e41a279cbc02bde57558b71e32d64f169`); and the read-only reproducer is `verify-provenance.ps1` (`53a027464e9277ff0522114a42524f21cf169f6f79f1431ffc90bd52ec2ec737`). No encoding/newline/BOM normalization is asserted or needed. `VFXComposer.sln` was not modified during recovery, so no build/test rerun or pre-existing receipt rewrite occurred. A fresh independent read-only delta audit reproduced the exact baseline and round trip and closed at `P0=0 / P1=0 / P2=0`.

## Exact authored source manifest

The manifest is repository-relative, forward-slash, lowercase SHA-256, two-space separated, LF-terminated, UTF-8 without BOM, and sorted with `StringComparer.Ordinal`. It excludes this handoff to avoid self-reference.

```text
b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689  VFXComposer.sln
75bab4b034727d7930f730ad68a501abbb36e70eacfee05ed7a265a8e0c15fa8  services/VFXComposer.Broker.ServiceHost.Tests/DirectLaunchSmokeTests.cs
704128897b95fe62bf3bc51f8de4e44f44b2f1cf35c5c11f3b92a798b910ca9b  services/VFXComposer.Broker.ServiceHost.Tests/ServiceHostBoundaryTests.cs
d6b8fcfbbdc0dd7a6a05589b50947641de190b9aa2b2191d589e4d50143f4d06  services/VFXComposer.Broker.ServiceHost.Tests/ServiceLifecycleTests.cs
40069ac6eb5486178152524b1d9c14b76820148dfa17bc9f6fe4ba47a3ff9a07  services/VFXComposer.Broker.ServiceHost.Tests/VFXComposer.Broker.ServiceHost.Tests.csproj
0dee853e3aa09e16ff1e96aa7cff6001e9ecc3fbadc04f3cf95c98b3ab7df0d9  services/VFXComposer.Broker.ServiceHost.Tests/WindowsScmServiceHostTests.cs
55ca3c7ed2090ec08bfc4e4209f26ab4bedf3b6c992e8cc03bdc368324dabab0  services/VFXComposer.Broker.ServiceHost.Tests/packages.lock.json
0f86f9c8070a8b5adf5799085558e7ea964c8167c6c8e8f71757b699d09bbd3c  services/VFXComposer.Broker.ServiceHost/Program.cs
8da4a24f0b32cf9b7862e43bceaa4d5188214451870fced41280a4a1bcca5153  services/VFXComposer.Broker.ServiceHost/Properties/AssemblyInfo.cs
5d761f7e68648d869514a05070dd49d76e22bdf906382adae3b5da1051df9e3c  services/VFXComposer.Broker.ServiceHost/ServiceHostDiagnostics.cs
8ad8ff42ccadab90212f92d596e98cadfdd208bed4863bcd09522e6cc1e2ee24  services/VFXComposer.Broker.ServiceHost/ServiceLifecycle.cs
e3696f59777ff9198f9afe426e6ece9e6cd54de08d7fdde6f67faf2226dc5255  services/VFXComposer.Broker.ServiceHost/VFXComposer.Broker.ServiceHost.csproj
8365179720397905dc101f3d5a85642eb7785f201f8e7d0a7404033d042d9333  services/VFXComposer.Broker.ServiceHost/WindowsScmInterop.cs
23e72c1cca04833f76212688ecc6b4bde239a1aebc35b62aacb9f5c7b96a8a39  services/VFXComposer.Broker.ServiceHost/WindowsScmServiceHost.cs
d3a254ffea01d08b134d98314ea256b347e27943e74e85a70d6294422c062ab2  services/VFXComposer.Broker.ServiceHost/packages.lock.json
```

Receipt: `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/final/manifests/changed-source-manifest.sha256`, 15 entries, SHA-256 `4d1154af3a300201f2b941ef8017524a4e473ac773c02e17ed291f1c4542f44c`.

Final new-root aggregates use the same algorithm:

- `services/VFXComposer.Broker.ServiceHost`: `8` / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`
- `services/VFXComposer.Broker.ServiceHost.Tests`: `6` / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`
- `VFXComposer.sln`: `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`

## Validation and durable receipts

All restores used the existing local-only NuGet configuration and existing centrally pinned package versions. No package or root configuration changed.

1. `dotnet restore services/VFXComposer.Broker.ServiceHost/VFXComposer.Broker.ServiceHost.csproj --locked-mode` — exit `0`.
2. `dotnet restore services/VFXComposer.Broker.ServiceHost.Tests/VFXComposer.Broker.ServiceHost.Tests.csproj --locked-mode` — exit `0`.
3. Release host build and Release test-project build with `--no-restore -p:RestoreLockedMode=true` — each exit `0`, `0` warnings, `0` errors.
4. Release host ABI builds with `PlatformTarget=x86` and `PlatformTarget=x64` — each exit `0`, `0` warnings, `0` errors. The ABI/layout test suite executed under x64 and verifies 28-byte `SERVICE_STATUS` layout, its field offsets, and a two-pointer dispatch entry layout.
5. `dotnet test services/VFXComposer.Broker.ServiceHost.Tests/VFXComposer.Broker.ServiceHost.Tests.csproj --configuration Release --no-build --no-restore -p:RestoreLockedMode=true --logger "trx;LogFileName=service-host-final.trx" ...` — exit `0`; **16/16 passed**, 0 failed, 0 skipped/not-executed. TRX: `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/final/test-service-host-release/test-results/service-host-final.trx` / `1e023fa57d3cac3c706bae2cb698a2804f5a5a49e47345781dd6330cd2396e79`.
6. `dotnet build VFXComposer.sln --configuration Release --no-restore -p:RestoreLockedMode=true` — exit `0`, `0` warnings, `0` errors.
7. Direct host DLL smoke — exit `23`; stdout SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (0 bytes); stderr SHA-256 `30c4b162221a5f50c639109bbb15ffe7f1af3dd44a6310575abf975c693b3c7a` (exactly `W24FS001`, 8 bytes); exit receipt SHA-256 `076320a2a08267b4c026d06573bba408ea68841e73cdc20e62cce59de165ece3`.
8. Product source scan passed with no SCM mutation, listener/network, project I/O, environment, Unity, or authority/verdict surface. PE scan passed with zero exported types and exactly the four allowlisted P/Invoke imports. Receipts: `static-scans/forbidden-surface-source-scan.txt` / `bb013ab994a337017e8f13aa716d907a91451b4f123807964e8765c05cb3c60b`; `static-scans/pe-interop-scan.txt` / `15ad6feb935b09cf9fe9a933a126885f2959da7f75a349062e297138cdf09ae2`.

Final command stdout/stderr/exit receipts are retained below `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/final/`; command summary SHA-256 `bc4828c5c58ad2179d5dec45c5b9d5009b6e4d6f1b5eb9a40ae03deed206df48`, binary manifest SHA-256 `d1bda3dfa2cffb1ab8b57978b5182599211ec7a82eabd09ad8fa570831a6e2a6`, and final evidence manifest (39 entries, excluding itself) SHA-256 `033ff72b30f7c18619a83a4c1be8d2c12b40488ae3ef282835a668eddce456e2`.

An early exploratory `dotnet build ... --locked-mode` invocation exited `1` before compilation because this SDK accepts locked mode for build via `-p:RestoreLockedMode=true`. It is retained only in the initial evidence; all final receipt commands use the accepted repository form above and passed.

## Proven invariants

- Direct and SCM-dispatch paths are closed to `W24FS001` / `23`; SCM closure reports bounded pending then service-specific stopped status and cannot reach `Running`.
- STOP/shutdown callbacks, reentrant callbacks, concurrent disposal, late callbacks, registration failure, and status-report exceptions are deterministically contained and leave the lifecycle stopped.
- The service name is the fixed compile-time `VFXComposerBrokerHost` token. Neither command-line/caller input nor configuration chooses it.
- There is no SCM database mutation, service installation, registry access, process launch, environment access, listener, network, pipe, filesystem/project I/O, raw public handle, public DTO, or authority/verdict surface.

## Self-audit and remaining blockers

- P0: none found in this bounded service-host package. No activation route, listener, service mutation, or project access was added.
- P1: none found. Callbacks are rooted and exception-contained; state/disposal sequencing is serialized; x86/x64 compilation passed, while ABI/layout tests executed under x64.
- P2: none found. The product contains no public or serializable data contract, and all claims remain limited to a dormant runtime foundation.

Still not proved or enabled: an independently privileged installed service/issuer; SCM installation or registration; trusted service SID/image/executable-content attestation; production policy activation; real pipe ACL application; listener/peer admission; project registration/access; Worker/Desktop/Unity integration; command execution; mutation; machine/visual/user verdict, L3, or L4. A later separately authorized package and independent audit must address those blockers.

This handoff intentionally does not hash itself. **STOPPED.** Do not extend this package into service installation, policy activation, listener creation, ACL application, project I/O, Worker/Desktop/Unity integration, or authority.

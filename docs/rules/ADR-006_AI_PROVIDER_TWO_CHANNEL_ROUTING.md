# ADR-006: AI provider two-channel routing and user-defined endpoints

Status: `ACCEPTED — A0–A6 CLOSED — FINAL ACCEPTED/GO — P0/P1/P2=0/0/0; ADR-006 AI TWO-CHANNEL SCOPE 100/100` on `2026-08-29`.

Normative architecture token: `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

This ADR is a documentation-only rebase of the post-U6 AI contract. It changes no source, runner, network behavior, credential, Desktop UI, Broker, Worker, Unity, project-write, or production-service capability. It supersedes only the earlier endpoint-admission design; ADR-005's ordinary-user Broker/Worker route remains closed and unchanged.

## 1. Program boundary and delivery state

The accepted U6 receipt remains the closeout for the USER_MODE architecture: `P0=0 / P1=0 / P2=0`, with the default Broker contract unchanged (`W24FS001` on stderr and exit `23` for no-argument launch). That final GO is not an AI-provider runtime claim.

The post-U6 AI DAG remains exactly:

`A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`.

`A0` and `A1 — AI_PROVIDER_FOUNDATION` are closed; A1 is finally accepted at merged source commit `698e770a35062cc4135872147a401dce40adcb51`. `A2 — WP-AI-CHAT-CHANNEL` and `A3 — WP-AI-IMAGE-CHANNEL` are each `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. A2 is the accepted sequence `55ee0993f71375ee0245cbee54815e7988fe04fd` followed by the redirect-boundary fix `2678cb62be9ac9ff5a05c9a5b605a75c60effb5c`; A3 is `c7c4adcfcc80c732bfaf87b0dfea11294b4af741` followed by `12b58ac69efe3175cf49a6ee129b3784b5b3da5c`. Their closeout records Chat `23/23 × 3`, Image `20/20`, and a Release solution build with `0 warnings / 0 errors`.

Combined QC `P1#1` remains superseded by the explicit requirement clarification: standard .NET/HTTP request-time `RequestUri` normalization is not a configuration-storage finding. Combined QC `P1#2` is closed by the A3 redirect transport boundary. Neither closeout is a live-provider, paid-image, Desktop integration, project-write, Broker, Worker, or Unity claim.

`A4 — AI_DESKTOP_WIRING` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. Its accepted source sequence is `fc986d11`, `ffc9f609`, and `cc5ff806`; the final receipt is `186/186`, with AI `77/77`, Desktop `22/22`, and `423/423` total tests, frozen-root replay `0`, and owned-runtime/artifact residue `0`. `A5 — AI_MOCK_E2E` is `CLOSED — FINAL ACCEPTED / GO — P0/P1/P2=0/0/0`: source `9152c7e6`, remediation `14abb1d3`, and integration merge `c6f9920f`. Its accepted local-only receipt `a5-final-acceptance-14abb1d3-bootstrap` records `11/11`, `209` SHA-256 receipt hashes, frozen-root replay `0`, unchanged tracked locks, and point-in-time runtime/pipe/owned-A5 residue `0`. It used a real loopback `TcpListener` with the production runtime and production `HttpClient` handlers, never handler injection, external traffic, or paid calls. `A6 — WP-AI-PROVIDER-FINAL-AUDIT` is `CLOSED — FINAL ACCEPTED/GO — P0/P1/P2=0/0/0`; it closes the ADR-006 AI functionality scope at `100/100`.

## 2. Decision: two explicit channels, no fallback

There are exactly two product AI channels:

| Channel | Sole permitted work | Mandatory binding |
|---|---|---|
| `ChatLlm` | All LLM, conversation, and text-generation operations. | The one explicit `ChatLlm` binding. |
| `ImageGeneration` | All image-generation operations. | The one explicit `ImageGeneration` binding. |

Each channel resolves exactly one explicit, channel-owned `ProviderProfile`, `SecretRef`, declared capability, model, and protocol. `ChatLlm` and `ImageGeneration` retain independent profiles, secrets, and protocols; no selection may be reused, inferred, or routed across channel boundaries. Origin (`Official`, `Relay`, `Friend`, `Subscription`, or `Custom`) is descriptive metadata; it does not infer a protocol, request format, authentication method, adapter, model, or fallback.

Feature callers enter `IAiGateway` with channel-specific request DTOs. They cannot select a profile, model, protocol, endpoint, adapter, header, credential, or second choice. Missing, disabled, unknown, cross-channel, stale, corrupt, or unsupported binding state fails closed before network activity.

The following remain forbidden:

- implicit default, last-successful, only-enabled, environment, PATH, display-name, model-name, origin, import-derived, or URL-derived routing;
- fallback or retry to another channel, profile, capability, model, endpoint, origin, or adapter;
- automatic protocol detection, or an unknown protocol falling back to an OpenAI-compatible or any other adapter;
- cookie scraping, browser-state access, scripts, CLI/sidecars, dynamic provider DLLs, arbitrary header templates, or TLS-validation bypasses.

Bounded retries may later be added only for the already resolved one route and request. A failure never chooses a different route.

## 3. Decision: endpoint is an opaque, user-owned configuration string

`OpaqueEndpoint` is the endpoint contract. Its value is a user-supplied, user-editable configuration string that the product saves and resolves as entered. It may represent an official API, relay, friend-provided service, subscription-related custom address, or any other custom address. It may contain a scheme, host, port, user-info, path, query, fragment, or text that is not a URI at all.

At every configuration boundary — configuration JSON/codec, atomic store, resolver, imported draft, and explicit UI editing value — `OpaqueEndpoint.Value` is a byte/string-exact user value. It must round-trip as entered. Those layers enforce only aggregate structure, required fields, field types, supported version/revision rules, duplicate/unknown-field handling, and bounded storage size. For `OpaqueEndpoint`, admission is limited to a string and a bounded storage size, including empty and whitespace values: there is no URI or upstream pre-validation, URI parsing as an admission gate, normalization, trimming, repair, rejection, reinterpretation, or write-back of a different value.

Configuration acceptance is not network authorization. In particular, local schema acceptance and resolver acceptance do not promise that a request can be formed, that a host is reachable, or that an upstream service will accept it. They also do not authorize any other route.

The formal key store remains the recommended place for API secrets: authentication is represented by `SecretRef`, protected with Windows DPAPI `CurrentUser` and product-specific versioned purpose/entropy. A profile is not rejected merely because the endpoint itself contains credentials in user-info or query text. Embedded endpoint credentials are treated as sensitive endpoint content, never as a replacement for the formal `SecretRef` design.

## 4. Request-time interpretation and failure behavior

A1 provides the closed `OpaqueEndpoint` storage, codec/schema support, redaction, and configuration resolution foundation only. It does not parse an endpoint to decide whether settings save or resolve; it contains no real Chat or Image HTTP.

Only A2 and A3 may interpret the original stored string, and only while constructing the one request for their already explicit binding. They may give that string to the .NET/HTTP stack to form a `RequestUri`; standard interpretation and canonicalization performed by that stack are permitted for that transient request representation. This request-time behavior does not alter the byte/string-exact configuration value.

If .NET/HTTP cannot interpret the value as a request URI, the adapter returns a stable redacted request-construction failure for that call. If a request is formed, the selected upstream is the final authority on request/protocol acceptance; network and upstream rejection likewise return a stable redacted failure. No adapter may append or concatenate a vendor/provider path to the endpoint: the user value is the complete request target for its explicit protocol.

Neither success nor failure may persist a normalized `RequestUri`, rewrite, normalize, disable, delete, or otherwise mutate saved configuration. It must not select a different profile, model, endpoint, adapter, protocol, channel, or fallback route. Request-time usability is therefore distinct from local configuration persistence.

Configuration save, application startup, and navigation between Create, Settings, and Preview are zero-network operations. They must not parse/probe an endpoint, resolve DNS, construct an HTTP client, perform a health check, refresh a credential, download an image, or make a paid request. Health begins as `Unknown`; that state does not prevent a user from submitting an explicit prompt. The resulting real prompt request is the first request and records the resulting health observation for its already selected route. There is no automatic Chat or Image health probe. In particular, Image must never turn health display, selection, startup, save, or navigation into an automatic or paid request; an image generation request requires its own explicit user action.

## 5. Storage, observability, import, and export

AI configuration remains current-user application data, not Unity project configuration. The aggregate stays versioned and atomically written with bounded UTF-8 serialization and a preserved `.bak`; corrupt or unsupported aggregate data fails closed rather than silently replacing user settings. This integrity handling does not become endpoint syntax validation.

Raw endpoint strings can contain sensitive query parameters or user-info. Logs, exceptions, receipts, telemetry, diagnostics, normal UI, cache keys, and default exports must show only a redacted endpoint summary and stable error code. They must never emit the raw endpoint, user-info, query, fragment, key, token, `Authorization` value, authentication header, SecretRef payload, prompt, raw request, raw response, or image bytes.

An explicit provider-profile editing surface may display the user's raw endpoint value so that the user can edit it. That exception is limited to the deliberate edit interaction and must not flow into surrounding diagnostics. Configuration export must require an explicit "include provider configuration" choice and a warning that the exported raw endpoint may contain credentials or other sensitive data; it must not include configuration by default. SecretRef payloads remain excluded from exports.

The fixed Tom design input remains [`snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`](https://github.com/snowpeak008/Tom_doc/tree/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf). Tom import is user-confirmed draft import only. A source endpoint may be carried as the same opaque user value and must be redacted in preview outside an explicit edit action. `ApiKeyProtected` is never copied, parsed, decrypted, re-encrypted, returned, logged, or persisted. Command paths, sidecar/CLI hints, cookies, and automatic protocol detection are rejected. Import cannot activate a channel binding without explicit user selection of protocol, capability/model, credential, and channel.

Future image output remains a private, untrusted artifact. It must not automatically write to Unity, `Assets`, recipes, patches, or any project path. A later explicit Desktop export remains a separately bounded action.

The Settings UI treats a secret as entry-only. It may expose a blank/password entry control and a redacted presence state, but it must never read a plaintext secret back into the UI, logs, receipts, diagnostics, or export. A new or changed binding that needs a `SecretRef` requires deliberate secret re-entry; it cannot recover a prior plaintext value, infer another profile's secret, or fall back to another credential. Explicit revoke must clear the selected binding's secret reference through the secret store, clear the UI's transient entry state, and leave that route fail-closed until the user deliberately re-enters a replacement. Endpoint text is still shown only inside the deliberate profile-edit interaction and remains redacted everywhere else.

The only Desktop stream exception is `PrivateImagePreviewDecoder`. It may consume only a provider-issued `Stream`, decode it directly into an in-memory Avalonia `Bitmap`, and close the stream immediately after decoding, including failure paths. It is not a file/cache/export/network abstraction: it must not use `File`, `Directory`, `Path`, `FileStream`, `Environment`, `System.Net`, any project path, or Unity API.

## 6. Module boundary and active-channel contract

| Module | Responsibility | Prohibited behavior |
|---|---|---|
| `VFXComposer.AI.Contracts` | Versioned channel/profile/capability/binding/request/response/diagnostic contracts, including `OpaqueEndpoint` and `IAiGateway`. | Provider transport, Desktop UI, Broker, Worker, Unity, project I/O, secret payloads. |
| `VFXComposer.AI.Providers` | Atomic settings/DPAPI store, import, fingerprint, redaction, configuration resolution, and descriptive registry skeleton. | Local endpoint rejection/parsing as a save-or-resolve gate, real A1 adapter traffic, Broker/Worker/Unity/project writes. |
| `VFXComposer.AI.Tests` | Contract, store, import, resolver, redaction, and request-time adapter-boundary tests using synthetic data/fakes. | Real credentials, real provider traffic, and project mutation. |

Broker, Worker, and Unity have no AI secret, secret-resolution, provider-configuration, or provider-network role. Desktop may use only Gateway and constrained settings-management contracts; it has no direct provider transport route.

A1 retains its published owned roots:

1. `src/VFXComposer.AI.Contracts/**`
2. `src/VFXComposer.AI.Providers/**`
3. `src/VFXComposer.AI.Tests/**`
4. `docs/schemas/desktop/vfxcomposer-ai-provider-config-v1.schema.json`
5. `VFXComposer.sln`
6. `eng/verify-phase2-schemas.py`
7. `eng/run-phase2-gate.ps1`
8. `eng/phase2-baseline-roots.json`

The A1 ownership is historical closed ownership. A2 closed with only `src/VFXComposer.AI.Contracts/Chat/**`, `src/VFXComposer.AI.Providers/Chat/**`, and `src/VFXComposer.AI.Tests/Chat/**`; A3 closed with the corresponding `Image/**` roots. A4 consumed those outputs without reopening their closed leaf ownership except for the expressly listed Chat integration overlays below.

### A4 closed ownership and final evidence

A4 added only these new roots/leaves:

1. `src/VFXComposer.AI.Contracts/Desktop/**`
2. `src/VFXComposer.AI.Providers/Desktop/**`
3. `src/VFXComposer.AI.Tests/Desktop/**`
4. `apps/VFXComposer.Desktop/Services/PrivateImagePreviewDecoder.cs`
5. `apps/VFXComposer.Desktop.Tests/AiDesktopIntegrationTests.cs`

A4 modified only these existing integration surfaces:

1. `src/VFXComposer.AI.Providers/ProviderConfigurationResolver.cs`
2. `src/VFXComposer.AI.Providers/ProviderSecretStore.cs`
3. `src/VFXComposer.AI.Providers/Chat/ChatRouteResolver.cs`
4. `src/VFXComposer.AI.Providers/Chat/ChatChannelGateway.cs`
5. `src/VFXComposer.AI.Tests/Chat/**`
6. `src/VFXComposer.AI.Tests/ProviderSafetySurfaceTests.cs`
7. `apps/VFXComposer.Desktop/VFXComposer.Desktop.csproj` and `apps/VFXComposer.Desktop/packages.lock.json`
8. `apps/VFXComposer.Desktop/App.axaml.cs`; the MainWindow, Create, Settings, and Preview view models; and the Create, Settings, and Preview views/code-behind
9. `apps/VFXComposer.Desktop.Tests/AiDesktopIntegrationTests.cs`, `apps/VFXComposer.Desktop.Tests/NoProjectAccessSurfaceTests.cs`, `apps/VFXComposer.Desktop.Tests/VFXComposer.Desktop.Tests.csproj`, and `apps/VFXComposer.Desktop.Tests/packages.lock.json`
10. `eng/run-phase2-gate.ps1` and `eng/phase2-baseline-roots.json`

This was A4's all-and-only preflight allow-list. A4 stopped rather than widen it for `src/VFXComposer.Client/**`, any Broker, Worker, Unity, project, or solution path; `src/VFXComposer.AI.Providers/Image/OpenAiCompatibleImageGateway.cs`; or `apps/VFXComposer.Desktop/Views/MainWindow.axaml`. It added no automatic health/probe/prompt/image request, raw-secret recovery, direct Desktop transport, fallback route, project access, Unity write, or mock-handler cross-channel E2E.

The A4 final record is `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. The accepted implementation commits are `fc986d11` (Desktop provider wiring), `ffc9f609` (health/secret QC remediation), and `cc5ff806` (final baseline binding). The receipt records `186/186`, AI `77/77`, Desktop `22/22`, `423/423` total tests, frozen-root replay `0`, and owned runtime/pipe/private-artifact residue `0`. This closes the opaque configuration, zero-auto-network, secret/revoke, redaction, decoder-stream, and zero Desktop project-write controls, but is not mock-handler cross-channel E2E evidence.

### A5 final closeout and A6 final audit acceptance

A5 is closed. Its all-and-only implementation scope was:

1. `tests/VFXComposer.AiLocalE2E.Tests/**`
2. `src/VFXComposer.AI.Providers/Desktop/ProviderDesktopRuntime.cs`
3. `VFXComposer.sln`
4. `eng/run-phase2-gate.ps1`
5. `eng/phase2-baseline-roots.json`

`ProviderDesktopRuntime.cs` was the only production seam. A5 retained the existing constructor and added only the optional `privateImageTempRoot` passed transparently to `ImageGateway`; production `null` behavior remained unchanged. No other production path, provider handler, Desktop UI/decoder, Client, Broker, Worker, Unity, project, or documentation/runtime path was owned by A5.

A5 used a real loopback `TcpListener` and the production runtime plus production `HttpClient` handlers. Handler injection, external-network access, and paid-provider calls were absent. Its accepted local end-to-end evidence starts with Settings CRUD, DPAPI storage, and explicit isolated bindings, then exercises Create Chat and Preview Image for both base64 and URL image results through the existing decoder. It covers restart persistence, channel isolation, opaque endpoint handling, redacted failures, explicit revoke/fail-closed behavior, private-artifact cleanup, and zero project writes.

The accepted A5 gate records exact root replay `0`, locked Release build, A5 `11/11`, product-assembly binding, unchanged tracked locks, and `vfxcomposer-a5` residue `0`. The receipt `a5-final-acceptance-14abb1d3-bootstrap` has `209` SHA-256 hashes and records no root drift, build/test/binding failure, stale private artifact, handler injection, external/paid call, cross-channel fallback, secret/prompt/raw-payload/image-byte leak, or project write. Any sixth implementation path, changed production `null` behavior, or failed cleanup remains a STOP finding.

`A6 — WP-AI-PROVIDER-FINAL-AUDIT` is `CLOSED — FINAL ACCEPTED/GO — P0/P1/P2=0/0/0`. Its accepted Git-ignored receipt is `.codex_tmp/a6-ai-provider-final-audit-20260829T000000Z/audit-summary.json`. The independent A6 gate passed `434/434`; schemas `23`; source is stable, frozen-root replay is `0`, and runtime residue is `0`; the no-argument Broker smoke had empty stdout, exact stderr `W24FS001`, and exit `23`; the approved-package feed is `39/39`; isolated restore used `18` unique ignored locks; and tracked locks are unchanged.

This final acceptance formally closes ADR-006 AI two-channel scope at `100/100`: every LLM/conversation operation uses only its `ChatLlm` binding, every image-generation operation uses only its `ImageGeneration` binding, and the channels retain independent profile, secret, and protocol with no fallback. `OpaqueEndpoint` remains arbitrary bounded text—including empty and whitespace values—saved exactly and interpreted only at request time. This acceptance does not authenticate a real paid provider and is not a production release. All A0–A6 are closed, no AI work package is active, and any later AI requirement must be issued as a new milestone.

## 7. Consequences and stop line

`A2` and `A3` are final closed GO packages, A4 is `CLOSED — FINAL GO — P0/P1/P2=0/0/0`, A5 is `CLOSED — FINAL ACCEPTED / GO — P0/P1/P2=0/0/0`, and A6 is `CLOSED — FINAL ACCEPTED/GO — P0/P1/P2=0/0/0`. The overall ADR-006 AI functionality scope is final accepted at `100/100`, with no active AI package. No later work may use endpoint acceptance to infer permission for a different route, weaken endpoint/secret/prompt/image redaction, add vendor-path construction, persist a normalized endpoint, introduce fallback, conduct background network activity, write project/Unity state, or treat A2/A3/A4 component evidence as cross-channel E2E evidence.

A5 and A6 are stopped after their accepted closeouts. The completed A6 audit found no evidence mismatch, fallback, raw-data leak, Broker/Worker/Unity provider-network role, non-loopback or paid/external traffic claim, gate/lock/cleanup-residue failure, or dirty/unexplained audit state. A6 did not repair, develop product code, rerun a product gate, or reopen A0–A5. A future requirement must create a separately named milestone; it may not reactivate this completed audit.

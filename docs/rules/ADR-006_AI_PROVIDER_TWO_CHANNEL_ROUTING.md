# ADR-006: AI provider two-channel routing

Status: `ACCEPTED — A0 ARCHITECTURE FREEZE` on `2026-08-28`. This ADR is the A0 documentation closeout and publishes A1. It adds no provider runtime, network traffic, credential, Desktop UI, Broker, Worker, Unity, project-write, or production-service capability.

Normative architecture token: `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

Depends on: ADR-005 final U6 scoped GO. This ADR does not supersede, reopen, or alter ADR-005's ordinary-user Broker/Worker route.

## 1. U6 closeout and program boundary

The independent frozen-byte receipt `u6-independent-final-audit-20260828T232640380Z` is accepted as the U6 final GO: `P0=0 / P1=0 / P2=0`. Its `summary.json` is `passed: true`; its frozen-root replay reports `0` mismatches; its point-in-time residue reports no runtime processes, VFX Composer named pipes, or owned LocalE2E temporary roots; and its source manifest records `16607` entries with SHA-256 `592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284`.

Accordingly, the ADR-005 USER_MODE main architecture is `CLOSED — FINAL GO — 100/100`. This is completion of the defined user-mode route, not an AI-provider runtime claim. The default Broker contract remains unchanged: launch with no arguments writes only `W24FS001` to stderr and exits `23`.

A0 closes in the same single documentation commit that accepts this ADR. It uses the existing U6 unified-gate evidence only; A0 does not rerun the project gate. The only A0 validation command is a documentation diff check. A0 neither adds nor accepts implementation bytes.

The post-U6 AI delivery DAG is exactly:

`A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`.

At publication, `A0` is `CLOSED`; `A1` is the sole `ACTIVE` package; `A2`, `A3`, `A4`, `A5`, and `A6` are `NOT STARTED`. No node may skip its predecessor, and A2/A3 may overlap only after A1 has frozen their disjoint ownership.

## 2. Decision: two mandatory, isolated channels

There are exactly two product AI channels:

| Channel | Sole permitted work | Mandatory binding |
|---|---|---|
| `ChatLlm` | Every LLM, chat, conversation, and text-generation operation. | The explicit `ChatLlm` channel binding only. |
| `ImageGeneration` | Every image-generation operation. | The explicit `ImageGeneration` channel binding only. |

Each channel has exactly one explicit binding to one `ProviderProfile`, one declared capability, and one explicit model. A capability therefore contains a stable capability ID and model ID; a binding cannot name a list, priority, or optional second choice. One profile may declare both a chat capability and an image capability, but the user must separately choose, validate, and display each channel binding. Sharing a profile never shares a capability, model selection, adapter, health result, request, failure, or fallback path.

Every product call enters `IAiGateway` with a versioned channel-specific request DTO. Feature callers cannot supply a profile ID, model override, endpoint, protocol, adapter, authorization/header, or fallback candidate. Before any future request is constructed, Gateway resolves the immutable settings snapshot and validates the exact channel binding, enabled profile, selected model/capability, explicit protocol, endpoint policy, and auth reference. Missing, disabled, unknown, mismatched, stale, corrupt, or unverified state fails closed with zero network traffic.

The following are forbidden:

- implicit default, last-successful, only-enabled, environment, PATH, URL, model-name, web-site-name, or import-derived routing;
- a fallback list or retry to another profile, model, endpoint, origin, adapter, or channel;
- using an image profile for chat or a chat profile for image generation;
- automatic protocol detection or an unknown protocol falling back to an OpenAI-compatible, chat, image, or other adapter.

Bounded retries, if introduced by a later adapter node, may be only for the same resolved channel, profile, model, endpoint, protocol, and request, and must remain cancellable. A failure never selects a different route.

## 3. Profile, protocol, origin, and authentication model

`ProviderProfile`, `ProtocolBinding`, `EndpointDefinition`, `AuthDescriptor(SecretRef)`, `CapabilityDefinition`, `ChannelBinding`, immutable settings snapshot, health result, and request/response diagnostics are distinct versioned concepts. Public contracts contain neither plaintext nor protected credential payloads.

`Origin` is only descriptive metadata with this closed vocabulary: `Official`, `Relay`, `Friend`, `Subscription`, and `Custom`. Origin does not select a protocol, URL shape, adapter, header, authentication mechanism, or fallback. `ProtocolId` is independently explicit, versioned, implemented, and tested; protocol cannot be inferred from Origin, a URL, a model name, an imported field, or a display name.

Only a provider's documented API-token authentication or documented OAuth flow may be supported. `Subscription` remains metadata, not permission to automate a subscription login. The implementation must not scrape cookies, browser state, or credentials; execute scripts or CLI/sidecar programs; generate arbitrary/custom header templates; load dynamic DLLs for provider access; bypass TLS validation; or use certificate-validation exceptions. There is no custom auth-header, cookie, shell, dynamic-library, or TLS-bypass extension point.

Endpoints must use HTTPS. An explicit, user-visible loopback HTTP development exception may be permitted only for a loopback host and must not send a production secret. All non-loopback HTTP is rejected.

## 4. Local storage, diagnostics, import, and image boundary

AI configuration is current-user application data, not Unity project configuration. A strict, versioned aggregate JSON document is written atomically on the target volume and preserves the previous `.bak` file. Writes use a new temporary file, bounded UTF-8 serialization, flush/replace-or-move semantics, and cleanup; reads enforce size limits, canonical/strict shape, duplicate/unknown-field rejection, and exact supported-version policy. A future version, corrupt primary/backup, unreadable file, failed recovery, or stale revision fails closed and must never silently replace user settings with an empty default.

Secrets are referred to only by `SecretRef`. The provider store protects their payloads with Windows DPAPI `CurrentUser` plus a product-specific, versioned purpose/entropy. Plaintext exists only for the shortest save or request-assembly interval, is not returned by public APIs, and is never included in profiles, settings snapshots, fingerprints, DTOs, errors, logs, receipts, telemetry, cache keys, or exports. Every configuration change creates a new revision and a non-secret configuration fingerprint; it invalidates health/verification state without activating or rebinding a channel.

Logs, receipts, diagnostics, telemetry, and exports must not contain a key, token, `Authorization` value, authentication header, SecretRef payload, prompt, raw request, raw response, provider URL, or base64/binary image data. Redacted stable diagnostic codes and limited non-sensitive state are the only allowed observability surface.

The fixed design input is [`snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`](https://github.com/snowpeak008/Tom_doc/tree/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf). Tom import is a user-confirmed, non-sensitive draft import only. It uses a strict allow-list for metadata such as display name, origin suggestion, endpoint, model, and timeout. `ApiKeyProtected` is never copied, parsed, decrypted, re-encrypted, returned, logged, or persisted. Old verification state is discarded; command paths, sidecar/CLI hints, cookies, and automatic relay-protocol detection are rejected. An imported draft cannot create an active binding until the user explicitly selects a protocol, capability/model, credential, and channel binding.

Images are private, untrusted artifacts. Future image output may enter only a verified per-user private cache, with no prompt/model/endpoint/secret in names or manifests. It must not automatically write to Unity, `Assets`, recipes, patches, or any project path. Explicit Desktop user export is a later, separately bounded action.

## 5. Module and process boundary

The implementation boundary is fixed to these three modules:

| Module | Responsibility | Prohibited dependency or behavior |
|---|---|---|
| `VFXComposer.AI.Contracts` | Pure versioned channel/profile/capability/binding/request/response/diagnostic contracts and `IAiGateway`. | Provider transport implementation, Desktop UI, Broker, Worker, Unity, project I/O, and secret payloads. |
| `VFXComposer.AI.Providers` | Strict settings/store/DPAPI/import/fingerprint/health/registry/Gateway configuration resolution. | Broker, Worker, Unity, project writes, real adapter traffic in A1, and UI controls. |
| `VFXComposer.AI.Tests` | Contract, boundary, storage, import, resolver, and redaction tests with fakes only. | Real credentials, real provider traffic, and project mutation. |

Broker, Worker, and Unity have no AI secret, secret resolution, provider configuration, or AI network responsibility. Desktop may use only the Gateway and constrained settings-management contracts; it has no direct AI `HttpClient`/transport route and no secret-handling route. No image or AI result may cross this boundary into automatic Unity/project writing.

## 6. A1 release contract

`A1` is `ACTIVE — AI_PROVIDER_FOUNDATION`. It is the only active post-U6 package. Its owned roots are exactly:

1. `src/VFXComposer.AI.Contracts/**`
2. `src/VFXComposer.AI.Providers/**`
3. `src/VFXComposer.AI.Tests/**`
4. `docs/schemas/desktop/vfxcomposer-ai-provider-config-v1.schema.json`
5. `VFXComposer.sln`
6. `eng/run-phase2-gate.ps1`
7. `eng/phase2-baseline-roots.json`

No other path is authorized. In particular, A1 must STOP rather than edit a central package-management file, a project file outside an owned root, an existing external `.csproj`, Broker, Worker, Unity, Desktop, existing stage note, ADR-005, or any unrelated source/lock/configuration file. A dependency that requires such an external central-package or `.csproj` change is a STOP condition, not implicit authorization.

A1 must deliver only the foundation: core contracts; profile/channel bindings; the configuration schema; strict versioned atomic JSON store with `.bak`; DPAPI CurrentUser SecretRef store; configuration revision/fingerprint; health and adapter-registry skeletons; safe Tom draft import; configuration resolver; and `IAiGateway`. The registry is descriptive/fail-closed only in A1. A1 must not implement real Chat or Image HTTP, a real protocol adapter, image generation/download/cache, Desktop UI wiring, Broker/Worker/Unity change, or external provider verification.

At minimum, A1 tests must cover canonical configuration; migration and future-version rejection; corrupt primary/backup recovery behavior; DPAPI no-plaintext and unreadable-payload failure; URI policy; capability/channel mismatch failure; no fallback; Tom secret exclusion; internal dependency/process boundary; and redaction. Resolver-negative tests must prove zero outbound handler invocation. Tests use synthetic data and local fakes only.

## 7. Consequences and stop line

`A2` (Chat adapter lane), `A3` (Image adapter lane), `A4` (Desktop wiring), `A5` (mock E2E), and `A6` (independent audit) remain `NOT STARTED`. A2 and A3 are the first nodes that may add their respective protocol/transport behavior, subject to a later explicit owned-root release. No future node may weaken the channel, credential, network, logging, import, cache, or Unity-write prohibitions in this ADR without a new ADR and explicit publication.

This ADR is final for A0. After its single documentation commit and clean-tree verification, work stops here except for the separately authorized A1 package.

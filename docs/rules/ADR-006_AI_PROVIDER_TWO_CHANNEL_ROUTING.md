# ADR-006: AI provider two-channel routing and user-defined endpoints

Status: `ACCEPTED — A0 ARCHITECTURE FREEZE, REBASED BY USER PRODUCT DECISION` on `2026-08-29`.

Normative architecture token: `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

This ADR is a documentation-only rebase of the post-U6 AI contract. It changes no source, runner, network behavior, credential, Desktop UI, Broker, Worker, Unity, project-write, or production-service capability. It supersedes only the earlier endpoint-admission design; ADR-005's ordinary-user Broker/Worker route remains closed and unchanged.

## 1. Program boundary and delivery state

The accepted U6 receipt remains the closeout for the USER_MODE architecture: `P0=0 / P1=0 / P2=0`, with the default Broker contract unchanged (`W24FS001` on stderr and exit `23` for no-argument launch). That final GO is not an AI-provider runtime claim.

The post-U6 AI DAG remains exactly:

`A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`.

`A0` is closed. `A1 — AI_PROVIDER_FOUNDATION` remains the sole `ACTIVE` package; `A2` through `A6` remain `NOT STARTED`. The earlier A1 review verdict `NO-GO — P0/P1/P2=0/0/1` is superseded by this user product decision, not converted into a GO and not erased as historical evidence. A1 must rework its endpoint model to `OpaqueEndpoint`, then rerun its scoped validation and independent review before it may close.

## 2. Decision: two explicit channels, no fallback

There are exactly two product AI channels:

| Channel | Sole permitted work | Mandatory binding |
|---|---|---|
| `ChatLlm` | All LLM, conversation, and text-generation operations. | The one explicit `ChatLlm` binding. |
| `ImageGeneration` | All image-generation operations. | The one explicit `ImageGeneration` binding. |

Each channel resolves exactly one explicit `ProviderProfile`, declared capability, model, and protocol. A profile may serve both channels only through separately selected capabilities and separately displayed bindings. Origin (`Official`, `Relay`, `Friend`, `Subscription`, or `Custom`) is descriptive metadata; it does not infer a protocol, request format, authentication method, adapter, model, or fallback.

Feature callers enter `IAiGateway` with channel-specific request DTOs. They cannot select a profile, model, protocol, endpoint, adapter, header, credential, or second choice. Missing, disabled, unknown, cross-channel, stale, corrupt, or unsupported binding state fails closed before network activity.

The following remain forbidden:

- implicit default, last-successful, only-enabled, environment, PATH, display-name, model-name, origin, import-derived, or URL-derived routing;
- fallback or retry to another channel, profile, capability, model, endpoint, origin, or adapter;
- automatic protocol detection, or an unknown protocol falling back to an OpenAI-compatible or any other adapter;
- cookie scraping, browser-state access, scripts, CLI/sidecars, dynamic provider DLLs, arbitrary header templates, or TLS-validation bypasses.

Bounded retries may later be added only for the already resolved one route and request. A failure never chooses a different route.

## 3. Decision: endpoint is an opaque, user-owned configuration string

`OpaqueEndpoint` is the endpoint contract. Its value is a user-supplied, user-editable configuration string that the product saves and resolves as entered. It may represent an official API, relay, friend-provided service, subscription-related custom address, or any other custom address. It may contain a scheme, host, port, user-info, path, query, fragment, or text that is not a URI at all.

The persisted configuration and its JSON Schema enforce only aggregate structure, required fields, field types, supported version/revision rules, duplicate/unknown-field handling, and bounded storage size. For `OpaqueEndpoint`, validation is limited to a string and a reasonable non-empty storage bound. It must not validate URI syntax, scheme, host, port, user-info, query, fragment, provider-specific path, or upstream service legality. It must not normalize, strip, repair, reject, or reinterpret the string during local save or configuration resolution.

Configuration acceptance is not network authorization. In particular, local schema acceptance and resolver acceptance do not promise that a request can be formed, that a host is reachable, or that an upstream service will accept it. They also do not authorize any other route.

The formal key store remains the recommended place for API secrets: authentication is represented by `SecretRef`, protected with Windows DPAPI `CurrentUser` and product-specific versioned purpose/entropy. A profile is not rejected merely because the endpoint itself contains credentials in user-info or query text. Embedded endpoint credentials are treated as sensitive endpoint content, never as a replacement for the formal `SecretRef` design.

## 4. Request-time interpretation and failure behavior

A1 must provide `OpaqueEndpoint` storage, codec/schema support, redaction, and configuration resolution only. It must not parse the value as a URI merely to decide whether settings may be saved or resolved, and it must not add real Chat or Image HTTP.

A2 and A3 are the first lanes allowed to interpret an `OpaqueEndpoint` at call time. The explicitly bound adapter makes a best-effort, protocol-specific attempt to construct its one request from the original stored string. If it cannot construct a request, encounters a network failure, or receives an upstream rejection, it returns a stable redacted failure for that call.

Such a failure must not rewrite, normalize, disable, delete, or otherwise mutate the saved configuration. It must not select a different profile, model, endpoint, adapter, protocol, channel, or fallback route. Request-time usability is therefore distinct from local configuration persistence.

## 5. Storage, observability, import, and export

AI configuration remains current-user application data, not Unity project configuration. The aggregate stays versioned and atomically written with bounded UTF-8 serialization and a preserved `.bak`; corrupt or unsupported aggregate data fails closed rather than silently replacing user settings. This integrity handling does not become endpoint syntax validation.

Raw endpoint strings can contain sensitive query parameters or user-info. Logs, exceptions, receipts, telemetry, diagnostics, normal UI, cache keys, and default exports must show only a redacted endpoint summary and stable error code. They must never emit the raw endpoint, user-info, query, fragment, key, token, `Authorization` value, authentication header, SecretRef payload, prompt, raw request, raw response, or image bytes.

An explicit provider-profile editing surface may display the user's raw endpoint value so that the user can edit it. That exception is limited to the deliberate edit interaction and must not flow into surrounding diagnostics. Configuration export must require an explicit "include provider configuration" choice and a warning that the exported raw endpoint may contain credentials or other sensitive data; it must not include configuration by default. SecretRef payloads remain excluded from exports.

The fixed Tom design input remains [`snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`](https://github.com/snowpeak008/Tom_doc/tree/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf). Tom import is user-confirmed draft import only. A source endpoint may be carried as the same opaque user value and must be redacted in preview outside an explicit edit action. `ApiKeyProtected` is never copied, parsed, decrypted, re-encrypted, returned, logged, or persisted. Command paths, sidecar/CLI hints, cookies, and automatic protocol detection are rejected. Import cannot activate a channel binding without explicit user selection of protocol, capability/model, credential, and channel.

Future image output remains a private, untrusted artifact. It must not automatically write to Unity, `Assets`, recipes, patches, or any project path. A later explicit Desktop export remains a separately bounded action.

## 6. Module boundary and A1 rework contract

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

This documentation rebase does not exercise that implementation authority: it edits only the seven named AI control documents. The subsequent A1 rework must replace endpoint admission with `OpaqueEndpoint`, retain the explicit channel binding and no-fallback invariants, and revalidate from the revised contract.

At minimum, A1 revalidation must prove:

- arbitrary bounded endpoint strings, including malformed URI-like input and strings with user-info, query, and fragments, save and resolve unchanged;
- aggregate/schema structural failures and storage-size overflow still fail correctly, without treating endpoint syntax as a structural failure;
- configuration acceptance does not invoke a network handler or claim network authorization;
- request-time adapter parsing failures, network failures, and upstream rejections return stable redacted errors with no configuration write-back and no fallback;
- redaction protects endpoint-embedded sensitive material in logs, exceptions, receipts, ordinary UI, and default export, while explicit editing and warned configuration export follow the stated consent rules;
- DPAPI/`SecretRef`, corrupt primary/backup behavior, Tom `ApiKeyProtected` exclusion, channel/capability mismatch, and Contracts/Providers/Tests/Broker/Worker/Unity/Desktop boundaries remain intact.

## 7. Consequences and stop line

`A2` (Chat adapter), `A3` (Image adapter), `A4` (Desktop wiring), `A5` (mock E2E), and `A6` (independent audit) remain `NOT STARTED`. A2/A3 may add their respective request-time adapter behavior only after A1's reworked contract is independently accepted. No later node may use endpoint acceptance to infer permission for a different route, weaken endpoint redaction, or introduce fallback.

After this documentation commit passes `git diff --check` and the worktree is clean, this rebase is `FINAL STOPPED`. Only the separately authorized A1 implementation rework may proceed.

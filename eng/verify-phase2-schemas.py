from __future__ import annotations

import copy
import json
from importlib.metadata import version
from pathlib import Path

from jsonschema import Draft202012Validator
from referencing import Registry, Resource


ROOT = Path(__file__).resolve().parents[1]
SCHEMA_ROOT = ROOT / "docs" / "schemas" / "desktop"
PROTOCOL = "vfxcomposer.protocol/1.0"
DIGEST = "sha256:" + ("a" * 64)
PHASE2_NAMES = (
    "vfxcomposer-peer-hello-v1.schema.json",
    "vfxcomposer-peer-session-accepted-v1.schema.json",
    "vfxcomposer-project-registration-attestation-v1.schema.json",
    "vfxcomposer-project-lease-v1.schema.json",
    "vfxcomposer-worker-project-handle-grant-v1.schema.json",
    "vfxcomposer-worker-project-handle-grant-ack-v1.schema.json",
    "vfxcomposer-worker-project-handle-revoke-v1.schema.json",
    "vfxcomposer-worker-project-handle-revoke-ack-v1.schema.json",
    "vfxcomposer-read-document-query-v1.schema.json",
    "vfxcomposer-read-document-result-v1.schema.json",
    "vfxcomposer-registered-project-selection-v1.schema.json",
    "vfxcomposer-worker-project-locator-v1.schema.json",
    "vfxcomposer-worker-project-locator-ack-v1.schema.json",
)
AI_PROVIDER_CONFIG_NAME = "vfxcomposer-ai-provider-config-v1.schema.json"
AI_OPAQUE_ENDPOINT_VECTOR_PATH = ROOT / "src" / "VFXComposer.AI.Tests" / "OpaqueEndpointVectors.json"


def typed(type_tag: str) -> dict:
    return {"typeTag": type_tag, "digest": DIGEST}


def strict_json_load(payload: str) -> object:
    def reject_duplicate_decoded_names(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for name, value in pairs:
            if name in result:
                raise ValueError("duplicate decoded property name")
            result[name] = value
        return result

    return json.loads(payload, object_pairs_hook=reject_duplicate_decoded_names)


def load() -> tuple[dict[str, dict], Registry]:
    schemas: dict[str, dict] = {}
    resources: list[tuple[str, Resource]] = []
    for path in sorted(SCHEMA_ROOT.glob("*.schema.json")):
        schema = json.loads(path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        schemas[path.name] = schema
        resources.append((schema["$id"], Resource.from_contents(schema)))
    if len(schemas) != 23 or set(PHASE2_NAMES) - schemas.keys() or AI_PROVIDER_CONFIG_NAME not in schemas:
        raise AssertionError("The current desktop schema set is not exact.")
    return schemas, Registry().with_resources(resources)


def main() -> None:
    schemas, registry = load()
    validators = {
        name: Draft202012Validator(schemas[name], registry=registry)
        for name in PHASE2_NAMES
    }
    diagnostic = {
        "protocolVersion": PROTOCOL,
        "messageKind": "diagnostic",
        "code": "VFXP0008",
        "severity": "ERROR",
        "message": "The requested project document is unavailable.",
        "retryable": True,
    }
    project = typed("vfxcomposer.project-identity/1")
    positives = {
        PHASE2_NAMES[0]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "peer.hello",
            "requestId": "request-01",
            "peerRole": "DESKTOP",
            "peerInstanceId": "desktop-01",
            "processId": 42,
            "processEpoch": "epoch-01",
            "offeredCapabilities": [
                "broker.peer-session.v1",
                "project.readonly-query.v1",
            ],
            "imageIdentity": typed("vfxcomposer.process-image/1"),
        },
        PHASE2_NAMES[1]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "peer.session.accepted",
            "requestId": "request-01",
            "sessionId": "session-01",
            "peerRole": "DESKTOP",
            "brokerInstanceId": "broker-01",
            "brokerGeneration": 1,
            "processEpoch": "epoch-01",
            "negotiatedCapabilities": [
                "broker.peer-session.v1",
                "project.readonly-query.v1",
            ],
        },
        PHASE2_NAMES[2]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "project.registration.attestation",
            "requestId": "request-02",
            "registeredProjectId": "project-01",
            "projectIdentity": project,
            "volumeIdentity": typed("vfxcomposer.volume-identity/1"),
            "repositoryIdentity": typed("vfxcomposer.directory-identity/1"),
            "projectRootIdentity": typed("vfxcomposer.directory-identity/1"),
            "brokerGeneration": 1,
            "registrationGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "selfHash": typed("vfxcomposer.project-registration-attestation/1"),
        },
        PHASE2_NAMES[3]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "project.lease.descriptor",
            "requestId": "request-03",
            "leaseId": "lease-01",
            "registeredProjectId": "project-01",
            "projectIdentity": project,
            "brokerGeneration": 1,
            "registrationGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "leaseGeneration": 1,
            "selfHash": typed("vfxcomposer.project-lease-descriptor/1"),
        },
        PHASE2_NAMES[4]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.handle.grant",
            "requestId": "request-03-worker",
            "leaseId": "lease-01",
            "registeredProjectId": "project-01",
            "projectIdentity": project,
            "volumeIdentity": typed("vfxcomposer.volume-identity/1"),
            "repositoryIdentity": typed("vfxcomposer.directory-identity/1"),
            "projectRootIdentity": typed("vfxcomposer.directory-identity/1"),
            "brokerGeneration": 1,
            "registrationGeneration": 1,
            "leaseGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "handleEncoding": "win-handle-u64-lower-hex/1",
            "volumeHandle": "0000000000000100",
            "repositoryHandle": "0000000000000104",
            "projectRootHandle": "0000000000000108",
            "selfHash": typed("vfxcomposer.worker-project-handle-grant/1"),
        },
        PHASE2_NAMES[5]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.handle.grant.ack",
            "requestId": "request-03-worker-ack",
            "leaseId": "lease-01",
            "brokerGeneration": 1,
            "leaseGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "grantSelfHash": typed("vfxcomposer.worker-project-handle-grant/1"),
            "disposition": "GRANT_ACCEPTED",
            "selfHash": typed("vfxcomposer.worker-project-handle-grant-ack/1"),
        },
        PHASE2_NAMES[6]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.handle.revoke",
            "requestId": "request-03-worker-revoke",
            "leaseId": "lease-01",
            "brokerGeneration": 1,
            "leaseGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "grantSelfHash": typed("vfxcomposer.worker-project-handle-grant/1"),
            "reasonCode": "LEASE_REVOKED",
            "selfHash": typed("vfxcomposer.worker-project-handle-revoke/1"),
        },
        PHASE2_NAMES[7]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.handle.revoke.ack",
            "requestId": "request-03-worker-revoke-ack",
            "leaseId": "lease-01",
            "brokerGeneration": 1,
            "leaseGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "grantSelfHash": typed("vfxcomposer.worker-project-handle-grant/1"),
            "revokeSelfHash": typed("vfxcomposer.worker-project-handle-revoke/1"),
            "disposition": "HANDLES_CLOSED",
            "selfHash": typed("vfxcomposer.worker-project-handle-revoke-ack/1"),
        },
        PHASE2_NAMES[8]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "project.document.read.query",
            "requestId": "request-04",
            "leaseId": "lease-01",
            "projectIdentity": project,
            "leaseGeneration": 1,
            "documentKind": "MANIFEST",
            "documentId": "effect_fire",
            "expectedContentHash": None,
        },
        PHASE2_NAMES[9]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "project.document.read.result",
            "requestId": "request-04",
            "accepted": False,
            "projectIdentity": project,
            "documentKind": "MANIFEST",
            "documentId": "effect_fire",
            "contentHash": None,
            "byteLength": 0,
            "contentBase64": None,
            "diagnostic": diagnostic,
        },
        PHASE2_NAMES[10]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "project.registered.selection",
            "requestId": "request-selection-01",
            "registeredProjectId": "registered-project-01",
            "projectIdentity": project,
            "brokerGeneration": 1,
            "registrationGeneration": 1,
        },
        PHASE2_NAMES[11]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.locator",
            "requestId": "request-05-locator",
            "registeredProjectId": "registered-project-01",
            "projectIdentity": project,
            "volumeIdentity": typed("vfxcomposer.volume-identity/1"),
            "repositoryIdentity": typed("vfxcomposer.directory-identity/1"),
            "projectRootIdentity": typed("vfxcomposer.directory-identity/1"),
            "brokerGeneration": 1,
            "registrationGeneration": 1,
            "enrollmentGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "selfHash": typed("vfxcomposer.worker-project-locator/1"),
        },
        PHASE2_NAMES[12]: {
            "protocolVersion": PROTOCOL,
            "messageKind": "worker.project.locator.ack",
            "requestId": "request-05-locator",
            "registeredProjectId": "registered-project-01",
            "brokerGeneration": 1,
            "registrationGeneration": 1,
            "enrollmentGeneration": 1,
            "workerSessionId": "worker-session-01",
            "workerProcessEpoch": "worker-epoch-01",
            "locatorSelfHash": typed("vfxcomposer.worker-project-locator/1"),
            "disposition": "LOCATOR_ACCEPTED",
            "selfHash": typed("vfxcomposer.worker-project-locator-ack/1"),
        },
    }

    positive_count = 0
    negative_count = 0
    for name, instance in positives.items():
        active = validators[name]
        errors = list(active.iter_errors(instance))
        if errors:
            raise AssertionError(f"{name}: {errors[0].message}")
        positive_count += 1

        for field in tuple(instance):
            missing = copy.deepcopy(instance)
            del missing[field]
            if active.is_valid(missing):
                raise AssertionError(f"{name} accepted missing {field}")
            negative_count += 1

        unknown = copy.deepcopy(instance)
        unknown["callerPath"] = "C:/untrusted"
        if active.is_valid(unknown):
            raise AssertionError(f"{name} accepted callerPath")
        negative_count += 1

        wrong_kind = copy.deepcopy(instance)
        wrong_kind["messageKind"] = "authority.grant"
        if active.is_valid(wrong_kind):
            raise AssertionError(f"{name} accepted wrong kind")
        negative_count += 1

    unsorted = copy.deepcopy(positives[PHASE2_NAMES[0]])
    unsorted["offeredCapabilities"] = [
        "project.readonly-query.v1",
        "broker.peer-session.v1",
    ]
    if validators[PHASE2_NAMES[0]].is_valid(unsorted):
        raise AssertionError("peer hello accepted an unsorted capability set")
    negative_count += 1

    leaked = copy.deepcopy(positives[PHASE2_NAMES[8]])
    leaked["projectIdentity"]["path"] = "C:/untrusted"
    if validators[PHASE2_NAMES[8]].is_valid(leaked):
        raise AssertionError("query accepted a nested project path")
    negative_count += 1

    library_query = copy.deepcopy(positives[PHASE2_NAMES[8]])
    library_query["documentKind"] = "LIBRARY_INDEX"
    library_query["documentId"] = "project"
    if not validators[PHASE2_NAMES[8]].is_valid(library_query):
        raise AssertionError("query rejected the fixed library index identity")
    positive_count += 1

    for invalid_document_id in ("../effect", "Effect", "effect.json", "effect/path"):
        malformed = copy.deepcopy(positives[PHASE2_NAMES[8]])
        malformed["documentId"] = invalid_document_id
        if validators[PHASE2_NAMES[8]].is_valid(malformed):
            raise AssertionError("query accepted a path-shaped document identity")
        negative_count += 1

    wrong_library_id = copy.deepcopy(library_query)
    wrong_library_id["documentId"] = "other"
    if validators[PHASE2_NAMES[8]].is_valid(wrong_library_id):
        raise AssertionError("query accepted a non-project library index identity")
    negative_count += 1

    mixed_result = copy.deepcopy(positives[PHASE2_NAMES[9]])
    mixed_result["contentHash"] = typed("vfxcomposer.document-content/1")
    mixed_result["contentBase64"] = "e30="
    mixed_result["byteLength"] = 2
    if validators[PHASE2_NAMES[9]].is_valid(mixed_result):
        raise AssertionError("rejected result accepted content")
    negative_count += 1

    selection = positives[PHASE2_NAMES[10]]
    selection_validator = validators[PHASE2_NAMES[10]]
    for property_name in ("brokerGeneration", "registrationGeneration"):
        for invalid_value in (0, -1, 9223372036854775808, "1"):
            malformed_selection = copy.deepcopy(selection)
            malformed_selection[property_name] = invalid_value
            if selection_validator.is_valid(malformed_selection):
                raise AssertionError(f"selection accepted invalid {property_name}")
            negative_count += 1

    wrong_selection_version = copy.deepcopy(selection)
    wrong_selection_version["protocolVersion"] = "future.selection.version"
    if selection_validator.is_valid(wrong_selection_version):
        raise AssertionError("selection accepted wrong protocol version")
    negative_count += 1

    wrong_selection_identity_type = copy.deepcopy(selection)
    wrong_selection_identity_type["projectIdentity"] = "not-a-typed-hash"
    if selection_validator.is_valid(wrong_selection_identity_type):
        raise AssertionError("selection accepted a wrong typed-identity shape")
    negative_count += 1

    wrong_selection_domain = copy.deepcopy(selection)
    wrong_selection_domain["projectIdentity"]["typeTag"] = "vfxcomposer.volume-identity/1"
    if selection_validator.is_valid(wrong_selection_domain):
        raise AssertionError("selection accepted a wrong typed-identity domain")
    negative_count += 1

    nested_selection_extra = copy.deepcopy(selection)
    nested_selection_extra["projectIdentity"]["rawPath"] = "C:/untrusted"
    if selection_validator.is_valid(nested_selection_extra):
        raise AssertionError("selection accepted a nested path-shaped extra")
    negative_count += 1

    for raw_project_id in ("C:/untrusted", "https://untrusted.example/project"):
        path_shaped_selection = copy.deepcopy(selection)
        path_shaped_selection["registeredProjectId"] = raw_project_id
        if selection_validator.is_valid(path_shaped_selection):
            raise AssertionError("selection accepted a path-shaped project id")
        negative_count += 1

    for authority_field in ("accepted", "authorityGrant"):
        authority_selection = copy.deepcopy(selection)
        authority_selection[authority_field] = True
        if selection_validator.is_valid(authority_selection):
            raise AssertionError("selection accepted an authority-shaped extra")
        negative_count += 1

    duplicate_selection = (
        '{"protocolVersion":"vfxcomposer.protocol/1.0",'
        '"messageKind":"project.registered.selection",'
        '"requestId":"request-selection-01",'
        '"\\u0072equestId":"request-selection-02"}'
    )
    try:
        strict_json_load(duplicate_selection)
    except ValueError:
        negative_count += 1
    else:
        raise AssertionError("selection accepted a duplicate decoded property name")

    locator = positives[PHASE2_NAMES[11]]
    locator_validator = validators[PHASE2_NAMES[11]]
    acknowledgement = positives[PHASE2_NAMES[12]]
    acknowledgement_validator = validators[PHASE2_NAMES[12]]
    for property_name in (
        "brokerGeneration",
        "registrationGeneration",
        "enrollmentGeneration",
    ):
        for invalid_value in (0, -1, 9223372036854775808, "1"):
            malformed_locator = copy.deepcopy(locator)
            malformed_locator[property_name] = invalid_value
            if locator_validator.is_valid(malformed_locator):
                raise AssertionError(f"locator accepted invalid {property_name}")
            negative_count += 1

            malformed_acknowledgement = copy.deepcopy(acknowledgement)
            malformed_acknowledgement[property_name] = invalid_value
            if acknowledgement_validator.is_valid(malformed_acknowledgement):
                raise AssertionError(f"locator acknowledgement accepted invalid {property_name}")
            negative_count += 1

    wrong_locator_identity_shape = copy.deepcopy(locator)
    wrong_locator_identity_shape["projectIdentity"] = "not-a-typed-hash"
    if locator_validator.is_valid(wrong_locator_identity_shape):
        raise AssertionError("locator accepted a wrong typed project-identity shape")
    negative_count += 1

    wrong_locator_identity_domain = copy.deepcopy(locator)
    wrong_locator_identity_domain["volumeIdentity"]["typeTag"] = "vfxcomposer.directory-identity/1"
    if locator_validator.is_valid(wrong_locator_identity_domain):
        raise AssertionError("locator accepted a wrong volume-identity domain")
    negative_count += 1

    nested_locator_extra = copy.deepcopy(locator)
    nested_locator_extra["projectRootIdentity"]["rawPath"] = "C:/untrusted"
    if locator_validator.is_valid(nested_locator_extra):
        raise AssertionError("locator accepted a nested path-shaped extra")
    negative_count += 1

    wrong_locator_self_hash_domain = copy.deepcopy(locator)
    wrong_locator_self_hash_domain["selfHash"]["typeTag"] = "vfxcomposer.worker-project-handle-grant/1"
    if locator_validator.is_valid(wrong_locator_self_hash_domain):
        raise AssertionError("locator accepted a handle-grant self-hash domain")
    negative_count += 1

    wrong_acknowledgement_locator_hash_domain = copy.deepcopy(acknowledgement)
    wrong_acknowledgement_locator_hash_domain["locatorSelfHash"]["typeTag"] = "vfxcomposer.worker-project-handle-grant/1"
    if acknowledgement_validator.is_valid(wrong_acknowledgement_locator_hash_domain):
        raise AssertionError("locator acknowledgement accepted a handle-grant hash domain")
    negative_count += 1

    wrong_acknowledgement_self_hash_domain = copy.deepcopy(acknowledgement)
    wrong_acknowledgement_self_hash_domain["selfHash"]["typeTag"] = "vfxcomposer.worker-project-handle-grant-ack/1"
    if acknowledgement_validator.is_valid(wrong_acknowledgement_self_hash_domain):
        raise AssertionError("locator acknowledgement accepted a handle-grant acknowledgement hash domain")
    negative_count += 1

    wrong_acknowledgement_disposition = copy.deepcopy(acknowledgement)
    wrong_acknowledgement_disposition["disposition"] = "GRANT_ACCEPTED"
    if acknowledgement_validator.is_valid(wrong_acknowledgement_disposition):
        raise AssertionError("locator acknowledgement accepted a grant disposition")
    negative_count += 1

    for raw_project_id in ("C:/untrusted", "https://untrusted.example/project"):
        path_shaped_locator = copy.deepcopy(locator)
        path_shaped_locator["registeredProjectId"] = raw_project_id
        if locator_validator.is_valid(path_shaped_locator):
            raise AssertionError("locator accepted a path-shaped project id")
        negative_count += 1

    for authority_field in ("accepted", "authorityGrant"):
        authority_acknowledgement = copy.deepcopy(acknowledgement)
        authority_acknowledgement[authority_field] = True
        if acknowledgement_validator.is_valid(authority_acknowledgement):
            raise AssertionError("locator acknowledgement accepted an authority-shaped extra")
        negative_count += 1

    duplicate_locator = (
        '{"protocolVersion":"vfxcomposer.protocol/1.0",'
        '"messageKind":"worker.project.locator",'
        '"requestId":"request-locator-01",'
        '"\\u0072equestId":"request-locator-02"}'
    )
    try:
        strict_json_load(duplicate_locator)
    except ValueError:
        negative_count += 1
    else:
        raise AssertionError("locator accepted a duplicate decoded property name")

    ai_validator = Draft202012Validator(schemas[AI_PROVIDER_CONFIG_NAME], registry=registry)
    ai_positive = {
        "formatVersion": 1,
        "revision": 1,
        "profiles": [
            {
                "id": "profile-primary",
                "displayName": "Primary provider",
                "origin": "Official",
                "enabled": True,
                "protocol": {"id": "openai-compatible-v1"},
                "endpoint": {
                    "value": "https://provider.example.invalid/v1/",
                },
                "auth": {
                    "secretRef": "secret-primary",
                    "secretScope": "Production",
                },
                "timeoutSeconds": 30,
                "capabilities": [
                    {
                        "id": "chat-main",
                        "channel": "ChatLlm",
                        "modelId": "chat-model-1",
                    }
                ],
            }
        ],
        "channelBindings": [
            {
                "channel": "ChatLlm",
                "profileId": "profile-primary",
                "capabilityId": "chat-main",
                "modelId": "chat-model-1",
            }
        ],
    }
    if not ai_validator.is_valid(ai_positive):
        raise AssertionError("AI provider schema rejected its positive fixture")

    endpoint_at_limit = "a" * 8192
    if len(endpoint_at_limit) != 8192:
        raise AssertionError("AI provider opaque-endpoint boundary fixture is invalid")
    ai_endpoint_boundary_positive = copy.deepcopy(ai_positive)
    ai_endpoint_boundary_positive["profiles"][0]["endpoint"]["value"] = endpoint_at_limit
    if not ai_validator.is_valid(ai_endpoint_boundary_positive):
        raise AssertionError("AI provider schema rejected its opaque-endpoint size boundary")

    ai_negative_count = 0
    for invalid in (
        {key: value for key, value in ai_positive.items() if key != "revision"},
        {**ai_positive, "apiKeyProtected": "must-not-be-a-schema-field"},
        {**ai_positive, "formatVersion": 2},
        {
            **ai_positive,
            "profiles": [
                {
                    **ai_positive["profiles"][0],
                    "auth": {"secretRef": "secret-primary", "apiKey": "not-allowed"},
                }
            ],
        },
        {
            **ai_positive,
            "profiles": [
                {
                    **ai_positive["profiles"][0],
                    "endpoint": {"value": 42},
                }
            ],
        },
        {
            **ai_positive,
            "profiles": [
                {
                    **ai_positive["profiles"][0],
                    "protocol": {"id": "1openai-compatible-v1"},
                }
            ],
        },
        {
            **ai_positive,
            "profiles": [
                {
                    **ai_positive["profiles"][0],
                    "endpoint": {"value": endpoint_at_limit + "a"},
                }
            ],
        },
        {
            **ai_positive,
            "profiles": [
                {
                    **ai_positive["profiles"][0],
                    "endpoint": {"value": "", "unexpected": True},
                }
            ],
        },
    ):
        if ai_validator.is_valid(invalid):
            raise AssertionError("AI provider schema accepted a negative fixture")
        ai_negative_count += 1

    opaque_endpoint_vector_payload = strict_json_load(AI_OPAQUE_ENDPOINT_VECTOR_PATH.read_text(encoding="utf-8"))
    if not isinstance(opaque_endpoint_vector_payload, dict) or opaque_endpoint_vector_payload.get("formatVersion") != 1:
        raise AssertionError("AI opaque-endpoint vector corpus is invalid")
    opaque_endpoint_vectors = opaque_endpoint_vector_payload.get("vectors")
    if not isinstance(opaque_endpoint_vectors, list) or not opaque_endpoint_vectors:
        raise AssertionError("AI opaque-endpoint vector corpus is empty")

    opaque_endpoint_vector_names: set[str] = set()
    ai_opaque_vector_count = 0
    for vector in opaque_endpoint_vectors:
        if not isinstance(vector, dict):
            raise AssertionError("AI opaque-endpoint vector is not an object")
        required = {"name", "value"}
        if set(vector) != required:
            raise AssertionError("AI opaque-endpoint vector shape is invalid")
        name = vector["name"]
        value = vector["value"]
        if (
            not isinstance(name, str)
            or not name
            or name in opaque_endpoint_vector_names
            or not isinstance(value, str)
        ):
            raise AssertionError("AI opaque-endpoint vector values are invalid")
        opaque_endpoint_vector_names.add(name)

        candidate = copy.deepcopy(ai_positive)
        candidate["profiles"][0]["endpoint"] = {"value": value}
        if not ai_validator.is_valid(candidate):
            raise AssertionError(f"AI provider schema rejected opaque endpoint vector {name}")
        ai_opaque_vector_count += 1

    print(json.dumps({
        "schema": "w24-phase2-schema-verification/1",
        "status": "PASS",
        "jsonschemaVersion": version("jsonschema"),
        "totalSchemaCount": len(schemas),
        "phase2SchemaCount": len(PHASE2_NAMES),
        "positiveCount": positive_count,
        "negativeCount": negative_count,
        "aiProviderSchemaValidation": {
            "status": "PASS",
            "positiveCount": 2 + ai_opaque_vector_count,
            "negativeCount": ai_negative_count,
            "opaqueEndpointVectorCount": len(opaque_endpoint_vectors),
        },
    }, separators=(",", ":")))


if __name__ == "__main__":
    main()

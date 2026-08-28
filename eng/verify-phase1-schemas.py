from __future__ import annotations

import copy
import itertools
import json
import re
from datetime import datetime, timezone
from importlib.metadata import version
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker
from referencing import Registry, Resource


REPO_ROOT = Path(__file__).resolve().parents[1]
SCHEMA_ROOT = REPO_ROOT / "docs" / "schemas" / "desktop"
PROTOCOL_VERSION = "vfxcomposer.protocol/1.0"
DIGEST = "sha256:" + ("a" * 64)
UTC_TIMESTAMP = re.compile(
    r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$"
)
FORMAT_CHECKER = FormatChecker()
PHASE1_NAMES = (
    "vfxcomposer-diagnostic-v1.schema.json",
    "vfxcomposer-handshake-request-v1.schema.json",
    "vfxcomposer-handshake-response-v1.schema.json",
    "vfxcomposer-machine-status-v1.schema.json",
    "vfxcomposer-visual-status-v1.schema.json",
    "vfxcomposer-user-verdict-status-v1.schema.json",
    "vfxcomposer-l3-status-v1.schema.json",
    "vfxcomposer-l4-status-v1.schema.json",
    "vfxcomposer-status-provenance-v1.schema.json",
)


@FORMAT_CHECKER.checks("date-time", raises=(TypeError, ValueError))
def is_exact_datetime(value: object) -> bool:
    if not isinstance(value, str):
        return True
    if UTC_TIMESTAMP.fullmatch(value) is None:
        return False
    parsed = datetime.fromisoformat(value[:-1] + "+00:00" if value.endswith("Z") else value)
    return parsed.utcoffset() == timezone.utc.utcoffset(parsed)


def load_schemas() -> tuple[dict[str, dict], Registry]:
    schemas: dict[str, dict] = {}
    resources: list[tuple[str, Resource]] = []
    for path in sorted(SCHEMA_ROOT.glob("*.schema.json")):
        schema = json.loads(path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        schemas[path.name] = schema
        resources.append((schema["$id"], Resource.from_contents(schema)))
    if set(PHASE1_NAMES) - schemas.keys():
        raise AssertionError("Phase 1 schema file set is not exact.")
    return (
        {name: schemas[name] for name in PHASE1_NAMES},
        Registry().with_resources(resources),
    )


def diagnostic(code: str, severity: str, message: str, retryable: bool) -> dict:
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "messageKind": "diagnostic",
        "code": code,
        "severity": severity,
        "message": message,
        "retryable": retryable,
    }


DIAGNOSTICS = [
    diagnostic("VFXP0001", "INFO", "No broker or Unity worker connection is active.", True),
    diagnostic("VFXP0002", "ERROR", "The wire message is malformed.", False),
    diagnostic("VFXP0003", "ERROR", "The protocol version is unsupported.", False),
    diagnostic("VFXP0004", "ERROR", "The wire message kind is unsupported.", False),
    diagnostic("VFXP0005", "ERROR", "The requested capability is unsupported.", False),
    diagnostic("VFXP0006", "ERROR", "Status provenance is invalid.", False),
    diagnostic("VFXP0007", "ERROR", "The project lease is unavailable or no longer current.", True),
    diagnostic("VFXP0008", "ERROR", "The requested project document is unavailable.", True),
    diagnostic("VFXP0009", "ERROR", "The project document does not match the requested content identity.", True),
]


def provenance(domain: str) -> dict:
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "statusDomain": domain,
        "sourceKind": "PHASE1_FIXTURE",
        "sourceIdentity": {
            "typeTag": "vfxcomposer.status-source/1",
            "digest": DIGEST,
        },
        "observedAtUtc": "2026-08-26T00:00:00+00:00",
    }


def validator(schema: dict, registry: Registry) -> Draft202012Validator:
    return Draft202012Validator(
        schema,
        registry=registry,
        format_checker=FORMAT_CHECKER,
    )


def assert_valid(instance: dict, active: Draft202012Validator) -> None:
    errors = list(active.iter_errors(instance))
    if errors:
        raise AssertionError(errors[0].message)


def assert_invalid(instance: dict, active: Draft202012Validator) -> None:
    if active.is_valid(instance):
        raise AssertionError("Schema accepted a negative Phase 1 fixture.")


def main() -> None:
    schemas, registry = load_schemas()
    validators = {
        name: validator(schema, registry)
        for name, schema in schemas.items()
    }

    positives: dict[str, list[dict]] = {
        "vfxcomposer-diagnostic-v1.schema.json": copy.deepcopy(DIAGNOSTICS),
        "vfxcomposer-handshake-request-v1.schema.json": [
            {
                "protocolVersion": PROTOCOL_VERSION,
                "messageKind": "handshake.request",
                "requestId": "request-01",
                "clientInstanceId": "desktop-01",
                "offeredCapabilities": ["protocol.handshake.v1", "future.offered.v1"],
            }
        ],
        "vfxcomposer-handshake-response-v1.schema.json": [],
        "vfxcomposer-status-provenance-v1.schema.json": [
            provenance(domain)
            for domain in ("MACHINE", "VISUAL", "USER_VERDICT", "L3", "L4")
        ],
    }

    capabilities = [
        "diagnostics.stable.v1",
        "protocol.handshake.v1",
        "status.snapshot.v1",
    ]
    for count in range(len(capabilities) + 1):
        for subset in itertools.combinations(capabilities, count):
            positives["vfxcomposer-handshake-response-v1.schema.json"].append(
                {
                    "protocolVersion": PROTOCOL_VERSION,
                    "messageKind": "handshake.response",
                    "requestId": "request-01",
                    "serverInstanceId": "broker-01",
                    "accepted": True,
                    "negotiatedCapabilities": list(subset),
                    "diagnostic": None,
                }
            )
    for item in DIAGNOSTICS:
        positives["vfxcomposer-handshake-response-v1.schema.json"].append(
            {
                "protocolVersion": PROTOCOL_VERSION,
                "messageKind": "handshake.response",
                "requestId": "request-01",
                "serverInstanceId": "broker-01",
                "accepted": False,
                "negotiatedCapabilities": [],
                "diagnostic": copy.deepcopy(item),
            }
        )

    status_shapes = {
        "vfxcomposer-machine-status-v1.schema.json": ("MACHINE", "PENDING", "PASSED"),
        "vfxcomposer-visual-status-v1.schema.json": ("VISUAL", "VISUAL_PENDING", "PASSED"),
        "vfxcomposer-user-verdict-status-v1.schema.json": ("USER_VERDICT", "NOT_SIGNED", "APPROVED"),
        "vfxcomposer-l3-status-v1.schema.json": ("L3", "NOT_GRANTED", "GRANTED"),
        "vfxcomposer-l4-status-v1.schema.json": ("L4", "NOT_GRANTED", "GRANTED"),
    }
    for name, (domain, untrusted_state, authority_state) in status_shapes.items():
        positives[name] = [
            {
                "protocolVersion": PROTOCOL_VERSION,
                "state": untrusted_state,
                "provenance": None,
            },
            {
                "protocolVersion": PROTOCOL_VERSION,
                "state": authority_state,
                "provenance": provenance(domain),
            },
        ]

    positive_count = 0
    negative_count = 0
    for name, instances in positives.items():
        active = validators[name]
        for instance in instances:
            assert_valid(instance, active)
            positive_count += 1

        baseline = instances[0]
        for property_name in tuple(baseline):
            missing = copy.deepcopy(baseline)
            del missing[property_name]
            assert_invalid(missing, active)
            negative_count += 1

        unknown = copy.deepcopy(baseline)
        unknown["callerPath"] = "C:/untrusted"
        assert_invalid(unknown, active)
        negative_count += 1

    diagnostic_validator = validators["vfxcomposer-diagnostic-v1.schema.json"]
    for field, value in (
        ("severity", "WARNING"),
        ("message", "Failure at /home/user/secret.json"),
        ("retryable", True),
    ):
        malformed = copy.deepcopy(DIAGNOSTICS[1])
        malformed[field] = value
        assert_invalid(malformed, diagnostic_validator)
        negative_count += 1

    response_validator = validators["vfxcomposer-handshake-response-v1.schema.json"]
    accepted = copy.deepcopy(positives["vfxcomposer-handshake-response-v1.schema.json"][0])
    for bad_capabilities in (
        ["unknown.future.v1"],
        ["protocol.handshake.v1", "protocol.handshake.v1"],
        ["status.snapshot.v1", "protocol.handshake.v1"],
    ):
        malformed = copy.deepcopy(accepted)
        malformed["negotiatedCapabilities"] = bad_capabilities
        assert_invalid(malformed, response_validator)
        negative_count += 1
    accepted["diagnostic"] = copy.deepcopy(DIAGNOSTICS[1])
    assert_invalid(accepted, response_validator)
    negative_count += 1

    rejected = copy.deepcopy(positives["vfxcomposer-handshake-response-v1.schema.json"][-1])
    rejected["negotiatedCapabilities"] = ["protocol.handshake.v1"]
    assert_invalid(rejected, response_validator)
    negative_count += 1

    provenance_validator = validators["vfxcomposer-status-provenance-v1.schema.json"]
    for mutation in (
        lambda item: item["sourceIdentity"].__setitem__("unknown", True),
        lambda item: item["sourceIdentity"].__setitem__("digest", "sha256:ABC"),
        lambda item: item.__setitem__("observedAtUtc", "not-a-dateZ"),
    ):
        malformed = provenance("MACHINE")
        mutation(malformed)
        assert_invalid(malformed, provenance_validator)
        negative_count += 1

    for name, (domain, _, authority_state) in status_shapes.items():
        active = validators[name]
        missing_authority = {
            "protocolVersion": PROTOCOL_VERSION,
            "state": authority_state,
            "provenance": None,
        }
        assert_invalid(missing_authority, active)
        negative_count += 1

        wrong_domain = "VISUAL" if domain != "VISUAL" else "MACHINE"
        cross_domain = {
            "protocolVersion": PROTOCOL_VERSION,
            "state": authority_state,
            "provenance": provenance(wrong_domain),
        }
        assert_invalid(cross_domain, active)
        negative_count += 1

    print(json.dumps({
        "schema": "w24-phase1-schema-verification/1",
        "status": "PASS",
        "jsonschemaVersion": version("jsonschema"),
        "schemaCount": len(schemas),
        "positiveCount": positive_count,
        "negativeCount": negative_count,
    }, separators=(",", ":")))


if __name__ == "__main__":
    main()

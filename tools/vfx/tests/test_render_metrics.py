"""Programmatic positive, negative, and boundary fixtures for W24 render metrics.

These are synthetic arrays only.  They are deliberately not asserted to be
Unity capture evidence or a formal machine-gate result.
"""
from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
import jsonschema

from tools.vfx.metrics.render_metrics import (
    EvidenceInvalid, _json_load_strict, _typed_binary_encode, autocorrelation,
    cleanup, fragment_tracks, mask_statistics, multiview_3d, receiver_luminance,
    main, run_report, steady_windows, trail_corridor, transition, typed_binary_hash,
)


def square(x: int, y: int, size: int = 3, shape=(24, 24)) -> np.ndarray:
    result = np.zeros(shape, dtype=np.uint8)
    result[y:y + size, x:x + size] = 255
    return result


class RenderMetricTests(unittest.TestCase):
    def test_typed_report_self_seal_vectors_and_rejections(self):
        self.assertNotEqual(typed_binary_hash(1), typed_binary_hash(1.0))
        self.assertNotEqual(typed_binary_hash(0.0), typed_binary_hash(-0.0))
        self.assertEqual(_typed_binary_encode({"é": "值", "a": "snowman ☃"}).hex(),
                         "07000000020000000161050000000b736e6f776d616e20e2988300000002c3a90500000003e580bc")
        self.assertEqual(typed_binary_hash({"é": "值", "a": "snowman ☃"}),
                         "sha256:d96fc4926441837c6b4e7cffa4a044a9348cbfdf2917eedf76cd3fa9846d83b4")
        # Shape and finite floating sample emitted by the current C metrics route.
        c_report = {"schema": "w24-render-metrics-report/v1", "route": "MEASURED", "machineGatesPassed": True,
                    "checks": [{"id": "receiver", "kind": "receiver_luminance", "pass": True, "linearLuminanceDelta": .5, "receiverPixels": 36}],
                    "inputSha256": "sha256:" + "a" * 64, "toolSha256": "sha256:" + "b" * 64,
                    "sealedReportEncoding": "w24-typed-binary-v1"}
        self.assertEqual(typed_binary_hash(c_report), "sha256:88f8b75d242347a18a0f9834d56b13c4fbd625f28600409bab2a45b571dc6c6f")
        tampered = dict(c_report); tampered["route"] = "EVIDENCE_INVALID"
        self.assertNotEqual(typed_binary_hash(c_report), typed_binary_hash(tampered))
        for invalid in (float("nan"), float("inf"), "bad\ud800"):
            with self.assertRaises(EvidenceInvalid):
                typed_binary_hash(invalid)
        for invalid_json in ('{"x": NaN}', '{"x": 1e999}', '{"x": "\\ud800"}', '{"\\ud800": 1}', '{"x": 1, "x": 2}'):
            with self.assertRaises(ValueError): _json_load_strict(invalid_json)
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw); source = root / "invalid.json"; output = root / "report.json"
            source.write_text('{"x": 1e999}', encoding="utf-8")
            self.assertEqual(main([str(source), "--output", str(output)]), 0)
            self.assertEqual(json.loads(output.read_text(encoding="utf-8"))["route"], "EVIDENCE_INVALID")

    def test_official_metrics_input_schema_requires_typed_provenance_and_accepts_ldr_receiver(self):
        schema_path = Path(__file__).parents[1] / "metrics" / "w24-render-metrics-input.schema.json"
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        digest = "sha256:" + "a" * 64
        evidence = {
            "id": "receiver-on-seed-7-main", "path": "diagnostics/receiver-on.npy", "sha256": digest,
            "kind": "diagnostic", "passId": "receiver-linear-ldr", "encoding": "linear_ldr",
            "seed": 7, "logicalFrameIndex": 12, "playerLoopSerial": 31, "playerLoopFrame": 200,
            "playerLoopTime": 3.25, "viewId": "main", "derivedFrom": "authority-camera"
        }
        valid = {
            "schema": "w24-render-metrics-input/v1", "effectId": "sustained_flame_3d", "candidateId": "C0",
            "contractRevision": 1, "contractSha256": digest, "captureProfileSha256": digest, "recorderCaptureProfileSha256": digest,
            "captureToolBundlePath": "docs/vfx-contracts/capture-tools/test.bundle.json", "captureToolBundleSha256": digest, "expectedToolSha256": digest,
            "metricsEnvironment": {"pythonExecutablePath": "C:/Python/python.exe", "pythonExecutableSha256": digest, "pythonVersion": "Python 3.11.0", "numpyVersion": "2.0.0", "pillowVersion": "10.0.0", "environmentSha256": digest},
            "requiredEvidenceMatrix": [{"evidenceId": "receiver-on-seed-7-main", "passId": "receiver-linear-ldr", "seed": 7, "viewId": "main", "logicalFrameIndex": 12}], "requiredEvidenceMatrixSha256": digest, "evidence": [evidence],
            "checks": [{"id": "receiver-ldr", "kind": "receiver_luminance_ldr", "on": evidence["id"], "off": evidence["id"], "receiverIds": evidence["id"], "effectMask": evidence["id"], "receiverId": 17, "minLinearLuminanceDelta": .2}]
        }
        jsonschema.Draft202012Validator(schema).validate(valid)
        missing_view = json.loads(json.dumps(valid)); del missing_view["evidence"][0]["viewId"]
        with self.assertRaises(jsonschema.ValidationError): jsonschema.Draft202012Validator(schema).validate(missing_view)
        missing_python_identity = json.loads(json.dumps(valid)); del missing_python_identity["metricsEnvironment"]["pythonExecutableSha256"]
        with self.assertRaises(jsonschema.ValidationError): jsonschema.Draft202012Validator(schema).validate(missing_python_identity)
        missing_seed = json.loads(json.dumps(valid)); del missing_seed["evidence"][0]["seed"]
        with self.assertRaises(jsonschema.ValidationError): jsonschema.Draft202012Validator(schema).validate(missing_seed)
        missing_matrix_evidence = json.loads(json.dumps(valid)); del missing_matrix_evidence["requiredEvidenceMatrix"][0]["evidenceId"]
        with self.assertRaises(jsonschema.ValidationError): jsonschema.Draft202012Validator(schema).validate(missing_matrix_evidence)
        wrong_kind = json.loads(json.dumps(valid)); wrong_kind["checks"][0]["kind"] = "receiver_luminance_ldr_typo"
        with self.assertRaises(jsonschema.ValidationError): jsonschema.Draft202012Validator(schema).validate(wrong_kind)

    def test_mask_and_steady_windows_positive_negative_boundary(self):
        frame = square(2, 3)
        self.assertEqual(mask_statistics(frame)["areaPixels"], 9)
        self.assertEqual(mask_statistics(frame)["centroidPx"], [3.0, 4.0])
        frames = [square(2, 2, 3) for i in range(9)]
        passing = steady_windows(frames, [[0, 1, 2], [3, 4, 5], [6, 7, 8]], {"maxAreaMeanRange": 1.0, "maxLuminanceP50Range": 0.0, "maxAbsAreaSlope": 1.0})
        self.assertTrue(passing["pass"])
        failing = steady_windows([square(0, 0, i + 1) for i in range(9)], [[0, 1, 2], [3, 4, 5], [6, 7, 8]], {"maxAreaMeanRange": 2.0, "maxAbsAreaSlope": 1.0})
        self.assertFalse(failing["pass"])

    def test_periodic_and_random_autocorrelation(self):
        periodic = [0, 1, 0, -1] * 5
        self.assertTrue(autocorrelation(periodic, 4, .7)["pass"])
        self.assertFalse(autocorrelation([0, 1, 2, 3, 4, 5], 3, .8)["pass"])
        self.assertEqual(autocorrelation([1, 2], None)["status"], "NOT_APPLICABLE_RANDOM_STEADY")

    def test_cleanup_allowed_residuals_and_components(self):
        base = {"fire": np.zeros((10, 10)), "smoke": np.zeros((10, 10))}
        after = {"fire": np.zeros((10, 10)), "smoke": square(1, 1, 4, (10, 10))}
        self.assertTrue(cleanup(base, after, ["smoke"], 0.0, 0)["pass"])
        result = cleanup(base, after, [], 0.01, 5)
        self.assertFalse(result["pass"])
        self.assertEqual(result["residualComponentAreas"], [16])
        rgba_base = np.zeros((4, 4, 4), dtype=np.uint8); rgba_after = rgba_base.copy(); rgba_after[1, 1, 3] = 255
        self.assertFalse(cleanup({"fx": rgba_base}, {"fx": rgba_after}, [], 0.0, 0)["pass"])

    def test_trail_corridor_and_stationary_head_growth(self):
        trail = np.zeros((20, 20), dtype=np.uint8); trail[10, 3:16] = 1
        points = [[x, 10] for x in range(3, 16)]
        ok = trail_corridor(trail, points, 1, .1, 1.0)
        self.assertTrue(ok["pass"])
        previous = trail.copy(); head_space = np.zeros_like(trail); trail[10, 16] = 1; head_space[10, 16] = 1
        self.assertFalse(trail_corridor(trail, points, 1, 1, .8, previous, head_space)["pass"])

    def test_transition_strategies(self):
        a, b = square(2, 2), square(3, 2)
        continuous = transition({"core": a}, {"core": b}, "continuous", {"socket": [3, 3]}, {"socket": [4, 3]}, {"minIou": .5, "maxAreaChangeRatio": 0, "maxAnchorDistancePx": 1})
        self.assertTrue(continuous["pass"])
        self.assertTrue(transition({"core": a}, {"burst": square(12, 12)}, "impulse", {"anchor": [3, 3]}, {"anchor": [3, 3]}, {"maxAnchorDistancePx": 0})["pass"])
        self.assertTrue(transition({"old": a}, {"new": b}, "replace", {}, {}, {"minIncomingAreaPixels": 9})["pass"])
        self.assertTrue(transition({"old": a}, {"old": np.zeros_like(a)}, "clear", {}, {}, {"maxRemainingAreaPixels": 0})["pass"])
        self.assertFalse(transition({"old": a}, {"old": a}, "clear", {}, {}, {"maxRemainingAreaPixels": 0})["pass"])

    def test_receiver_fragment_and_3d(self):
        off = np.zeros((12, 12, 3), dtype=float); on = off.copy(); ids = np.zeros((12, 12), dtype=int); ids[2:8, 2:8] = 7; on[2:8, 2:8] = .4
        self.assertTrue(receiver_luminance(on, off, ids, 7, np.zeros_like(ids), .3)["pass"])
        effect = np.zeros_like(ids); effect[2:8, 2:8] = 1
        with self.assertRaises(Exception): receiver_luminance(on, off, ids, 7, effect, .1)
        frames = []
        for shift in range(3):
            ids_frame = np.zeros((16, 16), int); ids_frame[2:5, 2 + shift:5 + shift] = 1; ids_frame[10:13, 10 - shift:13 - shift] = 2; frames.append(ids_frame)
        fragment = fragment_tracks(frames, [1, 2])
        self.assertEqual(fragment["authority"], "cross_evidence_only")
        self.assertTrue(fragment["pass"])
        rigid = []
        for shift in range(3):
            rigid_frame = np.zeros((20, 20), int)
            rigid_frame[2:5, 2 + shift:5 + shift] = 1
            rigid_frame[10:13, 10 + shift:13 + shift] = 2
            rigid.append(rigid_frame)
        rigid_result = fragment_tracks(rigid, [1, 2])
        self.assertFalse(rigid_result["pass"])
        self.assertTrue(rigid_result["singleRigidBodyIndication"])
        partially_correlated = []
        for shift in range(3):
            independent_frame = np.zeros((24, 24), int)
            independent_frame[2:5, 2 + shift:5 + shift] = 1
            independent_frame[7:10, 2 + shift:5 + shift] = 2
            independent_frame[14 + shift:17 + shift, 16 - shift:19 - shift] = 3
            partially_correlated.append(independent_frame)
        partially_correlated_result = fragment_tracks(partially_correlated, [1, 2, 3])
        self.assertGreaterEqual(partially_correlated_result["trajectoryCorrelation"]["maxPositive"], .98)
        self.assertFalse(partially_correlated_result["singleRigidBodyIndication"])
        self.assertTrue(partially_correlated_result["pass"])
        first_ids = np.zeros((12, 12), int); first_ids[3:8, 3:8] = 5
        second_ids = np.zeros((12, 12), int); second_ids[3:8, 5:10] = 5
        depth1 = np.zeros((12, 12)); depth1[3:8, 3:8] = np.linspace(.1, .5, 25).reshape(5, 5)
        depth2 = np.zeros((12, 12)); depth2[3:8, 5:10] = np.linspace(.2, .7, 25).reshape(5, 5)
        real = multiview_3d([{"ids": first_ids, "depth": depth1}, {"ids": second_ids, "depth": depth2}], 5, "mesh", .3, 2)
        self.assertTrue(real["pass"])
        self.assertEqual(multiview_3d([], 5, "billboard", .3, 2)["status"], "BILLBOARD_CONTRACT_EXEMPT")

    def test_full_report_hash_failure_and_beauty_rejection(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            for name, value in (("on.npy", np.ones((8, 8, 3), float)), ("off.npy", np.zeros((8, 8, 3), float)), ("ids.npy", np.ones((8, 8), int)), ("effect.npy", np.zeros((8, 8), int))): np.save(root / name, value)
            def evidence(name, kind="diagnostic", pass_id=None, encoding=None):
                data = {"id": name[:-4], "path": name, "sha256": "sha256:" + hashlib.sha256((root / name).read_bytes()).hexdigest(), "kind": kind}
                if pass_id: data["passId"] = pass_id
                if encoding: data["encoding"] = encoding
                return data
            document = {"schema": "w24-render-metrics-input/v1", "evidence": [evidence("on.npy", pass_id="receiver-linear-hdr", encoding="linear_hdr"), evidence("off.npy", pass_id="receiver-linear-hdr", encoding="linear_hdr"), evidence("ids.npy", pass_id="receiver-id", encoding="id_uint"), evidence("effect.npy", pass_id="effect-mask", encoding="mask_binary")], "checks": [{"id": "receiver", "kind": "receiver_luminance", "on": "on", "off": "off", "receiverIds": "ids", "effectMask": "effect", "receiverId": 1, "minLinearLuminanceDelta": .5}]}
            report = run_report(document, root)
            self.assertEqual(report["route"], "MEASURED")
            self.assertTrue(report["machineGatesPassed"])
            self.assertTrue(report["sealedReportHash"].startswith("sha256:"))
            document["evidence"][0]["kind"] = "beauty"
            self.assertEqual(run_report(document, root)["route"], "EVIDENCE_INVALID")
            document["evidence"][0]["kind"] = "diagnostic"; document["evidence"][0]["passId"] = "effect-mask"
            self.assertEqual(run_report(document, root)["route"], "EVIDENCE_INVALID")
            document["evidence"][0]["passId"] = "receiver-linear-hdr"; document["evidence"][0]["sha256"] = "sha256:" + "0" * 64
            self.assertEqual(run_report(document, root)["route"], "EVIDENCE_INVALID")

    def test_measured_failure_and_evidence_invalid_are_typed_sealed_non_authority_outcomes(self):
        """The frozen tool already distinguishes a measurement failure from invalid evidence.

        This test deliberately grants neither outcome authority.  It only freezes the bytes that
        a future recorder bridge revision must be allowed to preserve without rewriting them.
        """
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            off = np.zeros((8, 8, 3), dtype=np.float64)
            on = off.copy(); on[2:6, 2:6] = .25
            ids = np.zeros((8, 8), dtype=np.uint32); ids[2:6, 2:6] = 17
            effect = np.zeros((8, 8), dtype=np.uint8)
            for name, value in (("on.npy", on), ("off.npy", off), ("ids.npy", ids), ("effect.npy", effect)):
                np.save(root / name, value)

            def evidence(name, pass_id, encoding):
                return {"id": name[:-4], "path": name,
                        "sha256": "sha256:" + hashlib.sha256((root / name).read_bytes()).hexdigest(),
                        "kind": "diagnostic", "passId": pass_id, "encoding": encoding}

            document = {"schema": "w24-render-metrics-input/v1", "evidence": [
                evidence("on.npy", "receiver-linear-ldr", "linear_ldr"),
                evidence("off.npy", "receiver-linear-ldr", "linear_ldr"),
                evidence("ids.npy", "receiver-id", "id_uint"),
                evidence("effect.npy", "effect-mask", "mask_binary")],
                "checks": [{"id": "receiver-a", "kind": "receiver_luminance_ldr", "on": "on", "off": "off",
                            "receiverIds": "ids", "effectMask": "effect", "receiverId": 17,
                            "minLinearLuminanceDelta": .5}]}

            measured_failure = run_report(document, root)
            self.assertEqual(measured_failure["route"], "MEASURED")
            self.assertFalse(measured_failure["machineGatesPassed"])
            self.assertEqual([(item["id"], item["kind"], item["pass"]) for item in measured_failure["checks"]],
                             [("receiver-a", "receiver_luminance_ldr", False)])
            measured_payload = dict(measured_failure); measured_seal = measured_payload.pop("sealedReportHash")
            self.assertEqual(measured_failure["sealedReportEncoding"], "w24-typed-binary-v1")
            self.assertEqual(measured_seal, typed_binary_hash(measured_payload))

            invalid_document = json.loads(json.dumps(document))
            invalid_document["evidence"][0]["sha256"] = "sha256:" + "0" * 64
            invalid = run_report(invalid_document, root)
            self.assertEqual(invalid["route"], "EVIDENCE_INVALID")
            self.assertFalse(invalid["machineGatesPassed"])
            self.assertEqual(invalid["checks"], [])
            self.assertTrue(invalid["reason"])
            invalid_payload = dict(invalid); invalid_seal = invalid_payload.pop("sealedReportHash")
            self.assertEqual(invalid["sealedReportEncoding"], "w24-typed-binary-v1")
            self.assertEqual(invalid_seal, typed_binary_hash(invalid_payload))
            self.assertNotEqual(measured_seal, invalid_seal)

    def test_receiver_linear_ldr_requires_typed_id_and_effect_mask(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            off = np.zeros((8, 8, 3), dtype=np.float32)
            on = off.copy(); on[2:6, 2:6] = .25
            ids = np.zeros((8, 8), dtype=np.uint32); ids[2:6, 2:6] = 17
            effect = np.zeros((8, 8), dtype=np.uint8)
            for name, value in (("on.npy", on), ("off.npy", off), ("ids.npy", ids), ("effect.npy", effect)): np.save(root / name, value)
            def evidence(name, pass_id, encoding):
                return {"id": name[:-4], "path": name, "sha256": "sha256:" + hashlib.sha256((root / name).read_bytes()).hexdigest(), "kind": "diagnostic", "passId": pass_id, "encoding": encoding}
            document = {"schema": "w24-render-metrics-input/v1", "evidence": [
                evidence("on.npy", "receiver-linear-ldr", "linear_ldr"), evidence("off.npy", "receiver-linear-ldr", "linear_ldr"),
                evidence("ids.npy", "receiver-id", "id_uint"), evidence("effect.npy", "effect-mask", "mask_binary")],
                "checks": [{"id": "receiver-a", "kind": "receiver_luminance_ldr", "on": "on", "off": "off", "receiverIds": "ids", "effectMask": "effect", "receiverId": 17, "minLinearLuminanceDelta": .2}]}
            self.assertTrue(run_report(document, root)["machineGatesPassed"])
            document["evidence"][0]["encoding"] = "rgba8_png"
            self.assertEqual(run_report(document, root)["route"], "EVIDENCE_INVALID")
            document["evidence"][0]["encoding"] = "linear_ldr"; on[0, 0, 0] = 1.1; np.save(root / "on.npy", on); document["evidence"][0]["sha256"] = "sha256:" + hashlib.sha256((root / "on.npy").read_bytes()).hexdigest()
            self.assertEqual(run_report(document, root)["route"], "EVIDENCE_INVALID")

    def test_metric_pass_contract_and_array_validation_rejects_impersonation(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw); value = square(1, 1); np.save(root / "mask.npy", value)
            digest = "sha256:" + hashlib.sha256((root / "mask.npy").read_bytes()).hexdigest()
            base = {"schema": "w24-render-metrics-input/v1", "evidence": [{"id": "m", "path": "mask.npy", "sha256": digest, "kind": "beauty", "passId": "effect-mask", "encoding": "mask_binary"}], "checks": [{"kind": "mask_steady", "frames": ["m", "m", "m"], "windows": [[0], [1], [2]]}]}
            self.assertEqual(run_report(base, root)["route"], "EVIDENCE_INVALID")
            base["evidence"][0]["kind"] = "diagnostic"; base["evidence"][0]["encoding"] = "linear_hdr"
            self.assertEqual(run_report(base, root)["route"], "EVIDENCE_INVALID")
            base["evidence"][0]["encoding"] = "mask_binary"; np.save(root / "mask.npy", np.full((24, 24), np.nan)); base["evidence"][0]["sha256"] = "sha256:" + hashlib.sha256((root / "mask.npy").read_bytes()).hexdigest()
            self.assertEqual(run_report(base, root)["route"], "EVIDENCE_INVALID")


if __name__ == "__main__":
    unittest.main()

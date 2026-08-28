from __future__ import annotations

import ast
import copy
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from jsonschema import Draft202012Validator

from tools.vfx import w24_metrics_runtime_bundle as runtime_bundle


REPOSITORY = Path(__file__).resolve().parents[3]
SCHEMA_PATH = REPOSITORY / "docs/schemas/w24-s5-metrics-runtime-bundle-v1.schema.json"
SOURCE_PATH = REPOSITORY / "tools/vfx/w24_metrics_runtime_bundle.py"


def build_scaffold(spec_path: Path, output_root: Path) -> Path:
    return runtime_bundle.build_scaffold_bundle(
        spec_path,
        output_root,
        acknowledge_test_only=True,
    )


class MetricsRuntimeBundleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.sources = {name: self.root / "sources" / name for name in runtime_bundle.ROOT_IDS}
        for path in self.sources.values():
            path.mkdir(parents=True)
        self._write("python", "python.exe", b"MZ-synthetic-python-3.12.4")
        self._write("python", "Lib/json.py", b"def loads(value): return value\n")
        self._write("python", "DLLs/runtime.dll", b"MZ-synthetic-runtime")
        self._write("numpy", "numpy/__init__.py", b"__version__ = '2.4.5'\n")
        self._write("numpy", "numpy/core.pyd", b"MZ-synthetic-numpy-core")
        self._write("pillow", "PIL/__init__.py", b"__version__ = '12.2.0'\n")
        self.spec_path = self.root / "build-spec.json"
        self.output_root = self.root / "bundles"
        self.spec = {
            "schema": runtime_bundle.SPEC_SCHEMA,
            "platform": "windows",
            "architecture": "x86_64",
            "pythonVersion": "Python 3.12.4",
            "numpyVersion": "2.4.5",
            "pillowVersion": "12.2.0",
            "roots": [
                {"id": name, "path": str(self.sources[name].resolve())}
                for name in runtime_bundle.ROOT_IDS
            ],
            "entryExecutable": "bin/python.exe",
            "entryArguments": ["-I", "-s"],
            "files": [
                {
                    "rootId": "pillow",
                    "sourcePath": "PIL/__init__.py",
                    "bundlePath": "site-packages/PIL/__init__.py",
                    "kind": "pillow",
                },
                {
                    "rootId": "python",
                    "sourcePath": "python.exe",
                    "bundlePath": "bin/python.exe",
                    "kind": "python-executable",
                },
                {
                    "rootId": "numpy",
                    "sourcePath": "numpy/core.pyd",
                    "bundlePath": "site-packages/numpy/core.pyd",
                    "kind": "native-dependency",
                },
                {
                    "rootId": "python",
                    "sourcePath": "DLLs/runtime.dll",
                    "bundlePath": "bin/runtime.dll",
                    "kind": "native-dependency",
                },
                {
                    "rootId": "numpy",
                    "sourcePath": "numpy/__init__.py",
                    "bundlePath": "site-packages/numpy/__init__.py",
                    "kind": "numpy",
                },
                {
                    "rootId": "python",
                    "sourcePath": "Lib/json.py",
                    "bundlePath": "Lib/json.py",
                    "kind": "stdlib",
                },
            ],
        }
        self._write_spec(self.spec)

    def tearDown(self) -> None:
        if os.name == "nt" and self.root.exists():
            subprocess.run(
                ["icacls", str(self.root), "/reset", "/T", "/C", "/Q"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=30,
                check=False,
            )
        self.temporary.cleanup()

    def _write(self, component: str, relative: str, data: bytes) -> None:
        path = self.sources[component].joinpath(*relative.split("/"))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def _write_spec(self, value: dict) -> None:
        self.spec_path.write_text(
            json.dumps(value, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )

    def _build(self) -> tuple[Path, dict]:
        path = build_scaffold(self.spec_path, self.output_root)
        return path, runtime_bundle.verify_scaffold_bundle(path)

    def _active_pending(self) -> list[Path]:
        return [path for path in self.output_root.glob(".*.pending-*") if ".rejected-" not in path.name]

    def _mutable_clone(self, sealed: Path, name: str) -> Path:
        import shutil

        destination = self.root / ("mutable-" + name)
        shutil.copytree(sealed, destination)
        runtime_bundle.verify_scaffold_bundle(destination, expected_directory_name=None)
        return destination

    def test_build_verify_schema_and_component_tree_hashes(self) -> None:
        path, manifest = self._build()
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        Draft202012Validator(schema).validate(manifest)
        self.assertEqual(path.name, "sha256-" + manifest["bundleTypedHash"][7:])
        self.assertEqual(manifest["bundleHashEncoding"], "w24-typed-binary-v1")
        self.assertEqual(set(manifest["componentHashes"]), {"python", "numpy", "pillow"})
        self.assertEqual(manifest["bundleStatus"], "SYNTHETIC_SCAFFOLD_ONLY")
        self.assertEqual([item["path"] for item in manifest["files"]], sorted(
            (item["path"] for item in manifest["files"]), key=lambda value: value.encode("utf-8")
        ))

    def test_schema_independently_rejects_formal_sealed_status(self) -> None:
        _, manifest = self._build()
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        manifest["bundleStatus"] = "SEALED"
        self.assertFalse(Draft202012Validator(schema).is_valid(manifest))

    def test_build_is_exactly_idempotent_for_same_bytes(self) -> None:
        first, first_manifest = self._build()
        first_tree = {item.relative_to(first).as_posix(): item.read_bytes() for item in first.rglob("*") if item.is_file()}
        second = build_scaffold(self.spec_path, self.output_root)
        second_manifest = runtime_bundle.verify_scaffold_bundle(second)
        second_tree = {item.relative_to(second).as_posix(): item.read_bytes() for item in second.rglob("*") if item.is_file()}
        self.assertEqual(first, second)
        self.assertEqual(first_manifest, second_manifest)
        self.assertEqual(first_tree, second_tree)

    def test_concurrent_exact_publish_winner_is_verified_and_pending_leaves_active_namespace(self) -> None:
        import shutil

        original_rename = Path.rename
        published = False

        def publish_then_report_collision(source: Path, target: Path) -> Path:
            nonlocal published
            if not published and source.name.startswith(".sha256-"):
                published = True
                shutil.copytree(source, target)
                raise FileExistsError("synthetic exact concurrent publisher")
            return original_rename(source, target)

        with mock.patch.object(Path, "rename", new=publish_then_report_collision):
            path = build_scaffold(self.spec_path, self.output_root)
        runtime_bundle.verify_scaffold_bundle(path)
        self.assertTrue(published)
        self.assertEqual(self._active_pending(), [])
        self.assertEqual(len(list(self.output_root.glob(".*.pending-*.rejected-*"))), 1)

    def test_post_publish_verification_failure_quarantines_owned_target(self) -> None:
        original_verify = runtime_bundle.verify_scaffold_bundle

        def fail_only_published(
            path: Path,
            *,
            expected_directory_name: str | None = "content-addressed",
            require_readonly_acl: bool | None = None,
        ) -> dict:
            if (
                expected_directory_name == "content-addressed"
                and require_readonly_acl is not False
                and path.name.startswith("sha256-")
            ):
                raise runtime_bundle.RuntimeBundleError("synthetic post-publish verification failure")
            return original_verify(
                path,
                expected_directory_name=expected_directory_name,
                require_readonly_acl=require_readonly_acl,
            )

        with mock.patch.object(runtime_bundle, "verify_scaffold_bundle", side_effect=fail_only_published):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        self.assertEqual(list(self.output_root.glob("sha256-*")), [])
        rejected = list(self.output_root.glob(".sha256-*.rejected-*"))
        self.assertEqual(len(rejected), 1)
        original_verify(rejected[0], expected_directory_name=None)

    def test_shared_typed_binary_vectors_match_render_metrics(self) -> None:
        value = {"é": "值", "a": "snowman ☃"}
        self.assertEqual(
            runtime_bundle._typed_binary_encode(value).hex(),
            "07000000020000000161050000000b736e6f776d616e20e2988300000002c3a90500000003e580bc",
        )
        self.assertEqual(
            runtime_bundle._typed_hash(value),
            "sha256:d96fc4926441837c6b4e7cffa4a044a9348cbfdf2917eedf76cd3fa9846d83b4",
        )
        self.assertNotEqual(runtime_bundle._typed_hash(1), runtime_bundle._typed_hash(1.0))
        self.assertNotEqual(runtime_bundle._typed_hash(0.0), runtime_bundle._typed_hash(-0.0))

    def test_payload_manifest_and_extra_tree_tampering_are_rejected(self) -> None:
        sealed, _ = self._build()
        for mutation in ("payload", "manifest", "extra"):
            with self.subTest(mutation=mutation):
                path = self._mutable_clone(sealed, mutation)
                if mutation == "payload":
                    (path / "Lib/json.py").write_bytes(b"tampered")
                elif mutation == "manifest":
                    manifest_path = path / "runtime-bundle.json"
                    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                    manifest["numpyVersion"] = "0.0.0"
                    manifest_path.write_text(json.dumps(manifest, separators=(",", ":")), encoding="utf-8")
                else:
                    (path / "undeclared.bin").write_bytes(b"extra")
                with self.assertRaises(runtime_bundle.RuntimeBundleError):
                    runtime_bundle.verify_scaffold_bundle(path, expected_directory_name=None)
                runtime_bundle.verify_scaffold_bundle(sealed)

    def test_spec_is_exact_and_component_paths_are_fail_closed(self) -> None:
        mutations = []
        extra = copy.deepcopy(self.spec)
        extra["unexpected"] = True
        mutations.append(extra)
        relative_root = copy.deepcopy(self.spec)
        relative_root["roots"][0]["path"] = "relative-python"
        mutations.append(relative_root)
        duplicate_root = copy.deepcopy(self.spec)
        duplicate_root["roots"][1]["path"] = duplicate_root["roots"][0]["path"]
        mutations.append(duplicate_root)
        unsafe_output = copy.deepcopy(self.spec)
        unsafe_output["files"][0]["bundlePath"] = "../escape.py"
        mutations.append(unsafe_output)
        wrong_component = copy.deepcopy(self.spec)
        wrong_component["files"][0]["rootId"] = "numpy"
        mutations.append(wrong_component)
        duplicate_source = copy.deepcopy(self.spec)
        duplicate_source["files"].append(copy.deepcopy(duplicate_source["files"][0]))
        duplicate_source["files"][-1]["bundlePath"] = "site-packages/PIL/copy.py"
        mutations.append(duplicate_source)
        bad_python = copy.deepcopy(self.spec)
        bad_python["pythonVersion"] = "Python 3.13.0"
        mutations.append(bad_python)
        for index, value in enumerate(mutations):
            with self.subTest(index=index):
                self._write_spec(value)
                with self.assertRaises(runtime_bundle.RuntimeBundleError):
                    build_scaffold(self.spec_path, self.output_root)

    def test_strict_json_rejects_duplicate_nonfinite_overflow_and_surrogate(self) -> None:
        invalid = (
            b'{"x":1,"x":2}',
            b'{"x":NaN}',
            b'{"x":1e999}',
            b'{"x":"\\ud800"}',
            b'{"\\udc00":1}',
            b"[" * 65 + b"0" + b"]" * 65,
            b'{"x":' + b"1" * 129 + b"}",
        )
        for data in invalid:
            with self.subTest(data=data):
                with self.assertRaises(runtime_bundle.RuntimeBundleError):
                    runtime_bundle.strict_json_load_bytes(data, "fixture")

    def test_json_node_limit_accepts_boundary_and_rejects_boundary_plus_one(self) -> None:
        # One array node plus 99,999 scalar nodes is the exact 100,000-node limit.
        accepted = b"[" + b",".join(b"0" for _ in range(99_999)) + b"]"
        self.assertEqual(len(runtime_bundle.strict_json_load_bytes(accepted, "node boundary")), 99_999)
        rejected = b"[" + b",".join(b"0" for _ in range(100_000)) + b"]"
        with self.assertRaisesRegex(runtime_bundle.RuntimeBundleError, "node bound"):
            runtime_bundle.strict_json_load_bytes(rejected, "node boundary plus one")

    def test_source_change_during_copy_aborts_and_preserves_rejected_pending(self) -> None:
        original_copy = runtime_bundle._copy_file_exclusive
        changed = False

        def mutate_then_copy(source: Path, destination: Path, source_root: Path, destination_root: Path) -> None:
            nonlocal changed
            if not changed:
                changed = True
                source.write_bytes(source.read_bytes() + b"-changed")
            original_copy(source, destination, source_root, destination_root)

        with mock.patch.object(runtime_bundle, "_copy_file_exclusive", side_effect=mutate_then_copy):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        self.assertEqual(self._active_pending(), [])
        self.assertEqual(len(list(self.output_root.glob(".*.pending-*.rejected-*"))), 1)

    def test_cleanup_failure_is_not_allowed_to_replace_original_build_error(self) -> None:
        original = runtime_bundle.RuntimeBundleError("primary synthetic copy failure")
        cleanup = runtime_bundle.RuntimeBundleError("secondary preservation failure")
        with mock.patch.object(runtime_bundle, "_copy_file_exclusive", side_effect=original), mock.patch.object(
            runtime_bundle, "_preserve_owned_staging", side_effect=cleanup
        ):
            with self.assertRaises(runtime_bundle.RuntimeBundleError) as captured:
                build_scaffold(self.spec_path, self.output_root)
        self.assertIs(captured.exception, original)
        self.assertTrue(any("secondary preservation failure" in note for note in captured.exception.__notes__))

    def test_windows_drive_ads_reserved_and_shape_paths_are_rejected_before_copy(self) -> None:
        invalid_paths = (
            "C:escape.bin",
            "C:/escape.bin",
            "x:y",
            "name:stream",
            "CON",
            "NUL.txt",
            "dir/trailing.",
            "dir//double",
            "dir/segment with space",
            "dir/",
            "a" * (runtime_bundle.MAX_SEGMENT_CHARS + 1),
            "/".join("d" for _ in range(runtime_bundle.MAX_DEPTH + 1)) + "/file.bin",
        )
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        relative_validator = Draft202012Validator(schema["$defs"]["relativePath"])
        for invalid_path in invalid_paths:
            with self.subTest(path=invalid_path):
                value = copy.deepcopy(self.spec)
                value["files"][0]["bundlePath"] = invalid_path
                self._write_spec(value)
                with mock.patch.object(runtime_bundle, "_copy_file_exclusive") as copy_file:
                    with self.assertRaises(runtime_bundle.RuntimeBundleError):
                        build_scaffold(self.spec_path, self.output_root)
                    copy_file.assert_not_called()
                self.assertFalse(relative_validator.is_valid(invalid_path))
        for valid_path in ("bin/python.exe", ".hidden", "site-packages/PIL/__init__.py"):
            with self.subTest(valid_path=valid_path):
                self.assertTrue(relative_validator.is_valid(valid_path))

    def test_unc_roots_and_output_are_rejected_without_network_access(self) -> None:
        value = copy.deepcopy(self.spec)
        value["roots"][0]["path"] = r"\\server\share\python"
        self._write_spec(value)
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            build_scaffold(self.spec_path, self.output_root)
        self._write_spec(self.spec)
        external_paths = (
            r"\\server\share\bundle",
            r"\\?\C:\bundle",
            r"\\.\PhysicalDrive0",
            r"C:\bundle:stream",
        )
        for external in external_paths:
            with self.subTest(path=external), mock.patch.object(
                runtime_bundle, "_reject_reparse_chain"
            ) as reparse, mock.patch.object(runtime_bundle, "_read_bounded_file") as read, mock.patch.object(
                runtime_bundle, "_reject_network_path"
            ) as drive_check, mock.patch.object(Path, "absolute") as absolute, mock.patch.object(
                Path, "stat"
            ) as path_stat, mock.patch.object(Path, "exists") as exists:
                with self.assertRaises(runtime_bundle.RuntimeBundleError):
                    runtime_bundle.verify_scaffold_bundle(Path(external))
                reparse.assert_not_called()
                read.assert_not_called()
                drive_check.assert_not_called()
                absolute.assert_not_called()
                path_stat.assert_not_called()
                exists.assert_not_called()
        with mock.patch.object(Path, "absolute") as absolute, mock.patch.object(
            Path, "stat"
        ) as path_stat, mock.patch.object(Path, "exists") as exists, mock.patch.object(
            runtime_bundle, "_read_bounded_file"
        ) as read:
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, Path(r"\\server\share\bundles"))
            absolute.assert_not_called()
            path_stat.assert_not_called()
            exists.assert_not_called()
            read.assert_not_called()

    def test_absolute_build_spec_ads_is_rejected_before_any_path_io(self) -> None:
        with mock.patch.object(runtime_bundle, "_reject_reparse_chain") as reparse, mock.patch.object(
            runtime_bundle, "_read_bounded_file"
        ) as read, mock.patch.object(runtime_bundle, "_reject_network_path") as drive_check, mock.patch.object(
            Path, "absolute"
        ) as absolute, mock.patch.object(Path, "stat") as path_stat, mock.patch.object(Path, "exists") as exists:
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(Path(r"C:\spec.json:payload"), self.output_root)
            reparse.assert_not_called()
            read.assert_not_called()
            drive_check.assert_not_called()
            absolute.assert_not_called()
            path_stat.assert_not_called()
            exists.assert_not_called()

    @unittest.skipUnless(os.name == "nt", "Mapped-drive detection is a Windows-only gate.")
    def test_mapped_drive_type_is_rejected_by_volume_identity(self) -> None:
        class FakeCall:
            def __init__(self, result: int):
                self.result = result
                self.argtypes = None
                self.restype = None

            def __call__(self, *args: object) -> int:
                return self.result

        class FakeKernel:
            GetDriveTypeW = FakeCall(4)  # DRIVE_REMOTE
            GetVolumePathNameW = FakeCall(1)

        import ctypes

        with mock.patch.object(ctypes, "WinDLL", return_value=FakeKernel()):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                runtime_bundle._reject_network_path(self.root, "mapped fixture")

    def test_external_same_byte_hardlink_payload_is_rejected(self) -> None:
        sealed, _ = self._build()
        path = self._mutable_clone(sealed, "hardlink")
        payload = path / "Lib/json.py"
        external = self.root / "external-same-bytes.py"
        external.write_bytes(payload.read_bytes())
        payload.unlink()
        try:
            os.link(external, payload)
        except (OSError, NotImplementedError):
            self.skipTest("This filesystem does not permit creating a hard link.")
        self.assertGreaterEqual(payload.stat().st_nlink, 2)
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            runtime_bundle.verify_scaffold_bundle(path, expected_directory_name=None)

    def test_production_publish_verify_and_lease_fail_closed(self) -> None:
        path, _ = self._build()
        with self.assertRaisesRegex(runtime_bundle.RuntimeBundleError, "publication is unavailable"):
            runtime_bundle.build_bundle(self.spec_path, self.output_root)
        with self.assertRaisesRegex(runtime_bundle.RuntimeBundleError, "verification is unavailable"):
            runtime_bundle.verify_bundle(path)
        with self.assertRaisesRegex(runtime_bundle.RuntimeBundleError, "lease is unavailable"):
            runtime_bundle.acquire_verified_bundle_lease(path)
        self.assertFalse(hasattr(runtime_bundle, "VerifiedRuntimeBundleLease"))

    @unittest.skipUnless(os.name == "nt", "Protected ACL drift is a Windows-only gate.")
    def test_published_acl_drift_is_rejected(self) -> None:
        path, _ = self._build()
        result = subprocess.run(
            ["icacls", str(path), "/inheritance:e", "/Q"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            runtime_bundle.verify_scaffold_bundle(path)

    def test_empty_directories_and_noncanonical_manifest_bytes_are_rejected(self) -> None:
        sealed, _ = self._build()
        path = self._mutable_clone(sealed, "directory")
        (path / "undeclared-empty").mkdir()
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            runtime_bundle.verify_scaffold_bundle(path, expected_directory_name=None)
        (path / "undeclared-empty").rmdir()

        manifest_path = path / "runtime-bundle.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            runtime_bundle.verify_scaffold_bundle(path, expected_directory_name=None)

    def test_source_reparse_is_rejected_when_platform_allows_creation(self) -> None:
        link = self.sources["python"] / "Lib/linked.py"
        try:
            os.symlink(self.sources["python"] / "Lib/json.py", link)
        except (OSError, NotImplementedError):
            self.skipTest("This host does not permit an unprivileged file symlink.")
        value = copy.deepcopy(self.spec)
        value["files"].append({
            "rootId": "python",
            "sourcePath": "Lib/linked.py",
            "bundlePath": "Lib/linked.py",
            "kind": "stdlib",
        })
        self._write_spec(value)
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            build_scaffold(self.spec_path, self.output_root)

    def test_source_identity_change_during_copy_is_rechecked_and_not_published(self) -> None:
        original_copy = runtime_bundle._copy_file_exclusive
        original_has_reparse = runtime_bundle._has_reparse
        changed = False
        blocked = (self.sources["python"] / "Lib/json.py").resolve()

        def copy_then_change_identity(source: Path, destination: Path, source_root: Path, destination_root: Path) -> None:
            nonlocal changed
            original_copy(source, destination, source_root, destination_root)
            if source == blocked:
                changed = True

        def identify_after_copy(path: Path) -> bool:
            return (changed and path == blocked) or original_has_reparse(path)

        with mock.patch.object(runtime_bundle, "_copy_file_exclusive", side_effect=copy_then_change_identity), mock.patch.object(
            runtime_bundle, "_has_reparse", side_effect=identify_after_copy
        ):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        self.assertEqual(self._active_pending(), [])
        self.assertEqual(len(list(self.output_root.glob(".*.pending-*.rejected-*"))), 1)

    def test_reparse_and_resource_bounds_have_deterministic_fail_closed_paths(self) -> None:
        original_has_reparse = runtime_bundle._has_reparse
        blocked = self.sources["python"] / "Lib/json.py"

        def identify_blocked(path: Path) -> bool:
            return path == blocked or original_has_reparse(path)

        with mock.patch.object(runtime_bundle, "_has_reparse", side_effect=identify_blocked):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        with mock.patch.object(runtime_bundle, "MAX_FILE_BYTES", 4):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        with mock.patch.object(runtime_bundle, "MAX_TOTAL_BYTES", 16):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)
        with mock.patch.object(runtime_bundle, "MAX_JSON_BYTES", 4):
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                runtime_bundle.strict_json_load_bytes(b'{"x":1}', "bounded fixture")
            with self.assertRaises(runtime_bundle.RuntimeBundleError):
                build_scaffold(self.spec_path, self.output_root)

        unrelated = self.root / "not-owned-staging"
        unrelated.mkdir()
        with self.assertRaises(runtime_bundle.RuntimeBundleError):
            runtime_bundle._preserve_owned_staging(unrelated)
        self.assertTrue(unrelated.is_dir())

    def test_cli_failures_are_evidence_invalid_and_do_not_publish(self) -> None:
        value = copy.deepcopy(self.spec)
        value["entryArguments"] = ["-s"]
        self._write_spec(value)
        self.assertEqual(
            runtime_bundle._cli(["build", "--spec", str(self.spec_path), "--output-root", str(self.output_root)]),
            2,
        )
        self.assertFalse(self.output_root.exists())

    def test_tool_has_no_process_network_package_or_path_discovery_surface(self) -> None:
        tree = ast.parse(SOURCE_PATH.read_text(encoding="utf-8"))
        imported: set[str] = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imported.update(alias.name.split(".")[0] for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imported.add(node.module.split(".")[0])
        self.assertTrue(imported.isdisjoint({"subprocess", "socket", "urllib", "http", "requests", "pip"}))
        source = SOURCE_PATH.read_text(encoding="utf-8")
        self.assertNotIn("shutil.which", source)
        self.assertNotIn("os.environ", source)


if __name__ == "__main__":
    unittest.main()

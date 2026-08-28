"""Synthetic scaffold for a content-addressed W24 metrics runtime bundle.

The builder deliberately does not discover Python from PATH, import packages
from the ambient process, invoke pip, or access the network.  A caller must
provide three explicit source roots and an exact file allow-list.  The output
only exercises manifest and copy mechanics.  This process has no isolated gate
principal or handle-bound directory publication, so it MUST NOT publish or
lease a production runtime and grants no machine, QA, user, or execution
authority.
"""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
import math
import os
import re
import stat
import struct
import sys
import uuid
from pathlib import Path
from typing import Any, Iterable


SPEC_SCHEMA = "w24-s5-metrics-runtime-bundle-build-spec/1"
BUNDLE_SCHEMA = "w24-s5-metrics-runtime-bundle/1"
FILE_SET_SCHEMA = "w24-s5-runtime-file-set/1"
COMPONENT_FILE_SET_SCHEMA = "w24-s5-runtime-component-file-set/1"
HASH_ENCODING = "w24-typed-binary-v1"
_TYPED_DOMAIN = b"w24-typed-binary-v1\0"

MAX_JSON_BYTES = 32 * 1024 * 1024
MAX_FILES = 20_000
MAX_FILE_BYTES = 128 * 1024 * 1024
MAX_TOTAL_BYTES = 2 * 1024 * 1024 * 1024
MAX_PATH_CHARS = 512
MAX_SEGMENT_CHARS = 128
MAX_DEPTH = 12

HASH_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
TOKEN_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,95}$")
PYTHON_VERSION_RE = re.compile(r"^Python 3\.12\.[0-9]{1,3}$")
SAFE_SEGMENT_RE = re.compile(r"^[A-Za-z0-9_.+-]+$")
WINDOWS_RESERVED_STEM_RE = re.compile(r"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", re.IGNORECASE)
ROOT_IDS = ("python", "numpy", "pillow")
FILE_KINDS = {
    "python-executable",
    "python-runtime",
    "stdlib",
    "native-dependency",
    "numpy",
    "pillow",
}
MANIFEST_FIELDS = {
    "schema",
    "bundleStatus",
    "platform",
    "architecture",
    "pythonVersion",
    "numpyVersion",
    "pillowVersion",
    "entryCommandPolicy",
    "componentHashes",
    "files",
    "fileSetTypedHash",
    "bundleHashEncoding",
    "bundleTypedHash",
}
POLICY_FIELDS = {
    "executable",
    "fixedArguments",
    "ambientPathAllowed",
    "userSiteAllowed",
    "networkAllowed",
    "packageMutationAllowed",
}
FILE_FIELDS = {"path", "sha256", "byteLength", "component", "kind"}
SPEC_FIELDS = {
    "schema",
    "platform",
    "architecture",
    "pythonVersion",
    "numpyVersion",
    "pillowVersion",
    "roots",
    "entryExecutable",
    "entryArguments",
    "files",
}
ROOT_FIELDS = {"id", "path"}
SPEC_FILE_FIELDS = {"rootId", "sourcePath", "bundlePath", "kind"}


class RuntimeBundleError(ValueError):
    """A fail-closed build or verification error."""


def _reject_constant(value: str) -> None:
    raise RuntimeBundleError(f"Non-finite JSON constant is forbidden: {value}")


def _parse_bounded_int(value: str) -> int:
    if len(value) > 128:
        raise RuntimeBundleError("JSON integer token exceeds the lexical bound.")
    return int(value)


def _parse_bounded_float(value: str) -> float:
    if len(value) > 128:
        raise RuntimeBundleError("JSON floating-point token exceeds the lexical bound.")
    return float(value)


def _pairs_no_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise RuntimeBundleError(f"Duplicate JSON property is forbidden: {key}")
        result[key] = value
    return result


def _validate_unicode_and_numbers(value: Any, *, nodes: list[int] | None = None, depth: int = 0) -> None:
    if nodes is None:
        nodes = [0]
    nodes[0] += 1
    if nodes[0] > 100_000 or depth > 64:
        raise RuntimeBundleError("JSON exceeds the structural bound.")
    if isinstance(value, str):
        if len(value) > 1024 * 1024:
            raise RuntimeBundleError("JSON string exceeds the bound.")
        index = 0
        while index < len(value):
            code = ord(value[index])
            if 0xD800 <= code <= 0xDBFF:
                if index + 1 >= len(value) or not 0xDC00 <= ord(value[index + 1]) <= 0xDFFF:
                    raise RuntimeBundleError("JSON contains an unpaired UTF-16 surrogate.")
                index += 2
                continue
            if 0xDC00 <= code <= 0xDFFF:
                raise RuntimeBundleError("JSON contains an unpaired UTF-16 surrogate.")
            index += 1
    elif isinstance(value, float) and not math.isfinite(value):
        raise RuntimeBundleError("JSON contains a non-finite number.")
    elif isinstance(value, dict):
        for key, child in value.items():
            _validate_unicode_and_numbers(key, nodes=nodes, depth=depth + 1)
            _validate_unicode_and_numbers(child, nodes=nodes, depth=depth + 1)
    elif isinstance(value, list):
        for child in value:
            _validate_unicode_and_numbers(child, nodes=nodes, depth=depth + 1)


def _preflight_json_structure(text: str, label: str) -> None:
    """Bound nesting and token count before CPython's recursive JSON decoder runs."""
    index = 0
    depth = 0
    nodes = 0
    length = len(text)

    def add_node() -> None:
        nonlocal nodes
        nodes += 1
        if nodes > 100_000:
            raise RuntimeBundleError(f"{label} exceeds the pre-parse node bound.")

    while index < length:
        char = text[index]
        if char.isspace() or char in ",:":
            index += 1
            continue
        if char in "[{":
            add_node()
            depth += 1
            if depth > 64:
                raise RuntimeBundleError(f"{label} exceeds the pre-parse depth bound.")
            index += 1
            continue
        if char in "]}":
            depth -= 1
            if depth < 0:
                raise RuntimeBundleError(f"{label} has an invalid closing delimiter.")
            index += 1
            continue
        if char == '"':
            add_node()
            index += 1
            while index < length:
                current = text[index]
                if current == '"':
                    index += 1
                    break
                if current == "\\":
                    index += 1
                    if index >= length:
                        raise RuntimeBundleError(f"{label} has a truncated JSON escape.")
                    if text[index] == "u":
                        if index + 4 >= length:
                            raise RuntimeBundleError(f"{label} has a truncated Unicode escape.")
                        index += 5
                    else:
                        index += 1
                    continue
                index += 1
            else:
                raise RuntimeBundleError(f"{label} has an unterminated JSON string.")
            continue
        add_node()
        start = index
        while index < length and not text[index].isspace() and text[index] not in ",:[]{}":
            index += 1
        if index == start:
            raise RuntimeBundleError(f"{label} has an invalid JSON token.")
    if depth != 0:
        raise RuntimeBundleError(f"{label} has unbalanced JSON containers.")


def strict_json_load_bytes(data: bytes, label: str) -> Any:
    if len(data) > MAX_JSON_BYTES:
        raise RuntimeBundleError(f"{label} exceeds {MAX_JSON_BYTES} bytes.")
    try:
        text = data.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        raise RuntimeBundleError(f"{label} is not strict UTF-8.") from error
    _preflight_json_structure(text, label)
    try:
        value = json.loads(
            text,
            object_pairs_hook=_pairs_no_duplicates,
            parse_constant=_reject_constant,
            parse_float=_parse_bounded_float,
            parse_int=_parse_bounded_int,
        )
    except (json.JSONDecodeError, UnicodeError, ValueError, RecursionError) as error:
        if isinstance(error, RuntimeBundleError):
            raise
        raise RuntimeBundleError(f"{label} is not strict JSON: {error}") from error
    _validate_unicode_and_numbers(value)
    return value


def _normalize_windows_final_path(value: str) -> str:
    if value.startswith("\\\\?\\UNC\\"):
        return "\\\\" + value[8:]
    if value.startswith("\\\\?\\"):
        return value[4:]
    return value


def _assert_open_path_is_rooted(final_path: str, allowed_root: Path | None, label: str) -> None:
    if allowed_root is None:
        return
    expected = os.path.abspath(str(allowed_root))
    actual = os.path.abspath(_normalize_windows_final_path(final_path))
    try:
        common = os.path.commonpath((expected, actual))
    except ValueError as error:
        raise RuntimeBundleError(f"{label} opened outside its pinned root.") from error
    if os.path.normcase(common) != os.path.normcase(expected):
        raise RuntimeBundleError(f"{label} opened outside its pinned root.")


@contextlib.contextmanager
def _open_pinned_read(path: Path, allowed_root: Path | None, label: str) -> Iterable[Any]:
    if os.name == "nt":
        import ctypes
        import msvcrt
        from ctypes import wintypes

        class ByHandleFileInformation(ctypes.Structure):
            _fields_ = [
                ("dwFileAttributes", wintypes.DWORD),
                ("ftCreationTime", wintypes.FILETIME),
                ("ftLastAccessTime", wintypes.FILETIME),
                ("ftLastWriteTime", wintypes.FILETIME),
                ("dwVolumeSerialNumber", wintypes.DWORD),
                ("nFileSizeHigh", wintypes.DWORD),
                ("nFileSizeLow", wintypes.DWORD),
                ("nNumberOfLinks", wintypes.DWORD),
                ("nFileIndexHigh", wintypes.DWORD),
                ("nFileIndexLow", wintypes.DWORD),
            ]

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        create_file = kernel32.CreateFileW
        create_file.argtypes = [
            wintypes.LPCWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPVOID,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.HANDLE,
        ]
        create_file.restype = wintypes.HANDLE
        handle = create_file(
            str(path),
            0x80000000,  # GENERIC_READ
            0x00000001,  # FILE_SHARE_READ: deny writers and deletion while pinned
            None,
            3,  # OPEN_EXISTING
            0x00200000 | 0x08000000,  # OPEN_REPARSE_POINT | SEQUENTIAL_SCAN
            None,
        )
        invalid = ctypes.c_void_p(-1).value
        if handle == invalid:
            raise RuntimeBundleError(f"{label} is missing or cannot be pinned for read.")
        close_handle = kernel32.CloseHandle
        close_handle.argtypes = [wintypes.HANDLE]
        close_handle.restype = wintypes.BOOL
        try:
            information = ByHandleFileInformation()
            get_information = kernel32.GetFileInformationByHandle
            get_information.argtypes = [wintypes.HANDLE, ctypes.POINTER(ByHandleFileInformation)]
            get_information.restype = wintypes.BOOL
            if not get_information(handle, ctypes.byref(information)):
                raise RuntimeBundleError(f"Cannot inspect pinned identity for {label}.")
            if information.dwFileAttributes & (0x00000010 | 0x00000400) or information.nNumberOfLinks != 1:
                raise RuntimeBundleError(f"{label} must be a single-link non-reparse regular file.")
            get_final_path = kernel32.GetFinalPathNameByHandleW
            get_final_path.argtypes = [wintypes.HANDLE, wintypes.LPWSTR, wintypes.DWORD, wintypes.DWORD]
            get_final_path.restype = wintypes.DWORD
            required = get_final_path(handle, None, 0, 0)
            if required == 0 or required > 32768:
                raise RuntimeBundleError(f"Cannot resolve pinned final path for {label}.")
            buffer = ctypes.create_unicode_buffer(required + 1)
            if get_final_path(handle, buffer, len(buffer), 0) == 0:
                raise RuntimeBundleError(f"Cannot resolve pinned final path for {label}.")
            _assert_open_path_is_rooted(buffer.value, allowed_root, label)
            descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY)
            handle = None
            with os.fdopen(descriptor, "rb", closefd=True) as stream:
                yield stream
        finally:
            if handle not in (None, invalid):
                close_handle(handle)
        return

    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags)
    except OSError as error:
        raise RuntimeBundleError(f"{label} is missing or cannot be pinned for read.") from error
    try:
        details = os.fstat(descriptor)
        if not stat.S_ISREG(details.st_mode) or details.st_nlink != 1:
            raise RuntimeBundleError(f"{label} must be a single-link regular file.")
        _assert_open_path_is_rooted(os.path.realpath(path), allowed_root, label)
        with os.fdopen(descriptor, "rb", closefd=False) as stream:
            yield stream
    finally:
        os.close(descriptor)


@contextlib.contextmanager
def _open_exclusive_write(path: Path, allowed_root: Path, label: str) -> Iterable[Any]:
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags, 0o600)
    except OSError as error:
        raise RuntimeBundleError(f"{label} cannot be created exclusively.") from error
    try:
        details = os.fstat(descriptor)
        if not stat.S_ISREG(details.st_mode) or details.st_nlink != 1:
            raise RuntimeBundleError(f"{label} is not a unique regular file.")
        final_path = os.path.realpath(path)
        _assert_open_path_is_rooted(final_path, allowed_root, label)
        with os.fdopen(descriptor, "wb", closefd=False) as stream:
            yield stream
    finally:
        os.close(descriptor)


def _read_bounded_file(path: Path, limit: int, label: str, allowed_root: Path | None = None) -> bytes:
    try:
        with _open_pinned_read(path, allowed_root, label) as stream:
            details = os.fstat(stream.fileno())
            if not stat.S_ISREG(details.st_mode) or details.st_nlink != 1:
                raise RuntimeBundleError(f"{label} must be a single-link regular file.")
            data = stream.read(limit + 1)
    except OSError as error:
        raise RuntimeBundleError(f"{label} is missing or unreadable.") from error
    if len(data) > limit:
        raise RuntimeBundleError(f"{label} exceeds {limit} bytes.")
    return data


def _canonical_bytes(value: Any) -> bytes:
    _validate_unicode_and_numbers(value)
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
        allow_nan=False,
    ).encode("utf-8", errors="strict")


def _typed_binary_encode(value: Any) -> bytes:
    """Encode the shared W24 structural typed-binary-v1 payload."""
    output = bytearray()
    nodes = 0

    def u32(number: int) -> None:
        if number < 0 or number > 0xFFFFFFFF:
            raise RuntimeBundleError("Typed canonical length/count is invalid.")
        output.extend(number.to_bytes(4, "big"))

    def utf8(text: str) -> bytes:
        try:
            encoded = text.encode("utf-8", "strict")
        except UnicodeEncodeError as error:
            raise RuntimeBundleError("Typed canonical string contains a lone surrogate.") from error
        if len(encoded) > 1024 * 1024:
            raise RuntimeBundleError("Typed canonical string exceeds the UTF-8 byte bound.")
        return encoded

    def with_length(data: bytes) -> None:
        u32(len(data))
        output.extend(data)

    def encode(item: Any, depth: int) -> None:
        nonlocal nodes
        if depth > 64:
            raise RuntimeBundleError("Typed canonical value exceeds the depth bound.")
        nodes += 1
        if nodes > 100_000:
            raise RuntimeBundleError("Typed canonical value exceeds the node bound.")
        if item is None:
            output.append(0)
        elif isinstance(item, bool):
            output.append(2 if item else 1)
        elif isinstance(item, int):
            output.append(3)
            with_length(str(item).encode("ascii"))
        elif isinstance(item, float):
            if not math.isfinite(item):
                raise RuntimeBundleError("Typed canonical double must be finite.")
            output.append(4)
            output.extend(struct.pack(">d", item))
        elif isinstance(item, str):
            output.append(5)
            with_length(utf8(item))
        elif isinstance(item, (list, tuple)):
            if len(item) > 100_000:
                raise RuntimeBundleError("Typed canonical array exceeds the item bound.")
            output.append(6)
            u32(len(item))
            for child in item:
                encode(child, depth + 1)
        elif isinstance(item, dict):
            if len(item) > 100_000:
                raise RuntimeBundleError("Typed canonical object exceeds the field bound.")
            entries: list[tuple[bytes, Any]] = []
            for key, child in item.items():
                if not isinstance(key, str):
                    raise RuntimeBundleError("Typed canonical object keys must be strings.")
                entries.append((utf8(key), child))
            entries.sort(key=lambda entry: entry[0])
            output.append(7)
            u32(len(entries))
            for key, child in entries:
                with_length(key)
                encode(child, depth + 1)
        else:
            raise RuntimeBundleError(f"Typed canonical encoding rejects {type(item).__name__}.")

    encode(value, 0)
    return bytes(output)


def _typed_hash(value: Any) -> str:
    return _hash_bytes(_TYPED_DOMAIN + _typed_binary_encode(value))


def _hash_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _hash_file(path: Path, allowed_root: Path | None = None) -> tuple[str, int]:
    digest = hashlib.sha256()
    total = 0
    try:
        with _open_pinned_read(path, allowed_root, f"file {path}") as stream:
            details = os.fstat(stream.fileno())
            if not stat.S_ISREG(details.st_mode) or details.st_nlink != 1:
                raise RuntimeBundleError(f"File must be a single-link regular file: {path}")
            while True:
                block = stream.read(1024 * 1024)
                if not block:
                    break
                total += len(block)
                if total > MAX_FILE_BYTES:
                    raise RuntimeBundleError(f"File exceeds {MAX_FILE_BYTES} bytes: {path}")
                digest.update(block)
    except OSError as error:
        raise RuntimeBundleError(f"File is missing or unreadable: {path}") from error
    return "sha256:" + digest.hexdigest(), total


def _exact_object(value: Any, fields: set[str], label: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != fields:
        raise RuntimeBundleError(f"{label} must have the exact fields: {','.join(sorted(fields))}")
    return value


def _required_token(value: Any, label: str) -> str:
    if not isinstance(value, str) or not TOKEN_RE.fullmatch(value):
        raise RuntimeBundleError(f"{label} must be a bounded token.")
    return value


def _required_python_version(value: Any, label: str) -> str:
    if not isinstance(value, str) or not PYTHON_VERSION_RE.fullmatch(value):
        raise RuntimeBundleError(f"{label} must identify a bounded Python 3.12 patch release.")
    return value


def _required_hash(value: Any, label: str) -> str:
    if not isinstance(value, str) or not HASH_RE.fullmatch(value):
        raise RuntimeBundleError(f"{label} must be a canonical SHA-256 token.")
    return value


def _safe_relative(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or len(value) > MAX_PATH_CHARS:
        raise RuntimeBundleError(f"{label} must be a bounded relative path.")
    if "\\" in value or ":" in value or value.startswith("/") or "//" in value:
        raise RuntimeBundleError(f"{label} must use normalized forward slashes.")
    parts = value.split("/")
    if len(parts) > MAX_DEPTH or any(
        not part
        or part in {".", ".."}
        or len(part) > MAX_SEGMENT_CHARS
        or not SAFE_SEGMENT_RE.fullmatch(part)
        or part.endswith(".")
        or WINDOWS_RESERVED_STEM_RE.fullmatch(part.split(".", 1)[0])
        for part in parts
    ):
        raise RuntimeBundleError(f"{label} contains an unsafe segment.")
    return value


def _checked_child(root: Path, relative: str, label: str) -> Path:
    relative = _safe_relative(relative, label)
    root_absolute = Path(os.path.abspath(root))
    candidate = Path(os.path.abspath(root_absolute.joinpath(*relative.split("/"))))
    try:
        common = os.path.commonpath((str(root_absolute), str(candidate)))
    except ValueError as error:
        raise RuntimeBundleError(f"{label} escapes its owned filesystem root.") from error
    if os.path.normcase(common) != os.path.normcase(str(root_absolute)):
        raise RuntimeBundleError(f"{label} escapes its owned filesystem root.")
    return candidate


def _reject_external_path_lexically(value: str, label: str) -> None:
    """Pure lexical gate run before Path normalization or filesystem access."""
    if not isinstance(value, str) or not value or "\x00" in value:
        raise RuntimeBundleError(f"{label} must be a non-empty local path.")
    normalized = value.replace("/", "\\")
    upper = normalized.upper()
    if (
        normalized.startswith("\\\\")
        or normalized.startswith("\\")
        or upper.startswith(("\\\\?\\", "\\\\.\\", "\\??\\"))
        or "GLOBALROOT" in upper
    ):
        raise RuntimeBundleError(
            f"{label} must be a lexical local filesystem path, not UNC/device/extended storage."
        )
    colon_indexes = [index for index, character in enumerate(normalized) if character == ":"]
    if colon_indexes and not (
        len(colon_indexes) == 1
        and colon_indexes[0] == 1
        and normalized[0].isalpha()
    ):
        raise RuntimeBundleError(f"{label} must not contain an alternate data stream or device namespace.")
    if colon_indexes and (len(normalized) < 3 or normalized[2] != "\\"):
        raise RuntimeBundleError(f"{label} must not use a drive-relative path.")


def _reject_network_path(path: Path, label: str) -> None:
    """Lexically reject external namespaces, then check only the drive type."""
    raw = str(path)
    _reject_external_path_lexically(raw, label)
    if os.name != "nt":
        return

    import ctypes
    from ctypes import wintypes

    absolute = os.path.abspath(raw)
    absolute_drive, _ = os.path.splitdrive(absolute)
    if not re.fullmatch(r"[A-Za-z]:", absolute_drive):
        raise RuntimeBundleError(f"{label} must resolve to a local drive-letter volume.")
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.GetDriveTypeW.argtypes = [wintypes.LPCWSTR]
    kernel32.GetDriveTypeW.restype = wintypes.UINT
    drive_root = absolute_drive + "\\"
    if kernel32.GetDriveTypeW(drive_root) != 3:  # DRIVE_FIXED
        raise RuntimeBundleError(f"{label} must reside on a fixed local volume.")

    # Deliberately do not probe the supplied path here. Reparse/stat inspection
    # is a later phase, after this drive-letter/fixed-volume check succeeds.


def _windows_readonly_acl() -> tuple[Any, Any, bytes]:
    import ctypes
    from ctypes import wintypes

    class Acl(ctypes.Structure):
        _fields_ = [
            ("AclRevision", wintypes.BYTE),
            ("Sbz1", wintypes.BYTE),
            ("AclSize", wintypes.WORD),
            ("AceCount", wintypes.WORD),
            ("Sbz2", wintypes.WORD),
        ]

    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    convert = advapi32.ConvertStringSecurityDescriptorToSecurityDescriptorW
    convert.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.POINTER(wintypes.LPVOID), ctypes.POINTER(wintypes.DWORD)]
    convert.restype = wintypes.BOOL
    descriptor = wintypes.LPVOID()
    if not convert("D:P(A;;GRGXWD;;;OW)(A;;FA;;;SY)", 1, ctypes.byref(descriptor), None):
        raise RuntimeBundleError("Cannot construct the protected read-only runtime ACL.")
    get_dacl = advapi32.GetSecurityDescriptorDacl
    get_dacl.argtypes = [wintypes.LPVOID, ctypes.POINTER(wintypes.BOOL), ctypes.POINTER(wintypes.LPVOID), ctypes.POINTER(wintypes.BOOL)]
    get_dacl.restype = wintypes.BOOL
    present = wintypes.BOOL()
    defaulted = wintypes.BOOL()
    acl_pointer = wintypes.LPVOID()
    if not get_dacl(descriptor, ctypes.byref(present), ctypes.byref(acl_pointer), ctypes.byref(defaulted)) or not present:
        ctypes.windll.kernel32.LocalFree(descriptor)
        raise RuntimeBundleError("Cannot read the protected runtime ACL template.")
    acl = ctypes.cast(acl_pointer, ctypes.POINTER(Acl)).contents
    return descriptor, acl_pointer, ctypes.string_at(acl_pointer, acl.AclSize)


def _seal_private_staging_acl(path: Path) -> None:
    if os.name != "nt":
        return
    import ctypes
    from ctypes import wintypes

    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    convert = advapi32.ConvertStringSecurityDescriptorToSecurityDescriptorW
    convert.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.POINTER(wintypes.LPVOID), ctypes.POINTER(wintypes.DWORD)]
    convert.restype = wintypes.BOOL
    descriptor = wintypes.LPVOID()
    if not convert("D:P(A;OICI;FA;;;OW)(A;OICI;FA;;;SY)", 1, ctypes.byref(descriptor), None):
        raise RuntimeBundleError("Cannot construct the private staging ACL.")
    get_dacl = advapi32.GetSecurityDescriptorDacl
    get_dacl.argtypes = [wintypes.LPVOID, ctypes.POINTER(wintypes.BOOL), ctypes.POINTER(wintypes.LPVOID), ctypes.POINTER(wintypes.BOOL)]
    get_dacl.restype = wintypes.BOOL
    present = wintypes.BOOL()
    defaulted = wintypes.BOOL()
    acl_pointer = wintypes.LPVOID()
    try:
        if not get_dacl(descriptor, ctypes.byref(present), ctypes.byref(acl_pointer), ctypes.byref(defaulted)) or not present:
            raise RuntimeBundleError("Cannot read the private staging ACL template.")
        set_security = advapi32.SetNamedSecurityInfoW
        set_security.argtypes = [
            wintypes.LPWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPVOID,
            wintypes.LPVOID,
            wintypes.LPVOID,
            wintypes.LPVOID,
        ]
        set_security.restype = wintypes.DWORD
        if set_security(str(path), 1, 0x00000004 | 0x80000000, None, None, acl_pointer, None) != 0:
            raise RuntimeBundleError("Cannot apply the private staging ACL.")
    finally:
        ctypes.windll.kernel32.LocalFree(descriptor)


def _seal_readonly_acl(root: Path) -> None:
    if os.name != "nt":
        return
    import ctypes
    from ctypes import wintypes

    files, directories = _walk_exact_tree(root)
    paths = [_checked_child(root, item, "ACL payload") for item in files]
    paths.extend(_checked_child(root, item, "ACL directory") for item in sorted(directories, key=lambda item: -item.count("/")))
    paths.append(root)
    descriptor, acl_pointer, _ = _windows_readonly_acl()
    try:
        advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
        set_security = advapi32.SetNamedSecurityInfoW
        set_security.argtypes = [
            wintypes.LPWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPVOID,
            wintypes.LPVOID,
            wintypes.LPVOID,
            wintypes.LPVOID,
        ]
        set_security.restype = wintypes.DWORD
        for path in paths:
            status = set_security(str(path), 1, 0x00000004 | 0x80000000, None, None, acl_pointer, None)
            if status != 0:
                raise RuntimeBundleError(f"Cannot seal protected read-only ACL on runtime path: {path}")
    finally:
        ctypes.windll.kernel32.LocalFree(descriptor)


def _verify_readonly_acl(root: Path) -> None:
    if os.name != "nt":
        return
    import ctypes
    from ctypes import wintypes

    class Acl(ctypes.Structure):
        _fields_ = [
            ("AclRevision", wintypes.BYTE),
            ("Sbz1", wintypes.BYTE),
            ("AclSize", wintypes.WORD),
            ("AceCount", wintypes.WORD),
            ("Sbz2", wintypes.WORD),
        ]

    files, directories = _walk_exact_tree(root)
    paths = [_checked_child(root, item, "ACL payload") for item in files]
    paths.extend(_checked_child(root, item, "ACL directory") for item in directories)
    paths.append(root)
    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    get_security = advapi32.GetNamedSecurityInfoW
    get_security.argtypes = [
        wintypes.LPWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.LPVOID),
        ctypes.POINTER(wintypes.LPVOID),
        ctypes.POINTER(wintypes.LPVOID),
        ctypes.POINTER(wintypes.LPVOID),
        ctypes.POINTER(wintypes.LPVOID),
    ]
    get_security.restype = wintypes.DWORD
    get_control = advapi32.GetSecurityDescriptorControl
    get_control.argtypes = [wintypes.LPVOID, ctypes.POINTER(wintypes.WORD), ctypes.POINTER(wintypes.DWORD)]
    get_control.restype = wintypes.BOOL
    convert_to_string = advapi32.ConvertSecurityDescriptorToStringSecurityDescriptorW
    convert_to_string.argtypes = [
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.LPWSTR),
        ctypes.POINTER(wintypes.DWORD),
    ]
    convert_to_string.restype = wintypes.BOOL
    for path in paths:
        actual_descriptor = wintypes.LPVOID()
        actual_acl_pointer = wintypes.LPVOID()
        status = get_security(
            str(path),
            1,
            0x00000004,
            None,
            None,
            ctypes.byref(actual_acl_pointer),
            None,
            ctypes.byref(actual_descriptor),
        )
        if status != 0:
            raise RuntimeBundleError(f"Cannot read protected ACL from runtime path: {path}")
        try:
            control = wintypes.WORD()
            revision = wintypes.DWORD()
            if not get_control(actual_descriptor, ctypes.byref(control), ctypes.byref(revision)) or not control.value & 0x1000:
                raise RuntimeBundleError(f"Runtime ACL is not inheritance-protected: {path}")
            actual_acl = ctypes.cast(actual_acl_pointer, ctypes.POINTER(Acl)).contents
            if actual_acl.AceCount != 2:
                raise RuntimeBundleError(f"Runtime ACL has an unexpected ACE count: {path}")
            string_pointer = wintypes.LPWSTR()
            if not convert_to_string(actual_descriptor, 1, 0x00000004, ctypes.byref(string_pointer), None):
                raise RuntimeBundleError(f"Cannot canonicalize runtime ACL: {path}")
            try:
                dacl_sddl = ctypes.wstring_at(string_pointer)
            finally:
                ctypes.windll.kernel32.LocalFree(string_pointer)
            if not re.fullmatch(r"D:P(?:AI)?\(A;;0x1600a9;;;OW\)\(A;;FA;;;SY\)", dacl_sddl):
                raise RuntimeBundleError(f"Runtime ACL differs from the frozen read-only policy: {path}")
        finally:
            ctypes.windll.kernel32.LocalFree(actual_descriptor)


def _has_reparse(path: Path) -> bool:
    try:
        if path.is_symlink():
            return True
        is_junction = getattr(path, "is_junction", None)
        if is_junction is not None and is_junction():
            return True
        attributes = getattr(os.lstat(path), "st_file_attributes", 0)
        return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))
    except OSError as error:
        raise RuntimeBundleError(f"Cannot inspect path identity: {path}") from error


def _reject_reparse_chain(path: Path, label: str) -> None:
    absolute = path.absolute()
    existing: list[Path] = []
    cursor = absolute
    while True:
        if cursor.exists() or cursor.is_symlink():
            existing.append(cursor)
        if cursor.parent == cursor:
            break
        cursor = cursor.parent
    for item in reversed(existing):
        if _has_reparse(item):
            raise RuntimeBundleError(f"{label} traverses a reparse point: {item}")


def _rooted_file(root: Path, relative: str, label: str) -> Path:
    relative = _safe_relative(relative, label)
    candidate = root.joinpath(*relative.split("/"))
    _reject_reparse_chain(candidate, label)
    try:
        resolved_root = root.resolve(strict=True)
        resolved = candidate.resolve(strict=True)
    except OSError as error:
        raise RuntimeBundleError(f"{label} is missing or unreadable: {candidate}") from error
    try:
        common = os.path.commonpath((str(resolved_root), str(resolved)))
    except ValueError as error:
        raise RuntimeBundleError(f"{label} escapes its explicit source root.") from error
    if os.path.normcase(common) != os.path.normcase(str(resolved_root)) or not resolved.is_file():
        raise RuntimeBundleError(f"{label} escapes its explicit source root or is not a file.")
    return resolved


def _parse_spec(spec_path: Path) -> dict[str, Any]:
    _reject_reparse_chain(spec_path, "build spec")
    data = _read_bounded_file(spec_path, MAX_JSON_BYTES, "build spec", spec_path.parent)
    spec = _exact_object(strict_json_load_bytes(data, "build spec"), SPEC_FIELDS, "build spec")
    if spec["schema"] != SPEC_SCHEMA or spec["platform"] != "windows" or spec["architecture"] != "x86_64":
        raise RuntimeBundleError("Build spec schema/platform/architecture is unsupported.")
    _required_python_version(spec["pythonVersion"], "build spec pythonVersion")
    for field in ("numpyVersion", "pillowVersion"):
        _required_token(spec[field], f"build spec {field}")
    if spec["entryArguments"] != ["-I", "-s"]:
        raise RuntimeBundleError("Entry arguments must be exactly -I -s.")
    entry = _safe_relative(spec["entryExecutable"], "entry executable")
    if not entry.endswith(".exe"):
        raise RuntimeBundleError("The Windows entry executable must have an .exe suffix.")

    roots_value = spec["roots"]
    if not isinstance(roots_value, list) or len(roots_value) != 3:
        raise RuntimeBundleError("Build spec must contain exactly python/numpy/pillow roots.")
    roots: dict[str, Path] = {}
    resolved_root_keys: set[str] = set()
    for index, token in enumerate(roots_value):
        item = _exact_object(token, ROOT_FIELDS, f"root[{index}]")
        root_id = item["id"]
        if root_id not in ROOT_IDS or root_id in roots or not isinstance(item["path"], str):
            raise RuntimeBundleError("Build roots must be unique explicit python/numpy/pillow paths.")
        _reject_external_path_lexically(item["path"], f"{root_id} root")
        root = Path(item["path"])
        if not root.is_absolute():
            raise RuntimeBundleError("Build roots must be explicit absolute paths.")
        root = root.absolute()
        _reject_network_path(root, f"{root_id} root")
        _reject_reparse_chain(root, f"{root_id} root")
        if not root.is_dir():
            raise RuntimeBundleError(f"Explicit {root_id} root does not exist.")
        resolved_root = root.resolve(strict=True)
        resolved_key = os.path.normcase(str(resolved_root))
        if resolved_key in resolved_root_keys:
            raise RuntimeBundleError("Build component roots must identify three distinct directories.")
        resolved_root_keys.add(resolved_key)
        roots[root_id] = resolved_root
    if set(roots) != set(ROOT_IDS):
        raise RuntimeBundleError("Build roots must be exactly python/numpy/pillow.")

    files_value = spec["files"]
    if not isinstance(files_value, list) or not 1 <= len(files_value) <= MAX_FILES:
        raise RuntimeBundleError("Build file allow-list is empty or exceeds the count bound.")
    seen_bundle: set[str] = set()
    seen_source: set[tuple[str, str]] = set()
    records: list[dict[str, Any]] = []
    total = 0
    kinds: set[str] = set()
    executable_count = 0
    for index, token in enumerate(files_value):
        item = _exact_object(token, SPEC_FILE_FIELDS, f"file[{index}]")
        root_id = item["rootId"]
        if root_id not in roots:
            raise RuntimeBundleError("File record uses an undeclared root.")
        source = _rooted_file(roots[root_id], item["sourcePath"], f"file[{index}].sourcePath")
        source_key = (root_id, os.path.normcase(str(source)))
        if source_key in seen_source:
            raise RuntimeBundleError("Build source files must be unique within each component.")
        seen_source.add(source_key)
        bundle_path = _safe_relative(item["bundlePath"], f"file[{index}].bundlePath")
        if bundle_path == "runtime-bundle.json" or bundle_path in seen_bundle:
            raise RuntimeBundleError("Bundle file paths must be unique and cannot replace the manifest.")
        seen_bundle.add(bundle_path)
        kind = item["kind"]
        if kind not in FILE_KINDS:
            raise RuntimeBundleError("File record has an unsupported kind.")
        if (
            (kind in {"python-executable", "python-runtime", "stdlib"} and root_id != "python")
            or (kind == "numpy" and root_id != "numpy")
            or (kind == "pillow" and root_id != "pillow")
        ):
            raise RuntimeBundleError("File kind does not match its explicit component root.")
        kinds.add(kind)
        file_hash, byte_length = _hash_file(source, roots[root_id])
        total += byte_length
        if total > MAX_TOTAL_BYTES:
            raise RuntimeBundleError("Runtime source set exceeds the aggregate byte bound.")
        if kind == "python-executable":
            executable_count += 1
            if bundle_path != entry:
                raise RuntimeBundleError("The unique python executable must equal entryExecutable.")
        records.append(
            {
                "source": source,
                "sourceRoot": roots[root_id],
                "path": bundle_path,
                "sha256": file_hash,
                "byteLength": byte_length,
                "component": root_id,
                "kind": kind,
            }
        )
    if executable_count != 1 or not {"stdlib", "numpy", "pillow"}.issubset(kinds):
        raise RuntimeBundleError("Bundle needs exactly one executable and explicit stdlib/numpy/pillow bytes.")
    records.sort(key=lambda value: value["path"].encode("utf-8"))
    spec["_entry"] = entry
    spec["_records"] = records
    return spec


def _manifest_from_spec(spec: dict[str, Any]) -> dict[str, Any]:
    files = [
        {
            "path": item["path"],
            "sha256": item["sha256"],
            "byteLength": item["byteLength"],
            "component": item["component"],
            "kind": item["kind"],
        }
        for item in spec["_records"]
    ]
    file_set = {"schema": FILE_SET_SCHEMA, "files": files}
    component_hashes = {
        component: _typed_hash(
            {
                "schema": COMPONENT_FILE_SET_SCHEMA,
                "component": component,
                "files": [item for item in files if item["component"] == component],
            }
        )
        for component in ROOT_IDS
    }
    manifest: dict[str, Any] = {
        "schema": BUNDLE_SCHEMA,
        "bundleStatus": "SYNTHETIC_SCAFFOLD_ONLY",
        "platform": "windows",
        "architecture": "x86_64",
        "pythonVersion": spec["pythonVersion"],
        "numpyVersion": spec["numpyVersion"],
        "pillowVersion": spec["pillowVersion"],
        "entryCommandPolicy": {
            "executable": spec["_entry"],
            "fixedArguments": ["-I", "-s"],
            "ambientPathAllowed": False,
            "userSiteAllowed": False,
            "networkAllowed": False,
            "packageMutationAllowed": False,
        },
        "componentHashes": component_hashes,
        "files": files,
        "fileSetTypedHash": _typed_hash(file_set),
        "bundleHashEncoding": HASH_ENCODING,
    }
    manifest["bundleTypedHash"] = _typed_hash(manifest)
    return manifest


def _write_exclusive(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def _copy_file_exclusive(source: Path, destination: Path, source_root: Path, destination_root: Path) -> None:
    total = 0
    try:
        with _open_pinned_read(source, source_root, "runtime source") as input_stream, _open_exclusive_write(
            destination, destination_root, "owned runtime destination"
        ) as output_stream:
            input_details = os.fstat(input_stream.fileno())
            output_details = os.fstat(output_stream.fileno())
            if not stat.S_ISREG(input_details.st_mode) or input_details.st_nlink != 1:
                raise RuntimeBundleError("Runtime source must remain a single-link regular file during copy.")
            if not stat.S_ISREG(output_details.st_mode) or output_details.st_nlink != 1:
                raise RuntimeBundleError("Owned runtime destination is not a unique regular file.")
            while True:
                block = input_stream.read(1024 * 1024)
                if not block:
                    break
                total += len(block)
                if total > MAX_FILE_BYTES:
                    raise RuntimeBundleError("Runtime source changed beyond its file-size bound during copy.")
                output_stream.write(block)
            output_stream.flush()
            os.fsync(output_stream.fileno())
    except OSError as error:
        raise RuntimeBundleError("Runtime source could not be copied exclusively into owned staging.") from error


def _directory_identity(path: Path, label: str) -> tuple[int, int]:
    try:
        details = path.stat(follow_symlinks=False)
    except OSError as error:
        raise RuntimeBundleError(f"Cannot inspect {label} identity.") from error
    if not stat.S_ISDIR(details.st_mode) or _has_reparse(path):
        raise RuntimeBundleError(f"{label} is not an owned regular directory.")
    return details.st_dev, details.st_ino


def _preserve_owned_staging(path: Path, expected_identity: tuple[int, int] | None = None) -> Path | None:
    if not path.exists():
        return None
    if not re.fullmatch(r"\.sha256-[0-9a-f]{64}\.pending-[0-9a-f]{32}", path.name):
        raise RuntimeBundleError("Refusing to preserve a path outside the owned staging namespace.")
    if expected_identity is None or _directory_identity(path, "owned staging cleanup") != expected_identity:
        raise RuntimeBundleError("Refusing to move staging whose pinned directory identity is unavailable or changed.")
    _reject_reparse_chain(path, "owned staging preservation")
    _walk_exact_tree(path)
    if _directory_identity(path, "owned staging preservation") != expected_identity:
        raise RuntimeBundleError("Refusing to move staging whose identity changed during bounded inspection.")
    rejected = path.parent / (path.name + ".rejected-" + uuid.uuid4().hex)
    if rejected.exists():
        raise RuntimeBundleError("Fresh rejected-staging path unexpectedly exists.")
    path.rename(rejected)
    if _directory_identity(rejected, "preserved staging") != expected_identity:
        raise RuntimeBundleError("Owned staging identity changed during preservation.")
    return rejected


def _quarantine_published_target(path: Path, expected_identity: tuple[int, int]) -> Path:
    if not path.exists():
        raise RuntimeBundleError("Published target disappeared before quarantine.")
    if _directory_identity(path, "published target") != expected_identity:
        raise RuntimeBundleError("Published target identity changed before quarantine.")
    _reject_reparse_chain(path, "published target quarantine")
    quarantine = path.parent / ("." + path.name + ".rejected-" + uuid.uuid4().hex)
    if quarantine.exists():
        raise RuntimeBundleError("Fresh quarantine path unexpectedly exists.")
    path.rename(quarantine)
    if _directory_identity(quarantine, "quarantined target") != expected_identity:
        raise RuntimeBundleError("Published target identity changed during quarantine.")
    return quarantine


def build_scaffold_bundle(
    spec_path: Path,
    output_root: Path,
    *,
    acknowledge_test_only: bool = False,
) -> Path:
    """Exercise synthetic copy/publication mechanics without production trust."""
    if acknowledge_test_only is not True:
        raise RuntimeBundleError("Synthetic scaffold build requires acknowledge_test_only=True.")
    _reject_external_path_lexically(str(spec_path), "build spec")
    _reject_external_path_lexically(str(output_root), "output root")
    spec_path = spec_path.absolute()
    output_root = output_root.absolute()
    _reject_network_path(spec_path, "build spec")
    _reject_network_path(output_root, "output root")
    _reject_reparse_chain(spec_path, "build spec")
    _reject_reparse_chain(output_root, "output root")
    spec = _parse_spec(spec_path)
    manifest = _manifest_from_spec(spec)
    bundle_hash = manifest["bundleTypedHash"]
    target_name = "sha256-" + bundle_hash.removeprefix("sha256:")
    output_root.mkdir(parents=True, exist_ok=True)
    _reject_reparse_chain(output_root, "output root")
    target = output_root / target_name
    if target.exists():
        verified = verify_scaffold_bundle(target, require_readonly_acl=False)
        if verified["bundleTypedHash"] != bundle_hash:
            raise RuntimeBundleError("Existing content-addressed target has another identity.")
        _seal_readonly_acl(target)
        verify_scaffold_bundle(target)
        return target

    staging = output_root / ("." + target_name + ".pending-" + uuid.uuid4().hex)
    if staging.exists():
        raise RuntimeBundleError("Fresh staging path unexpectedly exists.")
    staging_identity: tuple[int, int] | None = None
    published = False
    try:
        staging.mkdir()
        staging_identity = _directory_identity(staging, "owned staging root")
        _reject_reparse_chain(staging, "owned staging root")
        _seal_private_staging_acl(staging)
        for item in spec["_records"]:
            _reject_reparse_chain(item["source"], "runtime source before copy")
            source_hash, source_length = _hash_file(item["source"], item["sourceRoot"])
            if source_hash != item["sha256"] or source_length != item["byteLength"]:
                raise RuntimeBundleError("A source changed after the preparation pass.")
            destination = _checked_child(staging, item["path"], "owned runtime destination")
            destination.parent.mkdir(parents=True, exist_ok=True)
            _reject_reparse_chain(destination.parent, "owned staging parent")
            if destination.exists() or destination.is_symlink():
                raise RuntimeBundleError("A destination appeared inside the owned staging tree.")
            _copy_file_exclusive(item["source"], destination, item["sourceRoot"], staging)
            _reject_reparse_chain(item["source"], "runtime source after copy")
            _reject_reparse_chain(destination, "copied runtime file")
            copied_hash, copied_length = _hash_file(destination, staging)
            if copied_hash != source_hash or copied_length != source_length:
                raise RuntimeBundleError("Copied runtime bytes differ from their source.")
        _write_exclusive(staging / "runtime-bundle.json", _canonical_bytes(manifest) + b"\n")
        verify_scaffold_bundle(staging, expected_directory_name=None)
        _reject_reparse_chain(output_root, "output root before publish")
        if target.exists():
            raise RuntimeBundleError("Content-addressed target appeared during publication.")
        try:
            staging.rename(target)
        except FileExistsError:
            winner = verify_scaffold_bundle(target, require_readonly_acl=False)
            if winner["bundleTypedHash"] != bundle_hash:
                raise RuntimeBundleError("Concurrent publisher created a target with another identity.")
            _seal_readonly_acl(target)
            verify_scaffold_bundle(target)
            _preserve_owned_staging(staging, staging_identity)
            staging_identity = None
            return target
        published = True
        verify_scaffold_bundle(target, require_readonly_acl=False)
        _seal_readonly_acl(target)
        verify_scaffold_bundle(target)
        staging_identity = None
        return target
    except Exception as original_error:
        try:
            if staging_identity is not None and published:
                _quarantine_published_target(target, staging_identity)
                staging_identity = None
            elif staging_identity is not None:
                _preserve_owned_staging(staging, staging_identity)
        except Exception as cleanup_error:
            original_error.add_note(f"Synthetic scaffold cleanup also failed: {cleanup_error}")
        raise


def _walk_exact_tree(root: Path) -> tuple[list[str], list[str]]:
    files: list[str] = []
    directories: list[str] = []

    def fail_scan(error: OSError) -> None:
        raise RuntimeBundleError(f"Cannot enumerate bounded bundle tree: {error}") from error

    for current, directory_names, file_names in os.walk(
        root,
        topdown=True,
        onerror=fail_scan,
        followlinks=False,
    ):
        current_path = Path(current)
        _reject_reparse_chain(current_path, "bundle tree")
        if current_path != root:
            relative_directory = _safe_relative(current_path.relative_to(root).as_posix(), "bundle directory")
            directories.append(relative_directory)
            if len(directories) > MAX_FILES:
                raise RuntimeBundleError("Bundle directory count exceeds the bound.")
        directory_names.sort()
        file_names.sort()
        for name in directory_names:
            directory = current_path / name
            _safe_relative(directory.relative_to(root).as_posix(), "bundle directory")
            if _has_reparse(directory):
                raise RuntimeBundleError("Bundle contains a reparse directory.")
        for name in file_names:
            path = current_path / name
            if _has_reparse(path):
                raise RuntimeBundleError("Bundle contains a reparse file.")
            relative = path.relative_to(root).as_posix()
            files.append(_safe_relative(relative, "bundle file"))
            if len(files) > MAX_FILES + 1:
                raise RuntimeBundleError("Bundle file count exceeds the bound.")
    files.sort(key=lambda value: value.encode("utf-8"))
    directories.sort(key=lambda value: value.encode("utf-8"))
    return files, directories


def _expected_directories(files: Iterable[str]) -> set[str]:
    output: set[str] = set()
    for file_path in files:
        parts = file_path.split("/")[:-1]
        for length in range(1, len(parts) + 1):
            output.add("/".join(parts[:length]))
    return output


def verify_scaffold_bundle(
    bundle_root: Path,
    *,
    expected_directory_name: str | None = "content-addressed",
    require_readonly_acl: bool | None = None,
) -> dict[str, Any]:
    """Verify current scaffold bytes only; this creates no execution lease."""
    _reject_external_path_lexically(str(bundle_root), "bundle root")
    bundle_root = bundle_root.absolute()
    _reject_network_path(bundle_root, "bundle root")
    _reject_reparse_chain(bundle_root, "bundle root")
    if not bundle_root.is_dir():
        raise RuntimeBundleError("Runtime bundle root is missing.")
    manifest_path = bundle_root / "runtime-bundle.json"
    _reject_reparse_chain(manifest_path, "runtime manifest")
    manifest_data = _read_bounded_file(manifest_path, MAX_JSON_BYTES, "runtime manifest", bundle_root)
    manifest = _exact_object(strict_json_load_bytes(manifest_data, "runtime manifest"), MANIFEST_FIELDS, "runtime manifest")
    if manifest_data != _canonical_bytes(manifest) + b"\n":
        raise RuntimeBundleError("Runtime manifest bytes are not the unique canonical JSON encoding.")
    if (
        manifest["schema"] != BUNDLE_SCHEMA
        or manifest["bundleStatus"] != "SYNTHETIC_SCAFFOLD_ONLY"
        or manifest["platform"] != "windows"
        or manifest["architecture"] != "x86_64"
        or manifest["bundleHashEncoding"] != HASH_ENCODING
    ):
        raise RuntimeBundleError("Runtime manifest identity or policy is unsupported.")
    _required_python_version(manifest["pythonVersion"], "manifest pythonVersion")
    for field in ("numpyVersion", "pillowVersion"):
        _required_token(manifest[field], f"manifest {field}")
    policy = _exact_object(manifest["entryCommandPolicy"], POLICY_FIELDS, "entry command policy")
    executable = _safe_relative(policy["executable"], "entry executable")
    if not executable.endswith(".exe"):
        raise RuntimeBundleError("The Windows entry executable must have an .exe suffix.")
    if policy["fixedArguments"] != ["-I", "-s"] or any(
        policy[name] is not False
        for name in ("ambientPathAllowed", "userSiteAllowed", "networkAllowed", "packageMutationAllowed")
    ):
        raise RuntimeBundleError("Runtime command policy is not hermetic.")
    files = manifest["files"]
    if not isinstance(files, list) or not 1 <= len(files) <= MAX_FILES:
        raise RuntimeBundleError("Runtime file registry is empty or exceeds its bound.")
    seen: set[str] = set()
    normalized: list[dict[str, Any]] = []
    total = 0
    executable_count = 0
    kinds: set[str] = set()
    previous_key: bytes | None = None
    for index, token in enumerate(files):
        item = _exact_object(token, FILE_FIELDS, f"manifest file[{index}]")
        path = _safe_relative(item["path"], f"manifest file[{index}].path")
        key = path.encode("utf-8")
        if path in seen or (previous_key is not None and key <= previous_key):
            raise RuntimeBundleError("Runtime file registry must be unique and UTF-8 ordinal sorted.")
        seen.add(path)
        previous_key = key
        claimed_hash = _required_hash(item["sha256"], f"manifest file[{index}].sha256")
        claimed_length = item["byteLength"]
        if not isinstance(claimed_length, int) or isinstance(claimed_length, bool) or not 0 <= claimed_length <= MAX_FILE_BYTES:
            raise RuntimeBundleError("Runtime file byte length is out of bounds.")
        kind = item["kind"]
        if kind not in FILE_KINDS:
            raise RuntimeBundleError("Runtime file kind is unsupported.")
        component = item["component"]
        if component not in ROOT_IDS:
            raise RuntimeBundleError("Runtime file component is unsupported.")
        if (
            (kind in {"python-executable", "python-runtime", "stdlib"} and component != "python")
            or (kind == "numpy" and component != "numpy")
            or (kind == "pillow" and component != "pillow")
        ):
            raise RuntimeBundleError("Runtime file kind does not match its component.")
        kinds.add(kind)
        if kind == "python-executable":
            executable_count += 1
            if path != executable:
                raise RuntimeBundleError("Runtime executable record differs from command policy.")
        actual_path = _checked_child(bundle_root, path, f"runtime file {path}")
        _reject_reparse_chain(actual_path, f"runtime file {path}")
        actual_hash, actual_length = _hash_file(actual_path, bundle_root)
        if actual_hash != claimed_hash or actual_length != claimed_length:
            raise RuntimeBundleError("Runtime file bytes differ from the manifest.")
        total += actual_length
        if total > MAX_TOTAL_BYTES:
            raise RuntimeBundleError("Runtime bundle exceeds the aggregate byte bound.")
        normalized.append(
            {
                "path": path,
                "sha256": claimed_hash,
                "byteLength": claimed_length,
                "component": component,
                "kind": kind,
            }
        )
    if executable_count != 1 or not {"stdlib", "numpy", "pillow"}.issubset(kinds):
        raise RuntimeBundleError("Runtime manifest lacks its executable or required dependency groups.")
    if _required_hash(manifest["fileSetTypedHash"], "fileSetTypedHash") != _typed_hash(
        {"schema": FILE_SET_SCHEMA, "files": normalized}
    ):
        raise RuntimeBundleError("Runtime file-set hash is invalid.")
    component_hashes = _exact_object(manifest["componentHashes"], set(ROOT_IDS), "component hashes")
    for component in ROOT_IDS:
        expected_component_hash = _typed_hash(
            {
                "schema": COMPONENT_FILE_SET_SCHEMA,
                "component": component,
                "files": [item for item in normalized if item["component"] == component],
            }
        )
        if _required_hash(component_hashes[component], f"componentHashes.{component}") != expected_component_hash:
            raise RuntimeBundleError(f"Runtime {component} component hash is invalid.")
    claimed_bundle_hash = _required_hash(manifest["bundleTypedHash"], "bundleTypedHash")
    clone = dict(manifest)
    del clone["bundleTypedHash"]
    if claimed_bundle_hash != _typed_hash(clone):
        raise RuntimeBundleError("Runtime manifest self-hash is invalid.")
    if expected_directory_name is not None:
        expected = "sha256-" + claimed_bundle_hash.removeprefix("sha256:")
        if bundle_root.name != expected:
            raise RuntimeBundleError("Runtime bundle directory is not content-addressed by its manifest hash.")
    actual_files, actual_directories = _walk_exact_tree(bundle_root)
    actual_set = set(actual_files)
    expected_set = set(seen)
    expected_set.add("runtime-bundle.json")
    if actual_set != expected_set:
        raise RuntimeBundleError("Runtime bundle contains a missing or extra file.")
    if set(actual_directories) != _expected_directories(expected_set):
        raise RuntimeBundleError("Runtime bundle contains an undeclared or missing directory.")
    if require_readonly_acl is None:
        require_readonly_acl = expected_directory_name is not None
    if require_readonly_acl:
        _verify_readonly_acl(bundle_root)
    return manifest


class _SyntheticScaffoldReplay:
    """Test-only replay helper; explicitly not an execution or namespace lease."""

    def __init__(self, bundle_root: Path):
        self.bundle_root = bundle_root.absolute()
        self._stack = contextlib.ExitStack()
        self._closed = False
        try:
            first = verify_scaffold_bundle(self.bundle_root)
            paths = [item["path"] for item in first["files"]] + ["runtime-bundle.json"]
            self._streams = tuple(
                self._stack.enter_context(
                    _open_pinned_read(
                        _checked_child(self.bundle_root, relative, f"leased runtime file {relative}"),
                        self.bundle_root,
                        f"leased runtime file {relative}",
                    )
                )
                for relative in paths
            )
            second = verify_scaffold_bundle(self.bundle_root)
            if second != first:
                raise RuntimeBundleError("Synthetic scaffold changed while acquiring replay handles.")
            self.manifest = second
        except Exception:
            self._stack.close()
            self._closed = True
            raise

    @property
    def executable_path(self) -> Path:
        relative = self.manifest["entryCommandPolicy"]["executable"]
        return _checked_child(self.bundle_root, relative, "leased runtime executable")

    def close(self) -> None:
        if self._closed:
            return
        try:
            final = verify_scaffold_bundle(self.bundle_root)
            if final != self.manifest:
                raise RuntimeBundleError("Synthetic scaffold changed before replay handles were released.")
        finally:
            self._stack.close()
            self._closed = True

    def __enter__(self) -> "_SyntheticScaffoldReplay":
        if self._closed:
            raise RuntimeBundleError("Synthetic scaffold replay is already closed.")
        return self

    def __exit__(self, exc_type: Any, exc_value: Any, traceback: Any) -> bool:
        self.close()
        return False


def build_bundle(spec_path: Path, output_root: Path) -> Path:
    """Fail closed: trusted production publication is not implemented."""
    _reject_external_path_lexically(str(spec_path), "build spec")
    _reject_external_path_lexically(str(output_root), "output root")
    raise RuntimeBundleError(
        "Trusted runtime publication is unavailable: no isolated gate principal "
        "or handle-bound exclusive parent namespace is implemented."
    )


def verify_bundle(bundle_root: Path, **_: Any) -> dict[str, Any]:
    """Fail closed: a scaffold byte check is not production verification."""
    _reject_external_path_lexically(str(bundle_root), "bundle root")
    raise RuntimeBundleError(
        "Trusted runtime verification is unavailable; use verify_scaffold_bundle "
        "only for synthetic tests and never as an execution authority."
    )


def acquire_verified_bundle_lease(bundle_root: Path) -> None:
    """Fail closed until an isolated principal and handle-bound namespace exist."""
    _reject_external_path_lexically(str(bundle_root), "bundle root")
    raise RuntimeBundleError(
        "Trusted runtime lease is unavailable: pinned files do not prevent "
        "same-principal directory replacement or namespace injection."
    )


def _cli(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    build = subparsers.add_parser("build")
    build.add_argument("--spec", required=True)
    build.add_argument("--output-root", required=True)
    verify = subparsers.add_parser("verify")
    verify.add_argument("--bundle", required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "build":
            path = build_bundle(Path(args.spec), Path(args.output_root))
            print(path)
        else:
            manifest = verify_bundle(Path(args.bundle))
            print(manifest["bundleTypedHash"])
        return 0
    except (OSError, RuntimeBundleError) as error:
        print(f"EVIDENCE_INVALID: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(_cli(sys.argv[1:]))

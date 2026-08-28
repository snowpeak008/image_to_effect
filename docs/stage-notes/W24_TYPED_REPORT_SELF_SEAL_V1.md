# W24 typed metrics-report self-seal v1

Status: implementation and synthetic cross-runtime vectors only. This stage
does not capture Unity output, mutate any formal evidence, or re-sign any
bundle, contract, trace, or frozen identity.

`w24-typed-binary-v1` replaces the metrics-report self-seal's JSON-text hash.
The hash input starts with the ASCII domain `w24-typed-binary-v1\0`; values
then use tags for null, false, true, integer, double, string, array, and
object. Integer payloads are minimal decimal ASCII, doubles are finite IEEE-754
binary64 big-endian (therefore `1`, `1.0`, and `-0.0` remain distinct where
typed), strings are strict UTF-8, and all lengths/counts are big-endian u32.
Object members sort by UTF-8 key bytes. Both implementations cap depth, nodes,
container items, and string bytes, and reject non-finite doubles and lone UTF-16
surrogates.

The report now carries `sealedReportEncoding: "w24-typed-binary-v1"` before
the seal is made. Both W24 formal validators require that exact field and
recompute only the typed self-seal; there is no legacy JSON-hash fallback.

The important implementation lesson is that a deterministic JSON serializer is
not a type-preserving commitment: JSON text can blur numeric representations
and platform formatting rules. Separating integer decimal payloads from
binary64 payloads, plus sorting raw UTF-8 key bytes, makes the Python/Unity
boundary explicit and vector-testable.

Follow-up identity work (intentionally not performed in this stage): the new
helper and its S5 callers must be included when the S3 capture-tool bundle and
every S0b bundle that contains `W24S5EvidenceTransition.cs` are next formally
re-signed. Existing frozen bundle, contract, trace, and evidence identities
remain untouched here.

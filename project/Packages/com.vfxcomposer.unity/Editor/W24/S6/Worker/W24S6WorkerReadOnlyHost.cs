using System;
using System.IO;

namespace VFXComposer.Editor.W24.S6.Worker
{
    internal sealed class W24S6WorkerReadTarget
    {
        internal W24S6WorkerReadTarget(bool useProjectRoot, string relativePath)
        {
            UseProjectRoot = useProjectRoot;
            RelativePath = relativePath;
        }

        internal bool UseProjectRoot { get; private set; }
        internal string RelativePath { get; private set; }
    }

    /// <summary>
    /// Closed mapping from registry identities to Worker-owned relative document locations.
    /// Callers cannot submit a path, suffix, root or filename.
    /// </summary>
    internal static class W24S6WorkerReadOnlyHost
    {
        internal const string LibraryIndexRelativePath =
            "ProjectSettings/VFXComposer/LibraryIndex.json";

        internal static W24S6WorkerReadTarget Resolve(
            string documentKind,
            string documentId)
        {
            if (documentKind == Protocol.W24S6WorkerReadQueryCodec.LibraryIndexKind &&
                documentId == "project")
                return new W24S6WorkerReadTarget(true, LibraryIndexRelativePath);
            if (!IsRegistryDocumentId(documentId))
                throw new InvalidDataException("W24WKR004");
            if (documentKind == Protocol.W24S6WorkerReadQueryCodec.ManifestKind)
                return new W24S6WorkerReadTarget(
                    true,
                    "ProjectSettings/VFXComposer/BuildManifests/" + documentId + ".manifest.json");
            if (documentKind == Protocol.W24S6WorkerReadQueryCodec.ContractKind)
                return new W24S6WorkerReadTarget(
                    false,
                    "docs/vfx-contracts/" + documentId + ".contract.json");
            if (documentKind == Protocol.W24S6WorkerReadQueryCodec.TraceKind)
                return new W24S6WorkerReadTarget(
                    false,
                    "docs/vfx-traces/" + documentId + ".implementation-trace.json");
            throw new InvalidDataException("W24WKR004");
        }

        private static bool IsRegistryDocumentId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96 || value[0] < 'a' || value[0] > 'z')
                return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '_' && character != '-') return false;
            }
            return true;
        }
    }
}

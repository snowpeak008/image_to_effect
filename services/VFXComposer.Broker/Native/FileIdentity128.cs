using System.Buffers.Binary;

namespace VFXComposer.Broker.Native;

internal sealed record FileIdentity128
{
    private readonly byte[] _bytes;

    public FileIdentity128(ulong low, ulong high)
    {
        _bytes = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(_bytes.AsSpan(0, 8), low);
        BinaryPrimitives.WriteUInt64LittleEndian(_bytes.AsSpan(8, 8), high);
    }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public string Hex => Convert.ToHexString(_bytes).ToLowerInvariant();

    public bool FixedEquals(FileIdentity128? other) =>
        other is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            _bytes,
            other._bytes);
}

internal sealed record NativeDirectoryIdentity(
    ulong VolumeSerialNumber,
    FileIdentity128 FileId,
    uint FileAttributes)
{
    public bool FixedEquals(NativeDirectoryIdentity? other) =>
        other is not null &&
        VolumeSerialNumber == other.VolumeSerialNumber &&
        FileAttributes == other.FileAttributes &&
        FileId.FixedEquals(other.FileId);
}

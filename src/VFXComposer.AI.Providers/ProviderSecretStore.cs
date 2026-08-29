using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>Checks a profile-owned SecretRef without exposing its plaintext.</summary>
public interface ISecretReferenceVerifier
{
    bool IsReadable(string profileId, SecretRef secretRef);
}

/// <summary>
/// Per-user DPAPI storage for credential material. A secret is scoped to exactly one profile ID and one SecretRef;
/// configuration JSON can contain only that profile-owned reference.
/// </summary>
public sealed class ProviderSecretStore : ISecretReferenceVerifier
{
    private const int MaximumSecretBytes = 16 * 1024;
    private static readonly byte[] Magic = "VFXAIDP2"u8.ToArray();
    private static readonly byte[] LegacyMagic = "VFXAIDP1"u8.ToArray();
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly string _rootDirectory;

    public ProviderSecretStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <summary>Saves caller-owned plaintext without ever returning it or placing it in configuration JSON.</summary>
    public void SaveSecret(string profileId, SecretRef secretRef, ReadOnlySpan<char> plaintext)
    {
        profileId = ValidateProfileId(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        if (!OperatingSystem.IsWindows() || plaintext.Length == 0 || plaintext.Length > MaximumSecretBytes || plaintext.IndexOf('\0') >= 0)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }

        byte[]? plainBytes = null;
        byte[]? protectedBytes = null;
        byte[]? fileBytes = null;
        try
        {
            var byteCount = Utf8.GetByteCount(plaintext);
            if (byteCount is < 1 or > MaximumSecretBytes)
            {
                throw new AiGatewayException(AiErrorCode.SecretUnavailable);
            }

            plainBytes = new byte[byteCount];
            Utf8.GetBytes(plaintext, plainBytes);
            protectedBytes = DpapiCurrentUser.Protect(plainBytes, EntropyFor(profileId, secretRef));
            fileBytes = CreateEnvelope(profileId, secretRef, protectedBytes);
            AtomicFileWriter.WriteReplace(
                SecretPathFor(profileId, secretRef),
                BackupPathFor(profileId, secretRef),
                fileBytes);
        }
        catch (AiGatewayException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }
        catch (IOException)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }
        finally
        {
            Zero(plainBytes);
            Zero(protectedBytes);
            Zero(fileBytes);
        }
    }

    /// <summary>Returns only readability; the temporary plaintext buffer is immediately zeroed.</summary>
    public bool IsReadable(string profileId, SecretRef secretRef)
    {
        profileId = ValidateProfileId(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        try
        {
            using var lease = OpenSecret(profileId, secretRef);
            return lease.Length > 0;
        }
        catch (AiGatewayException)
        {
            return false;
        }
    }

    /// <summary>
    /// Revokes the exact profile-owned secret reference. Both the primary and retained backup are removed so a
    /// deleted profile cannot be revived by a stale protected envelope. No secret payload is opened or returned.
    /// </summary>
    public void RevokeSecret(string profileId, SecretRef secretRef)
    {
        profileId = ValidateProfileId(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        try
        {
            DeleteIfPresent(SecretPathFor(profileId, secretRef));
            DeleteIfPresent(BackupPathFor(profileId, secretRef));
        }
        catch (IOException)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }
    }

    public override string ToString() => "ProviderSecretStore(<redacted>)";

    internal ProviderSecretLease OpenSecret(string profileId, SecretRef secretRef)
    {
        profileId = ValidateProfileId(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        if (!OperatingSystem.IsWindows())
        {
            throw new AiGatewayException(AiErrorCode.SecretUnavailable);
        }

        if (TryOpen(SecretPathFor(profileId, secretRef), profileId, secretRef, out var primary))
        {
            return primary!;
        }

        if (TryOpen(BackupPathFor(profileId, secretRef), profileId, secretRef, out var backup))
        {
            return backup!;
        }

        throw new AiGatewayException(AiErrorCode.SecretUnavailable);
    }

    internal string SecretPathFor(string profileId, SecretRef secretRef)
    {
        profileId = ValidateProfileId(profileId);
        ArgumentNullException.ThrowIfNull(secretRef);
        var idBytes = Utf8.GetBytes("VFXComposer.AI.ProviderSecretStore/path/v2/" + profileId + "/" + secretRef.Id);
        try
        {
            var digest = SHA256.HashData(idBytes);
            try
            {
                return Path.Combine(_rootDirectory, Convert.ToHexString(digest).ToLowerInvariant() + ".secret");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(idBytes);
        }
    }

    private string BackupPathFor(string profileId, SecretRef secretRef) => SecretPathFor(profileId, secretRef) + ".bak";

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool TryOpen(string path, string profileId, SecretRef secretRef, out ProviderSecretLease? result)
    {
        result = null;
        if (!File.Exists(path))
        {
            return false;
        }

        byte[]? fileBytes = null;
        byte[]? protectedBytes = null;
        try
        {
            fileBytes = AtomicFileWriter.ReadBounded(path, MaximumSecretBytes * 16);
            if (IsLegacyEnvelope(fileBytes) || !TryReadEnvelope(fileBytes, profileId, secretRef, out protectedBytes))
            {
                return false;
            }

            var plaintext = DpapiCurrentUser.Unprotect(protectedBytes!, EntropyFor(profileId, secretRef));
            if (plaintext.Length is < 1 or > MaximumSecretBytes)
            {
                Zero(plaintext);
                return false;
            }

            result = new ProviderSecretLease(plaintext);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Zero(fileBytes);
            Zero(protectedBytes);
        }
    }

    private static byte[] CreateEnvelope(string profileId, SecretRef secretRef, ReadOnlySpan<byte> protectedBytes)
    {
        var profileBytes = Utf8.GetBytes(profileId);
        var secretRefBytes = Utf8.GetBytes(secretRef.Id);
        try
        {
            if (profileBytes.Length > byte.MaxValue || secretRefBytes.Length > byte.MaxValue || protectedBytes.Length == 0)
            {
                throw new AiGatewayException(AiErrorCode.SecretUnavailable);
            }

            var headerLength = checked(Magic.Length + 2 + profileBytes.Length + secretRefBytes.Length);
            var fileBytes = new byte[checked(headerLength + protectedBytes.Length)];
            Magic.CopyTo(fileBytes, 0);
            fileBytes[Magic.Length] = checked((byte)profileBytes.Length);
            fileBytes[Magic.Length + 1] = checked((byte)secretRefBytes.Length);
            profileBytes.CopyTo(fileBytes, Magic.Length + 2);
            secretRefBytes.CopyTo(fileBytes, Magic.Length + 2 + profileBytes.Length);
            protectedBytes.CopyTo(fileBytes.AsSpan(headerLength));
            return fileBytes;
        }
        finally
        {
            Zero(profileBytes);
            Zero(secretRefBytes);
        }
    }

    private static bool TryReadEnvelope(
        ReadOnlySpan<byte> fileBytes,
        string profileId,
        SecretRef secretRef,
        out byte[]? protectedBytes)
    {
        protectedBytes = null;
        if (fileBytes.Length <= Magic.Length + 2 || !fileBytes[..Magic.Length].SequenceEqual(Magic))
        {
            return false;
        }

        var profileLength = fileBytes[Magic.Length];
        var secretRefLength = fileBytes[Magic.Length + 1];
        var headerLength = checked(Magic.Length + 2 + profileLength + secretRefLength);
        if (profileLength == 0 || secretRefLength == 0 || headerLength >= fileBytes.Length)
        {
            return false;
        }

        var expectedProfileBytes = Utf8.GetBytes(profileId);
        var expectedSecretRefBytes = Utf8.GetBytes(secretRef.Id);
        try
        {
            if (expectedProfileBytes.Length != profileLength ||
                expectedSecretRefBytes.Length != secretRefLength ||
                !fileBytes.Slice(Magic.Length + 2, profileLength).SequenceEqual(expectedProfileBytes) ||
                !fileBytes.Slice(Magic.Length + 2 + profileLength, secretRefLength).SequenceEqual(expectedSecretRefBytes))
            {
                return false;
            }

            protectedBytes = fileBytes[headerLength..].ToArray();
            return true;
        }
        finally
        {
            Zero(expectedProfileBytes);
            Zero(expectedSecretRefBytes);
        }
    }

    private static bool IsLegacyEnvelope(ReadOnlySpan<byte> fileBytes) =>
        fileBytes.Length >= LegacyMagic.Length && fileBytes[..LegacyMagic.Length].SequenceEqual(LegacyMagic);

    private static string ValidateProfileId(string profileId) => new SecretRef(profileId).Id;

    private static byte[] EntropyFor(string profileId, SecretRef secretRef) =>
        Utf8.GetBytes("VFXComposer.AI.ProviderSecretStore/v2/" + profileId + "/" + secretRef.Id);

    private static void Zero(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}

/// <summary>
/// Internal-only short-lived plaintext material. It cannot format itself or escape as a string.
/// </summary>
internal sealed class ProviderSecretLease : IDisposable
{
    private byte[]? _bytes;

    internal ProviderSecretLease(byte[] bytes) => _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));

    internal int Length => _bytes?.Length ?? 0;

    internal ReadOnlySpan<byte> Bytes => _bytes ?? throw new ObjectDisposedException(nameof(ProviderSecretLease));

    public void Dispose()
    {
        var bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public override string ToString() => "ProviderSecretLease(<redacted>)";
}

internal static class DpapiCurrentUser
{
    private const uint CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(ReadOnlySpan<byte> plaintext, byte[] entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        if (!OperatingSystem.IsWindows())
        {
            throw new CryptographicException("Current-user protection is unavailable.");
        }

        var input = DataBlob.From(plaintext);
        var optionalEntropy = DataBlob.From(entropy);
        try
        {
            if (!CryptProtectData(
                    ref input,
                    "VFXComposer AI Provider Secret v2",
                    ref optionalEntropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                throw new CryptographicException("Current-user protection failed.");
            }

            try
            {
                return output.CopyToManaged();
            }
            finally
            {
                output.FreeLocal(zero: false);
            }
        }
        finally
        {
            input.FreeHGlobal(zero: true);
            optionalEntropy.FreeHGlobal(zero: true);
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    public static byte[] Unprotect(ReadOnlySpan<byte> protectedBytes, byte[] entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        if (!OperatingSystem.IsWindows())
        {
            throw new CryptographicException("Current-user protection is unavailable.");
        }

        var input = DataBlob.From(protectedBytes);
        var optionalEntropy = DataBlob.From(entropy);
        try
        {
            if (!CryptUnprotectData(
                    ref input,
                    out var description,
                    ref optionalEntropy,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                throw new CryptographicException("Current-user unprotection failed.");
            }

            try
            {
                return output.CopyToManaged();
            }
            finally
            {
                output.FreeLocal(zero: true);
                if (description != IntPtr.Zero)
                {
                    _ = LocalFree(description);
                }
            }
        }
        finally
        {
            input.FreeHGlobal(zero: false);
            optionalEntropy.FreeHGlobal(zero: true);
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public uint Length;
        public IntPtr Data;

        public static DataBlob From(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return new DataBlob();
            }

            var data = Marshal.AllocHGlobal(bytes.Length);
            var managed = bytes.ToArray();
            try
            {
                Marshal.Copy(managed, 0, data, managed.Length);
                return new DataBlob { Length = checked((uint)managed.Length), Data = data };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(managed);
            }
        }

        public byte[] CopyToManaged()
        {
            if (Length > int.MaxValue || (Length > 0 && Data == IntPtr.Zero))
            {
                throw new CryptographicException("Protected data was invalid.");
            }

            var length = checked((int)Length);
            var managed = new byte[length];
            if (length > 0)
            {
                Marshal.Copy(Data, managed, 0, length);
            }

            return managed;
        }

        public void FreeHGlobal(bool zero)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            if (zero)
            {
                ZeroMemory(Data, Length);
            }

            Marshal.FreeHGlobal(Data);
            Data = IntPtr.Zero;
            Length = 0;
        }

        public void FreeLocal(bool zero)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            if (zero)
            {
                ZeroMemory(Data, Length);
            }

            _ = LocalFree(Data);
            Data = IntPtr.Zero;
            Length = 0;
        }

        private static void ZeroMemory(IntPtr pointer, uint length)
        {
            for (var index = 0U; index < length; index++)
            {
                Marshal.WriteByte(pointer, checked((int)index), 0);
            }
        }
    }
}

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WindowsDurableProfileStoreTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string OtherServiceSidText = "S-1-5-80-101-202-303-404-506";
    private const uint FileReadData = 0x00000001;
    private const uint FileWriteData = 0x00000002;
    private const uint FileReadAttributes = 0x00000080;
    private const uint ReadControl = 0x00020000;
    private const uint DeleteAccess = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint AccessSystemSecurity = 0x01000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int AuthenticatorLength = 32;

    [TestMethod]
    public void RootAndStoreFileProfilesAreSeparateExactServiceOnlyDescriptors()
    {
        var profile = CreateProfile();
        var root = ReadDescriptor(profile.GetRootSecurityDescriptorBytes());
        var file = ReadDescriptor(profile.GetStoreFileSecurityDescriptorBytes());

        Assert.AreNotEqual(profile.CanonicalRootSecurityDescriptor, profile.CanonicalStoreFileSecurityDescriptor);
        Assert.AreEqual(ServiceSidText, root.Owner!.Value);
        Assert.AreEqual(ServiceSidText, root.Group!.Value);
        Assert.AreEqual(ServiceSidText, file.Owner!.Value);
        Assert.AreEqual(ServiceSidText, file.Group!.Value);
        Assert.IsTrue(profile.MatchesExpectedRootSecurityDescriptor(root));
        Assert.IsTrue(profile.MatchesExpectedStoreFileSecurityDescriptor(file));
        Assert.IsFalse(profile.MatchesExpectedRootSecurityDescriptor(file));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(root));
    }

    [TestMethod]
    public void RootAndStoreFileCanonicalBytesAndTypedDigestsAreSeparate()
    {
        var first = CreateProfile();
        var second = CreateProfile();

        CollectionAssert.AreEqual(first.GetCanonicalBytes(), second.GetCanonicalBytes());
        CollectionAssert.AreEqual(first.GetCanonicalRootBytes(), second.GetCanonicalRootBytes());
        CollectionAssert.AreEqual(first.GetCanonicalStoreFileBytes(), second.GetCanonicalStoreFileBytes());
        Assert.IsFalse(first.GetCanonicalRootBytes().SequenceEqual(first.GetCanonicalStoreFileBytes()));
        Assert.IsFalse(first.RootProtectionDigest.FixedTimeEquals(first.StoreFileProtectionDigest));
        Assert.IsTrue(first.ProfileDigest.FixedTimeEquals(second.ProfileDigest));
        Assert.AreEqual(DurableProductionProfile.RootProtectionDigestType, first.RootProtectionDigest.TypeTag);
        Assert.AreEqual(DurableProductionProfile.StoreFileProtectionDigestType, first.StoreFileProtectionDigest.TypeTag);
    }

    [TestMethod]
    public void RootMaskIsExact()
    {
        var ace = (CommonAce)ReadDescriptor(CreateProfile().GetRootSecurityDescriptorBytes()).DiscretionaryAcl![0];

        Assert.AreEqual(unchecked((int)0x001200A2), ace.AccessMask);
        Assert.AreEqual(AceType.AccessAllowed, ace.AceType);
        Assert.AreEqual(AceFlags.None, ace.AceFlags);
    }

    [TestMethod]
    public void StoreFileMaskIsExact()
    {
        var ace = (CommonAce)ReadDescriptor(CreateProfile().GetStoreFileSecurityDescriptorBytes()).DiscretionaryAcl![0];

        Assert.AreEqual(unchecked((int)0x00130083), ace.AccessMask);
        Assert.AreEqual(AceType.AccessAllowed, ace.AceType);
        Assert.AreEqual(AceFlags.None, ace.AceFlags);
    }

    [TestMethod]
    public void RootDescriptorRejectsOwnerAndGroupDrift()
    {
        var profile = CreateProfile();
        var service = new SecurityIdentifier(ServiceSidText);
        var other = new SecurityIdentifier(OtherServiceSidText);
        var allow = AllowAce(DurableProductionProfile.RootAccessMask, service);

        Assert.IsFalse(profile.MatchesExpectedRootSecurityDescriptor(CreateDescriptor(0x1404, other, service, null, allow)));
        Assert.IsFalse(profile.MatchesExpectedRootSecurityDescriptor(CreateDescriptor(0x1404, service, other, null, allow)));
    }

    [TestMethod]
    public void StoreFileDescriptorRejectsOwnerAndGroupDrift()
    {
        var profile = CreateProfile();
        var service = new SecurityIdentifier(ServiceSidText);
        var other = new SecurityIdentifier(OtherServiceSidText);
        var allow = AllowAce(DurableProductionProfile.StoreFileAccessMask, service);

        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, other, service, null, allow)));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, service, other, null, allow)));
    }

    [TestMethod]
    public void RootDescriptorRejectsBroadPrincipal()
    {
        var profile = CreateProfile();
        var service = new SecurityIdentifier(ServiceSidText);
        var broad = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        Assert.IsFalse(profile.MatchesExpectedRootSecurityDescriptor(CreateDescriptor(
            0x1404,
            service,
            service,
            null,
            AllowAce(DurableProductionProfile.RootAccessMask, broad))));
    }

    [TestMethod]
    public void StoreFileDescriptorRejectsDenyInheritedCallbackDuplicateAndSacl()
    {
        var profile = CreateProfile();
        var service = new SecurityIdentifier(ServiceSidText);
        var mask = DurableProductionProfile.StoreFileAccessMask;
        var deny = new CommonAce(AceFlags.None, AceQualifier.AccessDenied, unchecked((int)mask), service, false, null);
        var inherited = new CommonAce(AceFlags.Inherited, AceQualifier.AccessAllowed, unchecked((int)mask), service, false, null);
        var callback = new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, unchecked((int)mask), service, true, [0, 0, 0, 0]);
        var allow = AllowAce(mask, service);
        var sacl = new RawAcl(2, 1);
        sacl.InsertAce(0, new CommonAce(AceFlags.SuccessfulAccess, AceQualifier.SystemAudit, unchecked((int)mask), service, false, null));

        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, service, service, null, deny)));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, service, service, null, inherited)));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, service, service, null, callback)));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1004, service, service, null, allow, AllowAce(mask, service))));
        Assert.IsFalse(profile.MatchesExpectedStoreFileSecurityDescriptor(CreateDescriptor(0x1014, service, service, sacl, allow)));
    }

    [TestMethod]
    public void ProfileRejectsNonServiceSid()
    {
        AssertArgumentRejected(() => new DurableProductionProfile(
            "profile-a",
            WindowsSid.ParseUser("S-1-5-21-101-202-303-404")));
    }

    [TestMethod]
    public void ProfileRejectsNonCanonicalProfileId()
    {
        AssertArgumentRejected(() => new DurableProductionProfile("Profile/A", WindowsSid.ParseService(ServiceSidText)));
        AssertArgumentRejected(() => new DurableProductionProfile(string.Empty, WindowsSid.ParseService(ServiceSidText)));
    }

    [TestMethod]
    public void StoreOpenAccessContractsRequireSaclAndDeleteRights()
    {
        WithTemporaryDirectory(root =>
        {
            using var handle = OpenDirectory(root, DurableProductionProfile.RootAccessMask);
            var granted = WindowsDurableProfileStore.QueryGrantedAccess(handle);

            Assert.AreEqual(DurableProductionProfile.RootAccessMask, granted);
            Assert.AreNotEqual(WindowsDurableProfileStore.RequiredRootGrantedAccess, granted);
            AssertStoreRejected(() => WindowsDurableProfileStore.CreateNew(handle, CreateProfile(), 1));
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(root).Count());
        });
    }

    [TestMethod]
    public void FixedStoreSegmentsRejectPathsAndReservedInputs()
    {
        WindowsDurableProfileStore.RequireFixedSegment("vfx-durable-profile-r-00000000000000000001.record");
        foreach (var invalid in new[] { string.Empty, ".", "..", "name/part", "name\\part", "name:", "name.", "name ", "name\u001f" })
        {
            AssertArgumentRejected(() => WindowsDurableProfileStore.RequireFixedSegment(invalid));
        }
    }

    [TestMethod]
    public void NoPrivilegeDirectoryReportsItsExactGrantedAccess()
    {
        WithTemporaryDirectory(root =>
        {
            using var handle = OpenDirectory(root, DurableProductionProfile.RootAccessMask);
            Assert.AreEqual(DurableProductionProfile.RootAccessMask, WindowsDurableProfileStore.QueryGrantedAccess(handle));
            Assert.IsFalse(handle.IsClosed);
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(root).Count());
        });
    }

    [TestMethod]
    public void SensitiveTransferZeroesOwnedBytesWhenBoundsThrow()
    {
        var rejected = Filled(AuthenticatorLength + 1, 0xA5);
        AssertInvalidData(() => WindowsDurableProfileStore.TakeSensitiveBytesWithinBounds(
            rejected,
            AuthenticatorLength,
            AuthenticatorLength));
        Assert.IsTrue(rejected.All(value => value == 0));

        var accepted = Filled(AuthenticatorLength, 0x5A);
        var transferred = WindowsDurableProfileStore.TakeSensitiveBytesWithinBounds(
            accepted,
            AuthenticatorLength,
            AuthenticatorLength);
        try
        {
            Assert.AreSame(accepted, transferred);
            Assert.IsTrue(transferred.Any(value => value != 0));
        }
        finally
        {
            Zero(transferred);
        }
    }

    [TestMethod]
    public void AuthenticatedRecordAndTipDisposeZeroTheirOwnedBuffers()
    {
        var previous = Filled(AuthenticatorLength, 1);
        var recordAuthenticator = Filled(AuthenticatorLength, 2);
        var nonceFingerprint = Filled(AuthenticatorLength, 3);
        var tipAuthenticator = Filled(AuthenticatorLength, 4);
        var record = new WindowsDurableProfileStore.AuthenticatedRecord(
            DurableProfileStoreRecordKind.NonceConsumption,
            2,
            1,
            previous,
            recordAuthenticator,
            nonceFingerprint);
        var tip = new WindowsDurableProfileStore.AuthenticatedSealedTip(
            DurableProfileStoreRecordKind.NonceConsumption,
            2,
            1,
            recordAuthenticator,
            tipAuthenticator);

        record.Dispose();
        tip.Dispose();

        Assert.IsTrue(previous.All(value => value == 0));
        Assert.IsTrue(recordAuthenticator.All(value => value == 0));
        Assert.IsTrue(nonceFingerprint.All(value => value == 0));
        Assert.IsTrue(tipAuthenticator.All(value => value == 0));
    }

    [TestMethod]
    public void NonceFingerprintUsesDeterministicDomainSeparatedHmac()
    {
        var key = Filled(AuthenticatorLength, 11);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 21);
        var nonce = Filled(WindowsDurableProfileStore.NonceLength, 31);
        byte[]? first = null;
        byte[]? second = null;
        byte[]? changedNonce = null;
        byte[]? changedGeneration = null;
        try
        {
            var profile = CreateProfile();
            first = WindowsDurableProfileStore.ComputeNonceFingerprint(key, store, profile, 7, nonce);
            second = WindowsDurableProfileStore.ComputeNonceFingerprint(key, store, profile, 7, nonce);
            nonce[0] ^= 0x80;
            changedNonce = WindowsDurableProfileStore.ComputeNonceFingerprint(key, store, profile, 7, nonce);
            changedGeneration = WindowsDurableProfileStore.ComputeNonceFingerprint(key, store, profile, 8, nonce);

            Assert.IsTrue(CryptographicOperations.FixedTimeEquals(first, second));
            Assert.IsFalse(CryptographicOperations.FixedTimeEquals(first, changedNonce));
            Assert.IsFalse(CryptographicOperations.FixedTimeEquals(changedNonce, changedGeneration));
        }
        finally
        {
            Zero(key, store, nonce, first, second, changedNonce, changedGeneration);
        }
    }

    [TestMethod]
    public void RecordAndTipHmacDomainsAuthenticateTheSameBytesDifferently()
    {
        var key = Filled(AuthenticatorLength, 41);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 51);
        var signedBytes = Filled(67, 61);
        byte[]? record = null;
        byte[]? tip = null;
        byte[]? changed = null;
        try
        {
            record = WindowsDurableProfileStore.ComputeRecordAuthenticator(key, store, signedBytes);
            tip = WindowsDurableProfileStore.ComputeSealedTipAuthenticator(key, store, signedBytes);
            signedBytes[0] ^= 0x20;
            changed = WindowsDurableProfileStore.ComputeRecordAuthenticator(key, store, signedBytes);

            Assert.IsFalse(CryptographicOperations.FixedTimeEquals(record, tip));
            Assert.IsFalse(CryptographicOperations.FixedTimeEquals(record, changed));
        }
        finally
        {
            Zero(key, store, signedBytes, record, tip, changed);
        }
    }

    [TestMethod]
    public void SealedTipBindsStoreProfileSequenceGenerationKindAndFinalRecordAuthenticator()
    {
        var key = Filled(AuthenticatorLength, 71);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 81);
        var previous = new byte[AuthenticatorLength];
        byte[]? serializedRecord = null;
        byte[]? serializedTip = null;
        try
        {
            var profile = CreateProfile();
            serializedRecord = WindowsDurableProfileStore.SerializeRecord(
                key, store, profile, DurableProfileStoreRecordKind.ProfileGeneration, 1, 1, previous, null);
            using var record = WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, serializedRecord);
            serializedTip = WindowsDurableProfileStore.SerializeSealedTip(
                key, store, profile, record.Kind, record.Sequence, record.Generation, record.Authenticator);
            using var tip = WindowsDurableProfileStore.ParseAndAuthenticateSealedTip(key, store, profile, serializedTip);

            WindowsDurableProfileStore.ValidateRecordAndSealedTipBinding(record, tip, 0, previous);
            Assert.AreEqual((ulong)1, tip.Sequence);
            Assert.AreEqual(1L, tip.Generation);
            Assert.AreEqual(DurableProfileStoreRecordKind.ProfileGeneration, tip.Kind);
        }
        finally
        {
            Zero(key, store, previous, serializedRecord, serializedTip);
        }
    }

    [TestMethod]
    public void RecordCodecRoundTripsNonceConsumptionWithAuthenticatedPayload()
    {
        var key = Filled(AuthenticatorLength, 91);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 101);
        var previous = Filled(AuthenticatorLength, 111);
        var nonce = Filled(WindowsDurableProfileStore.NonceLength, 121);
        byte[]? fingerprint = null;
        byte[]? serialized = null;
        try
        {
            var profile = CreateProfile();
            fingerprint = WindowsDurableProfileStore.ComputeNonceFingerprint(key, store, profile, 3, nonce);
            serialized = WindowsDurableProfileStore.SerializeRecord(
                key, store, profile, DurableProfileStoreRecordKind.NonceConsumption, 2, 3, previous, fingerprint);
            using var record = WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, serialized);

            Assert.AreEqual(DurableProfileStoreRecordKind.NonceConsumption, record.Kind);
            Assert.IsTrue(CryptographicOperations.FixedTimeEquals(fingerprint, record.NonceFingerprint!));
            WindowsDurableProfileStore.ValidateRecordStateTransition(1, 3, record.Kind, record.Generation, record.NonceFingerprint);
        }
        finally
        {
            Zero(key, store, previous, nonce, fingerprint, serialized);
        }
    }

    [TestMethod]
    public void SyntheticTopologyClassifiesEveryPendingCrashState()
    {
        Assert.AreEqual(DurableRecordTipArtifactState.Pending,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(true, false, false, false));
        Assert.AreEqual(DurableRecordTipArtifactState.Pending,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, true, true, false));
        Assert.AreEqual(DurableRecordTipArtifactState.Pending,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, false, true, true));
    }

    [TestMethod]
    public void SyntheticTopologyClassifiesOnlyCompleteAndAbsentPairsAsSuch()
    {
        Assert.AreEqual(DurableRecordTipArtifactState.Complete,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, true, false, true));
        Assert.AreEqual(DurableRecordTipArtifactState.Absent,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, false, false, false));
    }

    [TestMethod]
    public void SyntheticTopologyClassifiesRecordOnlyAndTipOnlyAsIncomplete()
    {
        Assert.AreEqual(DurableRecordTipArtifactState.Incomplete,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, true, false, false));
        Assert.AreEqual(DurableRecordTipArtifactState.Incomplete,
            WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, false, false, true));
    }

    [TestMethod]
    public void ReplayRulesRejectPendingArtifactsBeforeAnyPairCanBeApplied()
    {
        Assert.AreEqual(DurableReplayDisposition.RejectPending,
            WindowsDurableProfileStore.GetReplayDisposition(1, DurableRecordTipArtifactState.Pending));
        Assert.AreEqual(DurableReplayDisposition.RejectPending,
            WindowsDurableProfileStore.GetReplayDisposition(17, DurableRecordTipArtifactState.Pending));
    }

    [TestMethod]
    public void ReplayRulesRejectGapsAndLaterArtifacts()
    {
        Assert.AreEqual(DurableReplayDisposition.MissingFirst,
            WindowsDurableProfileStore.GetReplayDisposition(1, DurableRecordTipArtifactState.Absent));
        Assert.AreEqual(DurableReplayDisposition.Finish,
            WindowsDurableProfileStore.GetReplayDisposition(2, DurableRecordTipArtifactState.Absent));
        Assert.AreEqual(DurableReplayDisposition.RejectOutOfRange,
            WindowsDurableProfileStore.GetReplayDisposition((ulong)WindowsDurableProfileStore.MaximumRecordCount + 1,
                DurableRecordTipArtifactState.Complete));
    }

    [TestMethod]
    public void RetainedTipMakesRecordSuffixDeletionFailClosed()
    {
        var artifacts = WindowsDurableProfileStore.ClassifyRecordTipArtifacts(false, false, false, true);
        Assert.AreEqual(DurableRecordTipArtifactState.Incomplete, artifacts);
        Assert.AreEqual(DurableReplayDisposition.RejectIncomplete,
            WindowsDurableProfileStore.GetReplayDisposition(9, artifacts));
    }

    [TestMethod]
    public void RecordAndTipTamperAreRejectedByAuthenticatedParsers()
    {
        var key = Filled(AuthenticatorLength, 131);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 141);
        var previous = new byte[AuthenticatorLength];
        byte[]? record = null;
        byte[]? tip = null;
        try
        {
            var profile = CreateProfile();
            record = WindowsDurableProfileStore.SerializeRecord(
                key, store, profile, DurableProfileStoreRecordKind.ProfileGeneration, 1, 1, previous, null);
            using var parsed = WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, record);
            tip = WindowsDurableProfileStore.SerializeSealedTip(
                key, store, profile, parsed.Kind, parsed.Sequence, parsed.Generation, parsed.Authenticator);
            record[0] ^= 0x01;
            tip[0] ^= 0x01;

            AssertInvalidData(() => WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, record));
            AssertInvalidData(() => WindowsDurableProfileStore.ParseAndAuthenticateSealedTip(key, store, profile, tip));
        }
        finally
        {
            Zero(key, store, previous, record, tip);
        }
    }

    [TestMethod]
    public void RecordAndTipBindingRejectsAValidButMismatchedTip()
    {
        var key = Filled(AuthenticatorLength, 151);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 161);
        var previous = new byte[AuthenticatorLength];
        var unrelatedAuthenticator = Filled(AuthenticatorLength, 171);
        byte[]? recordBytes = null;
        byte[]? tipBytes = null;
        try
        {
            var profile = CreateProfile();
            recordBytes = WindowsDurableProfileStore.SerializeRecord(
                key, store, profile, DurableProfileStoreRecordKind.ProfileGeneration, 1, 1, previous, null);
            using var record = WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, recordBytes);
            tipBytes = WindowsDurableProfileStore.SerializeSealedTip(
                key, store, profile, record.Kind, record.Sequence, record.Generation, unrelatedAuthenticator);
            using var tip = WindowsDurableProfileStore.ParseAndAuthenticateSealedTip(key, store, profile, tipBytes);

            AssertInvalidData(() => WindowsDurableProfileStore.ValidateRecordAndSealedTipBinding(record, tip, 0, previous));
        }
        finally
        {
            Zero(key, store, previous, unrelatedAuthenticator, recordBytes, tipBytes);
        }
    }

    [TestMethod]
    public void GenerationAndNonceTransitionsUseTheSharedProductionRule()
    {
        var fingerprint = Filled(AuthenticatorLength, 181);
        try
        {
            WindowsDurableProfileStore.ValidateRecordStateTransition(
                0, 0, DurableProfileStoreRecordKind.ProfileGeneration, 1, null);
            WindowsDurableProfileStore.ValidateRecordStateTransition(
                1, 7, DurableProfileStoreRecordKind.NonceConsumption, 7, fingerprint);
            AssertInvalidData(() => WindowsDurableProfileStore.ValidateRecordStateTransition(
                1, 7, DurableProfileStoreRecordKind.NonceConsumption, 8, fingerprint));
            AssertInvalidData(() => WindowsDurableProfileStore.ValidateRecordStateTransition(
                1, 7, DurableProfileStoreRecordKind.ProfileGeneration, 7, null));
        }
        finally
        {
            Zero(fingerprint);
        }
    }

    [TestMethod]
    public void MaximumSequence128ProducesAValidReplayFinishAndLaterValuesReject()
    {
        var key = Filled(AuthenticatorLength, 191);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 201);
        var previous = new byte[AuthenticatorLength];
        byte[]? recordBytes = null;
        byte[]? tipBytes = null;
        try
        {
            var profile = CreateProfile();
            recordBytes = WindowsDurableProfileStore.SerializeRecord(
                key,
                store,
                profile,
                DurableProfileStoreRecordKind.ProfileGeneration,
                128,
                128,
                previous,
                null);
            using var record = WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, recordBytes);
            tipBytes = WindowsDurableProfileStore.SerializeSealedTip(
                key, store, profile, record.Kind, record.Sequence, record.Generation, record.Authenticator);
            using var tip = WindowsDurableProfileStore.ParseAndAuthenticateSealedTip(key, store, profile, tipBytes);

            WindowsDurableProfileStore.ValidateRecordAndSealedTipBinding(
                record,
                tip,
                127,
                previous);
            Assert.AreEqual(DurableReplayDisposition.ApplyAndFinish,
                WindowsDurableProfileStore.GetReplayDisposition(record.Sequence, DurableRecordTipArtifactState.Complete));
            Assert.AreEqual(128UL, record.Sequence);
            Assert.IsFalse(WindowsDurableProfileStore.IsRecordSequenceInBounds(129));
            Assert.IsFalse(WindowsDurableProfileStore.IsRecordSequenceInBounds(ulong.MaxValue));
            AssertArgumentRejected(() => WindowsDurableProfileStore.SerializeRecord(
                key,
                store,
                profile,
                DurableProfileStoreRecordKind.ProfileGeneration,
                129,
                129,
                previous,
                null));
            AssertArgumentRejected(() => WindowsDurableProfileStore.SerializeSealedTip(
                key,
                store,
                profile,
                DurableProfileStoreRecordKind.ProfileGeneration,
                ulong.MaxValue,
                129,
                previous));
        }
        finally
        {
            Zero(key, store, previous, recordBytes, tipBytes);
        }
    }

    [TestMethod]
    public void HeaderRecordAndTipBoundsAreEnforcedByTheProductionCodec()
    {
        var key = Filled(AuthenticatorLength, 211);
        var store = Filled(WindowsDurableProfileStore.StoreIdentifierLength, 221);
        var profile = CreateProfile();
        var shortRecord = new byte[127];
        var oversizedTip = new byte[WindowsDurableProfileStore.MaximumTipBytes + 1];
        try
        {
            AssertInvalidData(() => WindowsDurableProfileStore.ParseAndAuthenticateRecord(key, store, profile, shortRecord));
            AssertInvalidData(() => WindowsDurableProfileStore.ParseAndAuthenticateSealedTip(key, store, profile, oversizedTip));
        }
        finally
        {
            Zero(key, store, shortRecord, oversizedTip);
        }
    }

    [TestMethod]
    public void ReceiptBindsSealedTipAndContainsNoKeyOrNonce()
    {
        var properties = typeof(DurableProfileStoreReceipt).GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsTrue(properties.Any(property => property.Name == "SealedTipDigest"));
        Assert.IsFalse(properties.Any(property =>
            property.PropertyType == typeof(byte[]) ||
            property.Name.Contains("Nonce", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Key", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void CallerOwnedHandleRemainsOpenAfterFailClosedAdmission()
    {
        WithTemporaryDirectory(root =>
        {
            using var handle = OpenDirectory(root, DurableProductionProfile.RootAccessMask);
            AssertStoreRejected(() => WindowsDurableProfileStore.CreateNew(handle, CreateProfile(), 1));
            Assert.IsFalse(handle.IsClosed);
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(root).Count());
        });
    }

    [TestMethod]
    public void NativeAbiUsesFixedWidthObjectBasicInformationAndRestrictedImports()
    {
        var imports = typeof(WindowsDurableProfileStore).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() is not null)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var objectBasicInformation = typeof(WindowsDurableProfileStore)
            .GetNestedType("PublicObjectBasicInformation", BindingFlags.NonPublic)!;

        CollectionAssert.Contains(imports, "NtCreateFile");
        CollectionAssert.Contains(imports, "NtOpenFile");
        CollectionAssert.Contains(imports, "NtQueryObject");
        CollectionAssert.Contains(imports, "NtSetInformationFile");
        CollectionAssert.Contains(imports, "GetSecurityInfo");
        CollectionAssert.Contains(imports, "RtlSecureZeroMemory");
        Assert.AreEqual(sizeof(uint) * 14, Marshal.SizeOf(objectBasicInformation));
        Assert.AreEqual(new IntPtr(sizeof(uint)), Marshal.OffsetOf(objectBasicInformation, "GrantedAccess"));
        Assert.AreEqual(IntPtr.Size * 2, Marshal.SizeOf(typeof(WindowsDurableProfileStore)
            .GetNestedType("IoStatusBlock", BindingFlags.NonPublic)!));
        Assert.AreEqual(24, Marshal.SizeOf(typeof(WindowsDurableProfileStore)
            .GetNestedType("FileIdInfo", BindingFlags.NonPublic)!));
    }

    [TestMethod]
    public void ProductionTypesAreInternalSealedAndNonWire()
    {
        foreach (var type in new[]
                 {
                     typeof(DurableProductionProfile),
                     typeof(WindowsDurableProfileStore),
                     typeof(DurableProfileStoreReceipt),
                 })
        {
            Assert.IsTrue(type.IsNotPublic);
            Assert.IsTrue(type.IsSealed);
            Assert.IsFalse(type.GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Any());
        }

        Assert.IsTrue(typeof(WindowsDurableProfileStore.AuthenticatedRecord).IsSealed);
        Assert.IsTrue(typeof(WindowsDurableProfileStore.AuthenticatedSealedTip).IsSealed);
        Assert.IsFalse(typeof(DurableProfileStoreReceipt).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.Name.Contains("Authorize", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Grant", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void NoAccessSystemSecurityFailsBeforeAnyStoreArtifactAndLeavesCallerOpen()
    {
        WithTemporaryDirectory(root =>
        {
            using var handle = OpenDirectory(root, DurableProductionProfile.RootAccessMask);
            Assert.AreEqual(DurableProductionProfile.RootAccessMask, WindowsDurableProfileStore.QueryGrantedAccess(handle));
            AssertStoreRejected(() => WindowsDurableProfileStore.CreateNew(handle, CreateProfile(), 1));
            Assert.IsFalse(handle.IsClosed);
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(root).Count());
        });
    }

    private static DurableProductionProfile CreateProfile() =>
        new("profile-a", WindowsSid.ParseService(ServiceSidText));

    private static CommonAce AllowAce(uint accessMask, SecurityIdentifier sid) =>
        new(AceFlags.None, AceQualifier.AccessAllowed, unchecked((int)accessMask), sid, false, null);

    private static RawSecurityDescriptor CreateDescriptor(
        uint requiredControlFlags,
        SecurityIdentifier owner,
        SecurityIdentifier group,
        RawAcl? systemAcl,
        params GenericAce[] daclAces)
    {
        var dacl = new RawAcl(2, daclAces.Length);
        for (var index = 0; index < daclAces.Length; index++)
        {
            dacl.InsertAce(index, daclAces[index]);
        }

        var descriptor = new RawSecurityDescriptor(
            (ControlFlags)requiredControlFlags,
            owner,
            group,
            systemAcl,
            dacl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return new RawSecurityDescriptor(bytes, 0);
    }

    private static RawSecurityDescriptor ReadDescriptor(byte[] bytes) => new(bytes, 0);

    private static byte[] Filled(int length, byte first)
    {
        var result = new byte[length];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = unchecked((byte)(first + index));
        }

        return result;
    }

    private static void Zero(params byte[]?[] buffers)
    {
        foreach (var buffer in buffers)
        {
            if (buffer is not null)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "vfxcomposer-durable-profile-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            action(root);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: false);
            }
        }
    }

    private static void AssertArgumentRejected(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        Assert.Fail("Expected the durable profile input to be rejected.");
    }

    private static void AssertInvalidData(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        Assert.Fail("Expected the durable store data to be rejected.");
    }

    private static void AssertStoreRejected(Action action) => AssertInvalidData(action);

    private static SafeFileHandle OpenDirectory(string path, uint desiredAccess)
    {
        var handle = CreateFileW(
            path,
            desiredAccess,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new InvalidOperationException("The no-privilege durable root could not be opened.");
        }

        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}

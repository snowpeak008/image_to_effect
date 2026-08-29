using System;

namespace VFXComposer.Editor.W24.S6.Worker.Protocol
{
    internal sealed class W24S6WorkerTypedHash
    {
        internal W24S6WorkerTypedHash(string typeTag, string digest)
        {
            TypeTag = typeTag;
            Digest = digest;
        }

        internal string TypeTag { get; private set; }
        internal string Digest { get; private set; }
    }

    internal abstract class W24S6WorkerLifecycleMessage
    {
        protected W24S6WorkerLifecycleMessage(
            string protocolVersion,
            string messageKind,
            string requestId,
            string leaseId,
            long brokerGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash selfHash)
        {
            ProtocolVersion = protocolVersion;
            MessageKind = messageKind;
            RequestId = requestId;
            LeaseId = leaseId;
            BrokerGeneration = brokerGeneration;
            LeaseGeneration = leaseGeneration;
            WorkerSessionId = workerSessionId;
            WorkerProcessEpoch = workerProcessEpoch;
            SelfHash = selfHash;
        }

        internal string ProtocolVersion { get; private set; }
        internal string MessageKind { get; private set; }
        internal string RequestId { get; private set; }
        internal string LeaseId { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal long LeaseGeneration { get; private set; }
        internal string WorkerSessionId { get; private set; }
        internal string WorkerProcessEpoch { get; private set; }
        internal W24S6WorkerTypedHash SelfHash { get; private set; }
    }

    internal sealed class W24S6WorkerProjectHandleGrant : W24S6WorkerLifecycleMessage
    {
        internal W24S6WorkerProjectHandleGrant(
            string protocolVersion,
            string messageKind,
            string requestId,
            string leaseId,
            string registeredProjectId,
            W24S6WorkerTypedHash projectIdentity,
            W24S6WorkerTypedHash volumeIdentity,
            W24S6WorkerTypedHash repositoryIdentity,
            W24S6WorkerTypedHash projectRootIdentity,
            long brokerGeneration,
            long registrationGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            string handleEncoding,
            string volumeHandle,
            string repositoryHandle,
            string projectRootHandle,
            W24S6WorkerTypedHash selfHash)
            : base(protocolVersion, messageKind, requestId, leaseId, brokerGeneration, leaseGeneration,
                workerSessionId, workerProcessEpoch, selfHash)
        {
            RegisteredProjectId = registeredProjectId;
            ProjectIdentity = projectIdentity;
            VolumeIdentity = volumeIdentity;
            RepositoryIdentity = repositoryIdentity;
            ProjectRootIdentity = projectRootIdentity;
            RegistrationGeneration = registrationGeneration;
            HandleEncoding = handleEncoding;
            VolumeHandle = volumeHandle;
            RepositoryHandle = repositoryHandle;
            ProjectRootHandle = projectRootHandle;
        }

        internal string RegisteredProjectId { get; private set; }
        internal W24S6WorkerTypedHash ProjectIdentity { get; private set; }
        internal W24S6WorkerTypedHash VolumeIdentity { get; private set; }
        internal W24S6WorkerTypedHash RepositoryIdentity { get; private set; }
        internal W24S6WorkerTypedHash ProjectRootIdentity { get; private set; }
        internal long RegistrationGeneration { get; private set; }
        internal string HandleEncoding { get; private set; }
        internal string VolumeHandle { get; private set; }
        internal string RepositoryHandle { get; private set; }
        internal string ProjectRootHandle { get; private set; }
    }

#if UNITY_INCLUDE_TESTS
    internal sealed class W24S6WorkerProjectHandleGrantAcknowledgement : W24S6WorkerLifecycleMessage
    {
        internal W24S6WorkerProjectHandleGrantAcknowledgement(
            string protocolVersion,
            string messageKind,
            string requestId,
            string leaseId,
            long brokerGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash grantSelfHash,
            string disposition,
            W24S6WorkerTypedHash selfHash)
            : base(protocolVersion, messageKind, requestId, leaseId, brokerGeneration, leaseGeneration,
                workerSessionId, workerProcessEpoch, selfHash)
        {
            GrantSelfHash = grantSelfHash;
            Disposition = disposition;
        }

        internal W24S6WorkerTypedHash GrantSelfHash { get; private set; }
        internal string Disposition { get; private set; }
    }
#endif

    internal sealed class W24S6WorkerProjectHandleRevoke : W24S6WorkerLifecycleMessage
    {
        internal W24S6WorkerProjectHandleRevoke(
            string protocolVersion,
            string messageKind,
            string requestId,
            string leaseId,
            long brokerGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash grantSelfHash,
            string reasonCode,
            W24S6WorkerTypedHash selfHash)
            : base(protocolVersion, messageKind, requestId, leaseId, brokerGeneration, leaseGeneration,
                workerSessionId, workerProcessEpoch, selfHash)
        {
            GrantSelfHash = grantSelfHash;
            ReasonCode = reasonCode;
        }

        internal W24S6WorkerTypedHash GrantSelfHash { get; private set; }
        internal string ReasonCode { get; private set; }
    }

    /// <summary>
    /// Immutable Unity projection of the host-owned C2 Worker project locator.
    /// It carries identity correlations only and cannot locate or read a project.
    /// </summary>
    internal sealed class W24S6WorkerProjectLocator
    {
        internal W24S6WorkerProjectLocator(
            string protocolVersion,
            string messageKind,
            string requestId,
            string registeredProjectId,
            W24S6WorkerTypedHash projectIdentity,
            W24S6WorkerTypedHash volumeIdentity,
            W24S6WorkerTypedHash repositoryIdentity,
            W24S6WorkerTypedHash projectRootIdentity,
            long brokerGeneration,
            long registrationGeneration,
            long enrollmentGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash selfHash)
        {
            ProtocolVersion = protocolVersion;
            MessageKind = messageKind;
            RequestId = requestId;
            RegisteredProjectId = registeredProjectId;
            ProjectIdentity = projectIdentity;
            VolumeIdentity = volumeIdentity;
            RepositoryIdentity = repositoryIdentity;
            ProjectRootIdentity = projectRootIdentity;
            BrokerGeneration = brokerGeneration;
            RegistrationGeneration = registrationGeneration;
            EnrollmentGeneration = enrollmentGeneration;
            WorkerSessionId = workerSessionId;
            WorkerProcessEpoch = workerProcessEpoch;
            SelfHash = selfHash;
        }

        internal string ProtocolVersion { get; private set; }
        internal string MessageKind { get; private set; }
        internal string RequestId { get; private set; }
        internal string RegisteredProjectId { get; private set; }
        internal W24S6WorkerTypedHash ProjectIdentity { get; private set; }
        internal W24S6WorkerTypedHash VolumeIdentity { get; private set; }
        internal W24S6WorkerTypedHash RepositoryIdentity { get; private set; }
        internal W24S6WorkerTypedHash ProjectRootIdentity { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal long RegistrationGeneration { get; private set; }
        internal long EnrollmentGeneration { get; private set; }
        internal string WorkerSessionId { get; private set; }
        internal string WorkerProcessEpoch { get; private set; }
        internal W24S6WorkerTypedHash SelfHash { get; private set; }
    }

#if UNITY_INCLUDE_TESTS
    internal sealed class W24S6WorkerProjectLocatorAcknowledgement
    {
        internal W24S6WorkerProjectLocatorAcknowledgement(
            string protocolVersion,
            string messageKind,
            string requestId,
            string registeredProjectId,
            long brokerGeneration,
            long registrationGeneration,
            long enrollmentGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash locatorSelfHash,
            string disposition,
            W24S6WorkerTypedHash selfHash)
        {
            ProtocolVersion = protocolVersion;
            MessageKind = messageKind;
            RequestId = requestId;
            RegisteredProjectId = registeredProjectId;
            BrokerGeneration = brokerGeneration;
            RegistrationGeneration = registrationGeneration;
            EnrollmentGeneration = enrollmentGeneration;
            WorkerSessionId = workerSessionId;
            WorkerProcessEpoch = workerProcessEpoch;
            LocatorSelfHash = locatorSelfHash;
            Disposition = disposition;
            SelfHash = selfHash;
        }

        internal string ProtocolVersion { get; private set; }
        internal string MessageKind { get; private set; }
        internal string RequestId { get; private set; }
        internal string RegisteredProjectId { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal long RegistrationGeneration { get; private set; }
        internal long EnrollmentGeneration { get; private set; }
        internal string WorkerSessionId { get; private set; }
        internal string WorkerProcessEpoch { get; private set; }
        internal W24S6WorkerTypedHash LocatorSelfHash { get; private set; }
        internal string Disposition { get; private set; }
        internal W24S6WorkerTypedHash SelfHash { get; private set; }
    }

    internal sealed class W24S6WorkerProjectHandleRevokeAcknowledgement : W24S6WorkerLifecycleMessage
    {
        internal W24S6WorkerProjectHandleRevokeAcknowledgement(
            string protocolVersion,
            string messageKind,
            string requestId,
            string leaseId,
            long brokerGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            W24S6WorkerTypedHash grantSelfHash,
            W24S6WorkerTypedHash revokeSelfHash,
            string disposition,
            W24S6WorkerTypedHash selfHash)
            : base(protocolVersion, messageKind, requestId, leaseId, brokerGeneration, leaseGeneration,
                workerSessionId, workerProcessEpoch, selfHash)
        {
            GrantSelfHash = grantSelfHash;
            RevokeSelfHash = revokeSelfHash;
            Disposition = disposition;
        }

        internal W24S6WorkerTypedHash GrantSelfHash { get; private set; }
        internal W24S6WorkerTypedHash RevokeSelfHash { get; private set; }
        internal string Disposition { get; private set; }
    }
#endif

    internal sealed class W24S6WorkerProtocolException : Exception
    {
        internal const string MalformedMessage = "W24WKR001";

        internal W24S6WorkerProtocolException()
            : base(MalformedMessage)
        {
        }
    }
}

using System;

namespace VFXComposer.Editor.W24.Workflow
{
    /// <summary>W24 quality maturity. VISUAL_PENDING is deliberately not a quality level.</summary>
    public enum W24MaturityLevel
    {
        L0_InvalidOrMissing,
        L1_FunctionalProtocolComplete,
        L2_VisualPlaceholder,
        L3_ProductionCandidate,
        L4_UserSignedProductionReady
    }

    public enum W24WorkingStatus
    {
        None,
        VISUAL_PENDING,
        AwaitingMachineGate,
        AwaitingVisualQa,
        AwaitingRecapture,
        AwaitingDesignRevision,
        AwaitingUserConfirmation,
        AwaitingOrdinaryUserSignoff,
        AwaitingMarkedUserUpgrade,
        CaptureBlocked,
        NeedsUserDecision
    }

    public enum W24CandidateId { C0, C1, C2 }
    public enum W24MachineGateVerdict { MACHINE_PASS, MACHINE_FAIL }

    /// <summary>Exactly the five routes permitted by the visual-QA review protocol.</summary>
    public enum W24VisualQaRoute
    {
        VISUAL_PASS,
        VISUAL_FAIL,
        EVIDENCE_INVALID,
        CONTRACT_AMBIGUOUS,
        VISUAL_UNCERTAIN
    }

    /// <summary>S0a has exactly these two completed terminal states; null means S0a is not yet terminal.</summary>
    public enum W24S0aTerminalStatus { S0A_GATE_QUALIFIED, S0A_ADVISORY_ONLY }
    public enum W24QaGateAuthority { OrdinaryL3Gate, AdvisoryOnly }
    public enum W24UserEntryPath { None, OrdinarySignoff, MarkedUpgrade }
    public enum W24UserDecision { Signed, Rejected }

    public sealed class W24WorkflowHistoryEntry
    {
        internal W24WorkflowHistoryEntry(string action, W24WorkingStatus status, W24CandidateIdentity candidate)
        {
            Action = action;
            Status = status;
            CandidateId = candidate.CandidateId;
            ContractRevision = candidate.ContractRevision;
        }

        public string Action { get; private set; }
        public W24WorkingStatus Status { get; private set; }
        public W24CandidateId CandidateId { get; private set; }
        public int ContractRevision { get; private set; }
    }

    public sealed class W24CandidateIdentity
    {
        public W24CandidateIdentity(W24CandidateId candidateId, int contractRevision, string buildHash, string captureProfileHash)
        {
            if (contractRevision < 1) throw new ArgumentOutOfRangeException(nameof(contractRevision));
            if (string.IsNullOrEmpty(buildHash)) throw new ArgumentException("A build hash is required.", nameof(buildHash));
            if (string.IsNullOrEmpty(captureProfileHash)) throw new ArgumentException("A capture profile hash is required.", nameof(captureProfileHash));
            CandidateId = candidateId;
            ContractRevision = contractRevision;
            BuildHash = buildHash;
            CaptureProfileHash = captureProfileHash;
        }

        public W24CandidateId CandidateId { get; private set; }
        public int ContractRevision { get; private set; }
        public string BuildHash { get; private set; }
        public string CaptureProfileHash { get; private set; }
    }

    public sealed class W24WorkflowState
    {
        private readonly System.Collections.Generic.List<W24WorkingStatus> statusHistory = new System.Collections.Generic.List<W24WorkingStatus>();
        private readonly System.Collections.Generic.List<W24WorkflowHistoryEntry> history = new System.Collections.Generic.List<W24WorkflowHistoryEntry>();

        public W24WorkflowState(W24CandidateIdentity candidate, W24S0aTerminalStatus? s0aTerminalStatus)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            S0aTerminalStatus = s0aTerminalStatus;
            Maturity = W24MaturityLevel.L2_VisualPlaceholder;
            WorkingStatus = W24WorkingStatus.AwaitingMachineGate;
            statusHistory.Add(WorkingStatus);
            history.Add(new W24WorkflowHistoryEntry("workflow-started", WorkingStatus, Candidate));
        }

        public W24CandidateIdentity Candidate { get; private set; }
        public W24S0aTerminalStatus? S0aTerminalStatus { get; private set; }
        public W24MaturityLevel Maturity { get; private set; }
        public W24WorkingStatus WorkingStatus { get; private set; }
        public W24UserEntryPath UserEntryPath { get; private set; }
        public int RecaptureFailures { get; private set; }
        public int ConsecutiveContractReopens { get; private set; }
        public bool RequiresUserConfirmation { get; private set; }
        public System.Collections.Generic.IReadOnlyList<W24WorkingStatus> StatusHistory => statusHistory;
        public System.Collections.Generic.IReadOnlyList<W24WorkflowHistoryEntry> History => history;

        public W24QaGateAuthority QaGateAuthority => S0aTerminalStatus == W24S0aTerminalStatus.S0A_GATE_QUALIFIED
            ? W24QaGateAuthority.OrdinaryL3Gate
            : W24QaGateAuthority.AdvisoryOnly;

        internal void SetCandidate(W24CandidateIdentity candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            Candidate = candidate;
            RecaptureFailures = 0;
            UserEntryPath = W24UserEntryPath.None;
            RecordStatus("candidate-advanced", W24WorkingStatus.AwaitingMachineGate);
        }

        internal void SetMaturity(W24MaturityLevel maturity) { Maturity = maturity; }
        internal void SetWorkingStatus(W24WorkingStatus status)
        {
            RecordStatus("status-updated", status);
        }
        internal void SetUserEntry(W24UserEntryPath path) { UserEntryPath = path; }
        internal void IncrementRecaptureFailures() { RecaptureFailures++; }
        internal void IncrementContractReopens() { ConsecutiveContractReopens++; }
        internal void ClearContractReopenConfirmation() { RequiresUserConfirmation = false; }
        internal void ResetContractReopens() { ConsecutiveContractReopens = 0; }
        internal void RequireContractReopenConfirmation() { RequiresUserConfirmation = true; }
        internal void ReplaceCandidateAfterContractRevision(W24CandidateIdentity candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            Candidate = candidate;
            RecaptureFailures = 0;
            UserEntryPath = W24UserEntryPath.None;
            RecordStatus("contract-revision-applied", W24WorkingStatus.AwaitingMachineGate);
        }

        private void RecordStatus(string action, W24WorkingStatus status)
        {
            WorkingStatus = status;
            statusHistory.Add(status);
            history.Add(new W24WorkflowHistoryEntry(action, status, Candidate));
        }
    }

    /// <summary>Pure workflow aggregator for W24 §5.1. It does not judge visual evidence.</summary>
    public static class W24WorkflowAggregator
    {
        public static W24WorkflowState Start(W24CandidateIdentity candidate, W24S0aTerminalStatus? s0aTerminalStatus)
        {
            return new W24WorkflowState(candidate, s0aTerminalStatus);
        }

        public static void ApplyMachineVerdict(W24WorkflowState state, W24MachineGateVerdict verdict, W24CandidateIdentity nextCandidate = null)
        {
            Require(state, W24WorkingStatus.AwaitingMachineGate);
            if (verdict == W24MachineGateVerdict.MACHINE_PASS)
            {
                state.SetWorkingStatus(W24WorkingStatus.AwaitingVisualQa);
                return;
            }

            AdvanceOrEscalate(state, nextCandidate);
        }

        public static void ApplyVisualQaRoute(W24WorkflowState state, W24VisualQaRoute route, W24CandidateIdentity nextCandidate = null)
        {
            Require(state, W24WorkingStatus.AwaitingVisualQa);
            // Only a non-ambiguous Visual QA route resolves this sequence. Machine-gate progress and
            // applying a revised contract cannot establish that the visual contract is now unambiguous.
            if (route != W24VisualQaRoute.CONTRACT_AMBIGUOUS) state.ResetContractReopens();
            switch (route)
            {
                case W24VisualQaRoute.VISUAL_PASS:
                    if (state.QaGateAuthority == W24QaGateAuthority.OrdinaryL3Gate)
                    {
                        state.SetMaturity(W24MaturityLevel.L3_ProductionCandidate);
                        state.SetUserEntry(W24UserEntryPath.OrdinarySignoff);
                        state.SetWorkingStatus(W24WorkingStatus.AwaitingOrdinaryUserSignoff);
                    }
                    else
                    {
                        state.SetMaturity(W24MaturityLevel.L2_VisualPlaceholder);
                        state.SetUserEntry(W24UserEntryPath.MarkedUpgrade);
                        state.SetWorkingStatus(W24WorkingStatus.AwaitingMarkedUserUpgrade);
                    }
                    return;
                case W24VisualQaRoute.VISUAL_FAIL:
                    AdvanceOrEscalate(state, nextCandidate);
                    return;
                case W24VisualQaRoute.EVIDENCE_INVALID:
                    state.IncrementRecaptureFailures();
                    if (state.RecaptureFailures == 1)
                    {
                        state.SetWorkingStatus(W24WorkingStatus.AwaitingRecapture);
                    }
                    else
                    {
                        state.SetWorkingStatus(W24WorkingStatus.CaptureBlocked);
                        state.SetUserEntry(W24UserEntryPath.MarkedUpgrade);
                        state.SetWorkingStatus(W24WorkingStatus.NeedsUserDecision);
                    }
                    return;
                case W24VisualQaRoute.CONTRACT_AMBIGUOUS:
                    state.IncrementContractReopens();
                    if (state.ConsecutiveContractReopens >= 2)
                    {
                        state.RequireContractReopenConfirmation();
                        state.SetWorkingStatus(W24WorkingStatus.AwaitingUserConfirmation);
                    }
                    else
                    {
                        state.SetWorkingStatus(W24WorkingStatus.AwaitingDesignRevision);
                    }
                    return;
                case W24VisualQaRoute.VISUAL_UNCERTAIN:
                    state.SetUserEntry(W24UserEntryPath.MarkedUpgrade);
                    state.SetWorkingStatus(W24WorkingStatus.AwaitingMarkedUserUpgrade);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        public static void ApplyRecaptureCompleted(W24WorkflowState state)
        {
            Require(state, W24WorkingStatus.AwaitingRecapture);
            state.SetWorkingStatus(W24WorkingStatus.AwaitingVisualQa);
        }

        public static void ApplyContractRevision(W24WorkflowState state, W24CandidateIdentity redesignedCandidate, bool userConfirmedSecondConsecutiveReopen)
        {
            if (state.WorkingStatus == W24WorkingStatus.AwaitingUserConfirmation && !userConfirmedSecondConsecutiveReopen)
                throw new InvalidOperationException("The second consecutive contract reopening requires user confirmation.");
            if (state.WorkingStatus != W24WorkingStatus.AwaitingDesignRevision && state.WorkingStatus != W24WorkingStatus.AwaitingUserConfirmation)
                throw new InvalidOperationException("A contract revision is only valid after CONTRACT_AMBIGUOUS.");
            if (redesignedCandidate == null) throw new ArgumentNullException(nameof(redesignedCandidate));
            if (redesignedCandidate.CandidateId != W24CandidateId.C0)
                throw new ArgumentException("A contract reopening starts a new C0 candidate.", nameof(redesignedCandidate));
            if (redesignedCandidate.ContractRevision <= state.Candidate.ContractRevision)
                throw new ArgumentOutOfRangeException(nameof(redesignedCandidate), "contractRevision must increase.");
            state.ClearContractReopenConfirmation();
            if (userConfirmedSecondConsecutiveReopen) state.ResetContractReopens();
            state.ReplaceCandidateAfterContractRevision(redesignedCandidate);
        }

        public static void ApplyUserDecision(W24WorkflowState state, W24UserDecision decision, W24CandidateIdentity signedIdentity)
        {
            if (state.WorkingStatus != W24WorkingStatus.AwaitingOrdinaryUserSignoff && state.WorkingStatus != W24WorkingStatus.AwaitingMarkedUserUpgrade && state.WorkingStatus != W24WorkingStatus.NeedsUserDecision)
                throw new InvalidOperationException("A user decision is not currently expected.");
            if (decision == W24UserDecision.Signed)
            {
                throw new InvalidOperationException("L4 promotion is disabled until a host-owned opaque user-signoff authority is available; a caller-supplied workflow identity is not a user signature.");
            }
            state.SetWorkingStatus(W24WorkingStatus.NeedsUserDecision);
        }

        private static bool SameSigningIdentity(W24CandidateIdentity a, W24CandidateIdentity b)
        {
            return a.ContractRevision == b.ContractRevision && string.Equals(a.BuildHash, b.BuildHash, StringComparison.Ordinal) && string.Equals(a.CaptureProfileHash, b.CaptureProfileHash, StringComparison.Ordinal);
        }

        private static void AdvanceOrEscalate(W24WorkflowState state, W24CandidateIdentity nextCandidate)
        {
            if (state.Candidate.CandidateId == W24CandidateId.C2)
            {
                state.SetUserEntry(W24UserEntryPath.MarkedUpgrade);
                state.SetWorkingStatus(W24WorkingStatus.NeedsUserDecision);
                return;
            }
            if (nextCandidate == null || (int)nextCandidate.CandidateId != (int)state.Candidate.CandidateId + 1)
                throw new ArgumentException("C0 and C1 failures require the immediately following immutable candidate.", nameof(nextCandidate));
            if (nextCandidate.ContractRevision != state.Candidate.ContractRevision)
                throw new ArgumentException("A normal candidate retry cannot change contractRevision.", nameof(nextCandidate));
            state.SetCandidate(nextCandidate);
        }

        private static void Require(W24WorkflowState state, W24WorkingStatus expected)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.WorkingStatus != expected) throw new InvalidOperationException("Expected workflow state " + expected + " but was " + state.WorkingStatus + ".");
        }
    }
}

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Closed set of UI string keys. Each constant's value equals its own name so XAML indexer bindings
/// (<c>{Binding Localization[DashboardTitle]}</c>) and C# call sites spell the key identically; the catalog parity
/// test pins that invariant.
/// </summary>
public static class UiStringKeys
{
    public const string AppProductName = "AppProductName";

    public const string MainWindowReadOnlySessionNotice = "MainWindowReadOnlySessionNotice";
    public const string MainWindowConnectAction = "MainWindowConnectAction";
    public const string MainWindowSelectProjectAction = "MainWindowSelectProjectAction";
    public const string MainWindowReadAction = "MainWindowReadAction";
    public const string MainWindowRestartAction = "MainWindowRestartAction";
    public const string MainWindowConnectionConnected = "MainWindowConnectionConnected";
    public const string MainWindowConnectionDisconnected = "MainWindowConnectionDisconnected";
    public const string MainWindowProjectNone = "MainWindowProjectNone";
    public const string MainWindowProjectRegistered = "MainWindowProjectRegistered";
    public const string MainWindowProjectSelected = "MainWindowProjectSelected";
    public const string MainWindowReadNone = "MainWindowReadNone";
    public const string MainWindowReadAccepted = "MainWindowReadAccepted";
    public const string MainWindowReadRejected = "MainWindowReadRejected";

    public const string DashboardTitle = "DashboardTitle";
    public const string DashboardDescription = "DashboardDescription";
    public const string DashboardEmptyState = "DashboardEmptyState";
    public const string DashboardProjectConnectionHeading = "DashboardProjectConnectionHeading";
    public const string DashboardStatusDomainsHeading = "DashboardStatusDomainsHeading";
    public const string DashboardMachineStatus = "DashboardMachineStatus";
    public const string DashboardVisualStatus = "DashboardVisualStatus";
    public const string DashboardUserVerdictStatus = "DashboardUserVerdictStatus";
    public const string DashboardL3Status = "DashboardL3Status";
    public const string DashboardL4Status = "DashboardL4Status";

    public const string LibraryTitle = "LibraryTitle";
    public const string LibraryDescription = "LibraryDescription";
    public const string LibraryEmptyState = "LibraryEmptyState";

    public const string CreateTitle = "CreateTitle";
    public const string CreateDescription = "CreateDescription";
    public const string CreateEmptyState = "CreateEmptyState";

    public const string CreateRecipeDraftHeading = "CreateRecipeDraftHeading";
    public const string CreateRecipeNameWatermark = "CreateRecipeNameWatermark";
    public const string CreateDraftNotesWatermark = "CreateDraftNotesWatermark";
    public const string CreateGenerateRecipeHeading = "CreateGenerateRecipeHeading";
    public const string CreateGenerateRecipeNotice = "CreateGenerateRecipeNotice";
    public const string CreateEffectDescriptionWatermark = "CreateEffectDescriptionWatermark";
    public const string CreateGenerateDraftAction = "CreateGenerateDraftAction";
    public const string CreateCancelGenerationAction = "CreateCancelGenerationAction";
    public const string CreateConfirmDraftAction = "CreateConfirmDraftAction";
    public const string CreateValidationWatermark = "CreateValidationWatermark";
    public const string CreateDraftJsonWatermark = "CreateDraftJsonWatermark";
    public const string CreateChatHeading = "CreateChatHeading";
    public const string CreateChatNotice = "CreateChatNotice";
    public const string CreateChatPromptWatermark = "CreateChatPromptWatermark";
    public const string CreateSendChatAction = "CreateSendChatAction";
    public const string CreateChatResponseWatermark = "CreateChatResponseWatermark";

    public const string CreateChatStatusNotConfigured = "CreateChatStatusNotConfigured";
    public const string CreateChatStatusCompleted = "CreateChatStatusCompleted";
    public const string CreateChatStatusUnavailableWithCode = "CreateChatStatusUnavailableWithCode";
    public const string CreateChatStatusCancelled = "CreateChatStatusCancelled";
    public const string CreateRecipeStatusInitial = "CreateRecipeStatusInitial";
    public const string CreateRecipeStatusGenerating = "CreateRecipeStatusGenerating";
    public const string CreateRecipeStatusDraftReady = "CreateRecipeStatusDraftReady";
    public const string CreateRecipeStatusValidationFailed = "CreateRecipeStatusValidationFailed";
    public const string CreateRecipeStatusGenerationCancelled = "CreateRecipeStatusGenerationCancelled";
    public const string CreateRecipeStatusGenerationUnavailableWithCode =
        "CreateRecipeStatusGenerationUnavailableWithCode";
    public const string CreateRecipeStatusDraftStorageFailedWithCode =
        "CreateRecipeStatusDraftStorageFailedWithCode";

    // Dedicated presentation for the two store codes whose remedy is user-actionable (F8 milestone audit ②):
    // UnsupportedVersion names the store file to delete (relative position only, ADR-002 redaction) and
    // LineageCapacityExhausted names the way out (start a new lineage). Every other code stays on the generic key.
    public const string CreateRecipeStatusStoreUnsupportedVersionWithCode =
        "CreateRecipeStatusStoreUnsupportedVersionWithCode";
    public const string CreateRecipeStatusLineageCapacityWithCode =
        "CreateRecipeStatusLineageCapacityWithCode";
    public const string CreateRecipeStatusDraftConfirmed = "CreateRecipeStatusDraftConfirmed";
    public const string CreateRecipeStatusConfirmationFailedWithCode =
        "CreateRecipeStatusConfirmationFailedWithCode";
    public const string CreateValidationPassed = "CreateValidationPassed";

    public const string CreateSimpleModeHeading = "CreateSimpleModeHeading";
    public const string CreateCapabilityLine = "CreateCapabilityLine";
    public const string CreateScopeNotice = "CreateScopeNotice";
    public const string CreatePresetApplyAction = "CreatePresetApplyAction";
    public const string CreateRecipeStatusPresetApplied = "CreateRecipeStatusPresetApplied";
    public const string CreateSuggestionsHeading = "CreateSuggestionsHeading";
    public const string CreateSuggestionSentence1 = "CreateSuggestionSentence1";
    public const string CreateSuggestionSentence2 = "CreateSuggestionSentence2";
    public const string CreateSuggestionSentence3 = "CreateSuggestionSentence3";
    public const string CreateBuildHandoffHeading = "CreateBuildHandoffHeading";
    public const string CreateBuildHandoffNotice = "CreateBuildHandoffNotice";

    // Parameter panel (F8b3): static copy, per-row hints, warning rows and the hand-edit status lines.
    public const string CreateParameterPanelHeading = "CreateParameterPanelHeading";
    public const string CreateParameterPanelNotice = "CreateParameterPanelNotice";
    public const string CreateParameterApplyAction = "CreateParameterApplyAction";
    public const string CreateParameterReportWatermark = "CreateParameterReportWatermark";
    public const string CreateParameterModuleHeader = "CreateParameterModuleHeader";
    public const string CreateParameterBoundsHint = "CreateParameterBoundsHint";
    public const string CreateParameterCurrentValue = "CreateParameterCurrentValue";
    public const string CreateParameterMissingHint = "CreateParameterMissingHint";
    public const string CreateParameterWarningsHeading = "CreateParameterWarningsHeading";
    public const string CreateParameterWarningTemplateUnknown = "CreateParameterWarningTemplateUnknown";
    public const string CreateParameterWarningParameterUndeclared = "CreateParameterWarningParameterUndeclared";
    public const string CreateParameterWarningModuleUnaddressable = "CreateParameterWarningModuleUnaddressable";
    public const string CreateRecipeStatusHumanEditSaved = "CreateRecipeStatusHumanEditSaved";
    public const string CreateRecipeStatusParameterEditRejected = "CreateRecipeStatusParameterEditRejected";
    public const string CreateRecipeStatusValidationFailedNotRetainedWithCode =
        "CreateRecipeStatusValidationFailedNotRetainedWithCode";
    public const string CreateRetentionNoticeSuperseded = "CreateRetentionNoticeSuperseded";
    public const string CreateRetentionNoticeTrimmed = "CreateRetentionNoticeTrimmed";
    public const string CreateRetentionNoticeEvicted = "CreateRetentionNoticeEvicted";
    public const string CreateValidationEditRefusedSeePanel = "CreateValidationEditRefusedSeePanel";

    // Refinement input area (F8b4, professional mode): the feedback box, the explicit refine action, the round
    // status lines and the guard-restoration disclosure. Stable codes, counts, ordinals, parameter paths and value
    // literals are formatted as arguments, untranslated.
    public const string CreateRefineHeading = "CreateRefineHeading";
    public const string CreateRefineNotice = "CreateRefineNotice";
    public const string CreateRefineFeedbackWatermark = "CreateRefineFeedbackWatermark";
    public const string CreateRefineAction = "CreateRefineAction";
    public const string CreateRefineCancelAction = "CreateRefineCancelAction";
    public const string CreateRefineStatusIdle = "CreateRefineStatusIdle";
    public const string CreateRefineStatusNoHead = "CreateRefineStatusNoHead";
    public const string CreateRefineStatusEmptyFeedback = "CreateRefineStatusEmptyFeedback";
    public const string CreateRefineStatusLineageFull = "CreateRefineStatusLineageFull";
    public const string CreateRefineStatusRefining = "CreateRefineStatusRefining";
    public const string CreateRefineStatusCompleted = "CreateRefineStatusCompleted";
    public const string CreateRefineStatusValidationFailed = "CreateRefineStatusValidationFailed";
    public const string CreateRefineStatusChannelFailedWithCode = "CreateRefineStatusChannelFailedWithCode";
    public const string CreateRefineStatusNotConfiguredWithCode = "CreateRefineStatusNotConfiguredWithCode";
    public const string CreateRefineStatusCancelled = "CreateRefineStatusCancelled";
    public const string CreateRefineGuardHeading = "CreateRefineGuardHeading";
    public const string CreateRefineGuardLine = "CreateRefineGuardLine";

    // Session timeline (F8b4, professional mode): one entry per round, protocol literals as arguments.
    public const string CreateTimelineHeading = "CreateTimelineHeading";
    public const string CreateTimelineNotice = "CreateTimelineNotice";
    public const string CreateTimelineEntryDraftSaved = "CreateTimelineEntryDraftSaved";
    public const string CreateTimelineEntryGenerationFailed = "CreateTimelineEntryGenerationFailed";
    public const string CreateTimelineEntryGenerationUnavailable = "CreateTimelineEntryGenerationUnavailable";
    public const string CreateTimelineEntryPresetApplied = "CreateTimelineEntryPresetApplied";
    public const string CreateTimelineEntryHumanEditSaved = "CreateTimelineEntryHumanEditSaved";
    public const string CreateTimelineEntryRefined = "CreateTimelineEntryRefined";
    public const string CreateTimelineEntryRefineValidationFailed = "CreateTimelineEntryRefineValidationFailed";
    public const string CreateTimelineEntryRefineChannelFailed = "CreateTimelineEntryRefineChannelFailed";
    public const string CreateTimelineEntryReverted = "CreateTimelineEntryReverted";
    public const string CreateTimelineEntryConfirmed = "CreateTimelineEntryConfirmed";
    public const string CreateTimelineRetentionLine = "CreateTimelineRetentionLine";

    // Version-chain view and revert (F8b3b): the list rows, the inline two-step confirmation and the status lines.
    // Origin/status words, ordinals, stable codes and the UTC time literal are formatted as arguments, untranslated.
    public const string CreateLineageHeading = "CreateLineageHeading";
    public const string CreateLineageNotice = "CreateLineageNotice";
    public const string CreateLineageVersionLabel = "CreateLineageVersionLabel";
    public const string CreateLineageHeadMarker = "CreateLineageHeadMarker";
    public const string CreateLineageCreatedLine = "CreateLineageCreatedLine";
    public const string CreateLineageGuardLine = "CreateLineageGuardLine";
    public const string CreateLineageFeedbackLine = "CreateLineageFeedbackLine";
    public const string CreateLineageRevertAction = "CreateLineageRevertAction";
    public const string CreateLineageRevertConfirmPrompt = "CreateLineageRevertConfirmPrompt";
    public const string CreateLineageConfirmRevertAction = "CreateLineageConfirmRevertAction";
    public const string CreateLineageCancelRevertAction = "CreateLineageCancelRevertAction";
    public const string CreateLineageListFailedWithCode = "CreateLineageListFailedWithCode";
    public const string CreateRecipeStatusRevertedToVersion = "CreateRecipeStatusRevertedToVersion";
    public const string CreateRecipeStatusRevertBlockedWithCode = "CreateRecipeStatusRevertBlockedWithCode";
    public const string CreateRecipeStatusRevertFailedWithCode = "CreateRecipeStatusRevertFailedWithCode";

    // Bilingual copy for the closed editor rejection-code set (F8b3). Each sentence formats the issue path as
    // {0}, the offending text as {1} and the allowed range as {2}; RecipeParameterEditCopy pins the mapping.
    public const string RecipeParameterEditNoChanges = "RecipeParameterEditNoChanges";
    public const string RecipeParameterEditTargetNotFound = "RecipeParameterEditTargetNotFound";
    public const string RecipeParameterEditValueNotInteger = "RecipeParameterEditValueNotInteger";
    public const string RecipeParameterEditValueNotFinite = "RecipeParameterEditValueNotFinite";
    public const string RecipeParameterEditValueOutOfRange = "RecipeParameterEditValueOutOfRange";
    public const string RecipeParameterEditDocumentNotEditable = "RecipeParameterEditDocumentNotEditable";
    public const string RecipeParameterEditDuplicateTarget = "RecipeParameterEditDuplicateTarget";

    public const string CreatePresetFireBoltTitle = "CreatePresetFireBoltTitle";
    public const string CreatePresetFireBoltDescription = "CreatePresetFireBoltDescription";
    public const string CreatePresetTrailingFireballTitle = "CreatePresetTrailingFireballTitle";
    public const string CreatePresetTrailingFireballDescription = "CreatePresetTrailingFireballDescription";
    public const string CreatePresetBurstingFireballTitle = "CreatePresetBurstingFireballTitle";
    public const string CreatePresetBurstingFireballDescription = "CreatePresetBurstingFireballDescription";
    public const string CreatePresetShockImpactTitle = "CreatePresetShockImpactTitle";
    public const string CreatePresetShockImpactDescription = "CreatePresetShockImpactDescription";
    public const string CreatePresetLaunchFlashTitle = "CreatePresetLaunchFlashTitle";
    public const string CreatePresetLaunchFlashDescription = "CreatePresetLaunchFlashDescription";
    public const string CreatePresetEmberStreakTitle = "CreatePresetEmberStreakTitle";
    public const string CreatePresetEmberStreakDescription = "CreatePresetEmberStreakDescription";

    // Bilingual copy for the closed L1.5 suggestion-key set (F8a1). Constant names and values spell the
    // RecipeSuggestionKeys values verbatim; RecipeSuggestionCopy pins the two sets against each other.
    public const string RecipeSuggestionChooseCatalogTemplate = "RecipeSuggestionChooseCatalogTemplate";
    public const string RecipeSuggestionMatchTemplateKind = "RecipeSuggestionMatchTemplateKind";
    public const string RecipeSuggestionAddMissingParameter = "RecipeSuggestionAddMissingParameter";
    public const string RecipeSuggestionRemoveUnknownParameter = "RecipeSuggestionRemoveUnknownParameter";
    public const string RecipeSuggestionClampParameterToRange = "RecipeSuggestionClampParameterToRange";
    public const string RecipeSuggestionUseParameterNumericType = "RecipeSuggestionUseParameterNumericType";
    public const string RecipeSuggestionAddMissingStageRoot = "RecipeSuggestionAddMissingStageRoot";
    public const string RecipeSuggestionReorderStageRoots = "RecipeSuggestionReorderStageRoots";
    public const string RecipeSuggestionReduceModuleCount = "RecipeSuggestionReduceModuleCount";
    public const string RecipeSuggestionRemoveAttachment = "RecipeSuggestionRemoveAttachment";
    public const string RecipeSuggestionUseBuildableArchetype = "RecipeSuggestionUseBuildableArchetype";
    public const string RecipeSuggestionUseBuildableDimension = "RecipeSuggestionUseBuildableDimension";
    public const string RecipeSuggestionRemoveUnknownField = "RecipeSuggestionRemoveUnknownField";
    public const string RecipeSuggestionAddRequiredField = "RecipeSuggestionAddRequiredField";
    public const string RecipeSuggestionUseDeclaredValueType = "RecipeSuggestionUseDeclaredValueType";
    public const string RecipeSuggestionUseAllowedEnumValue = "RecipeSuggestionUseAllowedEnumValue";
    public const string RecipeSuggestionReturnOneJsonObject = "RecipeSuggestionReturnOneJsonObject";

    public const string PreviewTitle = "PreviewTitle";
    public const string PreviewDescription = "PreviewDescription";
    public const string PreviewEmptyState = "PreviewEmptyState";

    public const string PreviewImageGenerationHeading = "PreviewImageGenerationHeading";
    public const string PreviewImageGenerationNotice = "PreviewImageGenerationNotice";
    public const string PreviewImagePromptWatermark = "PreviewImagePromptWatermark";
    public const string PreviewWidthLabel = "PreviewWidthLabel";
    public const string PreviewHeightLabel = "PreviewHeightLabel";
    public const string PreviewGenerateImageAction = "PreviewGenerateImageAction";

    public const string PreviewImageStatusNotConfigured = "PreviewImageStatusNotConfigured";
    public const string PreviewImageStatusReady = "PreviewImageStatusReady";
    public const string PreviewImageStatusUnavailableWithCode = "PreviewImageStatusUnavailableWithCode";
    public const string PreviewImageStatusCancelled = "PreviewImageStatusCancelled";
    public const string PreviewImageStatusUnavailable = "PreviewImageStatusUnavailable";

    public const string PatchTitle = "PatchTitle";
    public const string PatchDescription = "PatchDescription";
    public const string PatchEmptyState = "PatchEmptyState";

    public const string ReviewTitle = "ReviewTitle";
    public const string ReviewDescription = "ReviewDescription";
    public const string ReviewEmptyState = "ReviewEmptyState";

    public const string ReviewMachineStatus = "ReviewMachineStatus";
    public const string ReviewVisualStatus = "ReviewVisualStatus";
    public const string ReviewUserVerdictStatus = "ReviewUserVerdictStatus";
    public const string ReviewL3Status = "ReviewL3Status";
    public const string ReviewL4Status = "ReviewL4Status";
    public const string ReviewAuthorityNotice = "ReviewAuthorityNotice";

    public const string JobsTitle = "JobsTitle";
    public const string JobsDescription = "JobsDescription";
    public const string JobsEmptyState = "JobsEmptyState";

    public const string JobsRefreshAction = "JobsRefreshAction";
    public const string JobsCancelAction = "JobsCancelAction";
    public const string JobsConfirmCancelAction = "JobsConfirmCancelAction";
    public const string JobsKeepAction = "JobsKeepAction";
    public const string JobsResubmitAction = "JobsResubmitAction";
    public const string JobsTimelineHeading = "JobsTimelineHeading";
    public const string JobsItemLabel = "JobsItemLabel";
    public const string JobsQueuedAtLabel = "JobsQueuedAtLabel";
    public const string JobsStartedAtLabel = "JobsStartedAtLabel";
    public const string JobsFinishedAtLabel = "JobsFinishedAtLabel";

    public const string JobsQueueIdle = "JobsQueueIdle";
    public const string JobsQueueExecuting = "JobsQueueExecuting";
    public const string JobsQueueWaitingProjectLock = "JobsQueueWaitingProjectLock";
    public const string JobsStoreUnavailableWithCode = "JobsStoreUnavailableWithCode";
    public const string JobsCancelRejectedWithCode = "JobsCancelRejectedWithCode";
    public const string JobsResubmitRejectedWithCode = "JobsResubmitRejectedWithCode";
    public const string JobsTimelineUnavailableWithCode = "JobsTimelineUnavailableWithCode";
    public const string JobsDiagnosticRetryHint = "JobsDiagnosticRetryHint";
    public const string JobsNoArtifacts = "JobsNoArtifacts";
    public const string JobsArtifactsWithIds = "JobsArtifactsWithIds";
    public const string JobsBatchItemDetail = "JobsBatchItemDetail";
    public const string JobsBatchGroupIndividual = "JobsBatchGroupIndividual";
    public const string JobsBatchGroupBatch = "JobsBatchGroupBatch";

    public const string SettingsTitle = "SettingsTitle";
    public const string SettingsDescription = "SettingsDescription";
    public const string SettingsEmptyState = "SettingsEmptyState";

    public const string SettingsProviderProfilesHeading = "SettingsProviderProfilesHeading";
    public const string SettingsProviderProfilesNotice = "SettingsProviderProfilesNotice";
    public const string SettingsProfileIdentifierWatermark = "SettingsProfileIdentifierWatermark";
    public const string SettingsNewAction = "SettingsNewAction";
    public const string SettingsEditAction = "SettingsEditAction";
    public const string SettingsDeleteAndRevokeAction = "SettingsDeleteAndRevokeAction";
    public const string SettingsProfileEditorHeading = "SettingsProfileEditorHeading";
    public const string SettingsProfileIdentifierLabel = "SettingsProfileIdentifierLabel";
    public const string SettingsProfileNameLabel = "SettingsProfileNameLabel";
    public const string SettingsProfileOriginLabel = "SettingsProfileOriginLabel";
    public const string SettingsProfileProtocolLabel = "SettingsProfileProtocolLabel";
    public const string SettingsProfileTimeoutLabel = "SettingsProfileTimeoutLabel";
    public const string SettingsProfileEnabledLabel = "SettingsProfileEnabledLabel";
    public const string SettingsEndpointEditorNotice = "SettingsEndpointEditorNotice";
    public const string SettingsEndpointWatermark = "SettingsEndpointWatermark";
    public const string SettingsChatCapabilityLabel = "SettingsChatCapabilityLabel";
    public const string SettingsChatModelLabel = "SettingsChatModelLabel";
    public const string SettingsImageCapabilityLabel = "SettingsImageCapabilityLabel";
    public const string SettingsImageModelLabel = "SettingsImageModelLabel";
    public const string SettingsSecretLabel = "SettingsSecretLabel";
    public const string SettingsSecretWatermark = "SettingsSecretWatermark";
    public const string SettingsRevokeSecretAction = "SettingsRevokeSecretAction";
    public const string SettingsSaveProfileAction = "SettingsSaveProfileAction";
    public const string SettingsChannelBindingsHeading = "SettingsChannelBindingsHeading";
    public const string SettingsChannelBindingsNotice = "SettingsChannelBindingsNotice";
    public const string SettingsProfileIdWatermark = "SettingsProfileIdWatermark";
    public const string SettingsCapabilityIdWatermark = "SettingsCapabilityIdWatermark";
    public const string SettingsModelIdWatermark = "SettingsModelIdWatermark";
    public const string SettingsSaveChatBindingAction = "SettingsSaveChatBindingAction";
    public const string SettingsClearChatBindingAction = "SettingsClearChatBindingAction";
    public const string SettingsSaveImageBindingAction = "SettingsSaveImageBindingAction";
    public const string SettingsClearImageBindingAction = "SettingsClearImageBindingAction";
    public const string SettingsLanguageHeading = "SettingsLanguageHeading";
    public const string SettingsLanguageNotice = "SettingsLanguageNotice";
    public const string SettingsLanguageEnglishOption = "SettingsLanguageEnglishOption";
    public const string SettingsLanguageChineseSimplifiedOption = "SettingsLanguageChineseSimplifiedOption";

    // Generation-mode section (F8b4): two closed options, applied and persisted immediately like the language.
    public const string SettingsGenerationModeHeading = "SettingsGenerationModeHeading";
    public const string SettingsGenerationModeNotice = "SettingsGenerationModeNotice";
    public const string SettingsGenerationModeSimpleOption = "SettingsGenerationModeSimpleOption";
    public const string SettingsGenerationModeProfessionalOption = "SettingsGenerationModeProfessionalOption";

    public const string SettingsSecurityNotice = "SettingsSecurityNotice";
    public const string SettingsSecretConfigured = "SettingsSecretConfigured";
    public const string SettingsSecretNotConfigured = "SettingsSecretNotConfigured";
    public const string SettingsStatusNotLoaded = "SettingsStatusNotLoaded";
    public const string SettingsStatusNoProfile = "SettingsStatusNoProfile";
    public const string SettingsStatusLoaded = "SettingsStatusLoaded";
    public const string SettingsStatusUnavailableWithCode = "SettingsStatusUnavailableWithCode";
    public const string SettingsStatusUnavailable = "SettingsStatusUnavailable";
    public const string SettingsStatusEditingNew = "SettingsStatusEditingNew";
    public const string SettingsStatusEditingSelected = "SettingsStatusEditingSelected";
    public const string SettingsStatusProfileUnavailableWithCode = "SettingsStatusProfileUnavailableWithCode";
    public const string SettingsStatusProfileUnavailable = "SettingsStatusProfileUnavailable";
    public const string SettingsStatusProfileSaved = "SettingsStatusProfileSaved";
    public const string SettingsStatusProfileNotSavedWithCode = "SettingsStatusProfileNotSavedWithCode";
    public const string SettingsStatusProfileNotSaved = "SettingsStatusProfileNotSaved";
    public const string SettingsStatusProfileDeleted = "SettingsStatusProfileDeleted";
    public const string SettingsStatusProfileNotDeletedWithCode = "SettingsStatusProfileNotDeletedWithCode";
    public const string SettingsStatusProfileNotDeleted = "SettingsStatusProfileNotDeleted";
    public const string SettingsStatusSecretRevoked = "SettingsStatusSecretRevoked";
    public const string SettingsStatusSecretNotRevokedWithCode = "SettingsStatusSecretNotRevokedWithCode";
    public const string SettingsStatusSecretNotRevoked = "SettingsStatusSecretNotRevoked";
    public const string SettingsChatBindingLabel = "SettingsChatBindingLabel";
    public const string SettingsImageBindingLabel = "SettingsImageBindingLabel";
    public const string SettingsBindingSaved = "SettingsBindingSaved";
    public const string SettingsBindingNotSavedWithCode = "SettingsBindingNotSavedWithCode";
    public const string SettingsBindingNotSaved = "SettingsBindingNotSaved";
    public const string SettingsBindingCleared = "SettingsBindingCleared";
    public const string SettingsBindingNotClearedWithCode = "SettingsBindingNotClearedWithCode";
    public const string SettingsBindingNotCleared = "SettingsBindingNotCleared";
    public const string SettingsChannelUnavailableWithCode = "SettingsChannelUnavailableWithCode";
    public const string SettingsChannelUnavailable = "SettingsChannelUnavailable";
    public const string SettingsChannelStatusUnavailable = "SettingsChannelStatusUnavailable";

    public const string DialogSelectProjectTitle = "DialogSelectProjectTitle";
}

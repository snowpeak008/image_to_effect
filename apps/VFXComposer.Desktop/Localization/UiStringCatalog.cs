namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Bilingual UI string catalog embedded in code (no satellite assemblies, no new package). Stable diagnostic codes,
/// channel identifiers and protocol words stay untranslated on purpose: they are machine-readable carriers.
/// </summary>
public static class UiStringCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EnglishValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UiStringKeys.AppProductName] = "VFX Composer",

            [UiStringKeys.MainWindowReadOnlySessionNotice] =
                "Read-only ordinary-user session; commands and mutation are disabled",
            [UiStringKeys.MainWindowConnectAction] = "Connect",
            [UiStringKeys.MainWindowSelectProjectAction] = "Select project",
            [UiStringKeys.MainWindowReadAction] = "Read",
            [UiStringKeys.MainWindowRestartAction] = "Restart",
            [UiStringKeys.MainWindowConnectionConnected] = "Connected",
            [UiStringKeys.MainWindowConnectionDisconnected] = "Disconnected",
            [UiStringKeys.MainWindowProjectNone] = "No registered project",
            [UiStringKeys.MainWindowProjectRegistered] = "Registered project",
            [UiStringKeys.MainWindowProjectSelected] = "Selected project",
            [UiStringKeys.MainWindowReadNone] = "No read result",
            [UiStringKeys.MainWindowReadAccepted] = "Read {0} bytes",
            [UiStringKeys.MainWindowReadRejected] = "Read rejected: {0}",

            [UiStringKeys.DashboardTitle] = "Dashboard",
            [UiStringKeys.DashboardDescription] =
                "Project connection and independently sourced status domains.",
            [UiStringKeys.DashboardEmptyState] = "No registered project",
            [UiStringKeys.DashboardProjectConnectionHeading] = "Project connection",
            [UiStringKeys.DashboardStatusDomainsHeading] = "Independent status domains",
            [UiStringKeys.DashboardMachineStatus] = "Machine: Not evaluated",
            [UiStringKeys.DashboardVisualStatus] = "Visual: VISUAL_PENDING",
            [UiStringKeys.DashboardUserVerdictStatus] = "User verdict: Not signed",
            [UiStringKeys.DashboardL3Status] = "L3: Not granted",
            [UiStringKeys.DashboardL4Status] = "L4: Not granted",

            [UiStringKeys.LibraryTitle] = "Library",
            [UiStringKeys.LibraryDescription] =
                "Read-only Recipe, Manifest, Contract and Trace projections arrive in Phase 2.",
            [UiStringKeys.LibraryEmptyState] = "No registered project",

            [UiStringKeys.CreateTitle] = "Create",
            [UiStringKeys.CreateDescription] =
                "Local transient recipe drafts and an explicit ChatLlm prompt.",
            [UiStringKeys.CreateEmptyState] =
                "Drafts stay in memory and cannot write an external workspace.",

            [UiStringKeys.CreateRecipeDraftHeading] = "Recipe draft",
            [UiStringKeys.CreateRecipeNameWatermark] = "Recipe name",
            [UiStringKeys.CreateDraftNotesWatermark] = "Draft notes",
            [UiStringKeys.CreateGenerateRecipeHeading] = "Generate recipe",
            [UiStringKeys.CreateGenerateRecipeNotice] =
                "Describe the effect; one explicit click sends the generation request through the configured chat "
                + "binding. The draft below is not built until you confirm it.",
            [UiStringKeys.CreateEffectDescriptionWatermark] = "Describe the visual effect to generate",
            [UiStringKeys.CreateGenerateDraftAction] = "Generate recipe draft",
            [UiStringKeys.CreateCancelGenerationAction] = "Cancel",
            [UiStringKeys.CreateConfirmDraftAction] = "Confirm draft",
            [UiStringKeys.CreateValidationWatermark] = "Validation results appear here",
            [UiStringKeys.CreateDraftJsonWatermark] = "Draft recipe JSON appears here",
            [UiStringKeys.CreateChatHeading] = "Chat",
            [UiStringKeys.CreateChatNotice] =
                "This sends one explicit ChatLlm prompt through the configured binding.",
            [UiStringKeys.CreateChatPromptWatermark] = "Ask the selected chat provider",
            [UiStringKeys.CreateSendChatAction] = "Send chat prompt",
            [UiStringKeys.CreateChatResponseWatermark] = "Typed chat response appears here",

            [UiStringKeys.CreateChatStatusNotConfigured] = "Chat is not configured.",
            [UiStringKeys.CreateChatStatusCompleted] = "Chat completed.",
            [UiStringKeys.CreateChatStatusUnavailableWithCode] = "Chat unavailable: {0}.",
            [UiStringKeys.CreateChatStatusCancelled] = "Chat cancelled.",
            [UiStringKeys.CreateRecipeStatusInitial] = "Describe an effect, then generate a draft.",
            [UiStringKeys.CreateRecipeStatusGenerating] = "Generating recipe draft...",
            [UiStringKeys.CreateRecipeStatusDraftReady] =
                "Draft ready after {0} request(s) - confirm to queue it for build.",
            [UiStringKeys.CreateRecipeStatusValidationFailed] = "Validation failed after {0} request(s): {1}.",
            [UiStringKeys.CreateRecipeStatusGenerationCancelled] = "Generation cancelled.",
            [UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode] = "Generation unavailable: {0}.",
            [UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode] = "Draft storage failed: {0}.",
            [UiStringKeys.CreateRecipeStatusDraftConfirmed] = "Draft confirmed - awaiting build.",
            [UiStringKeys.CreateRecipeStatusConfirmationFailedWithCode] = "Confirmation failed: {0}.",
            [UiStringKeys.CreateValidationPassed] = "L1 validation passed.",

            [UiStringKeys.CreateSimpleModeHeading] = "Example cards",
            [UiStringKeys.CreateCapabilityLine] =
                "Catalog {4} (contract revision {5}): {0} templates, {1} parameters; buildable archetypes: {2}; "
                + "dimensions: {3}.",
            [UiStringKeys.CreateScopeNotice] =
                "Honest scope: only {0} {1} effects from the fire-family template catalog can currently be built.",
            [UiStringKeys.CreatePresetApplyAction] = "Use this example",
            [UiStringKeys.CreateRecipeStatusPresetApplied] =
                "Example draft ready - no AI request was made. Confirm it as-is, or describe changes and generate.",
            [UiStringKeys.CreateSuggestionsHeading] = "Ways to describe an effect",
            [UiStringKeys.CreateSuggestionSentence1] =
                "A small fire bolt with a short flame trail, fast and light.",
            [UiStringKeys.CreateSuggestionSentence2] =
                "A slow heavy fireball whose impact sends out a wide shockwave.",
            [UiStringKeys.CreateSuggestionSentence3] =
                "A bright launch flash, then a compact projectile scattering embers.",
            [UiStringKeys.CreateBuildHandoffHeading] = "After you confirm",
            [UiStringKeys.CreateBuildHandoffNotice] =
                "Building still runs from the command line: close the Unity editor first, put the confirmed recipe "
                + "JSON into a batch manifest, then run the command below from the repository root. The batch path "
                + "does not update this page's draft status.",

            [UiStringKeys.CreateParameterPanelHeading] = "Parameter panel",
            [UiStringKeys.CreateParameterPanelNotice] =
                "The current head draft's declared parameters, editable inside the catalog's inclusive [min, max] "
                + "bounds. Applying lands a new human_edit version awaiting confirmation; no AI request is made.",
            [UiStringKeys.CreateParameterApplyAction] = "Apply changes",
            [UiStringKeys.CreateParameterReportWatermark] = "Edit results appear here",
            [UiStringKeys.CreateParameterModuleHeader] = "Stage {0}, module {1}: {2} ({3})",
            [UiStringKeys.CreateParameterBoundsHint] = "{0} in {1}, default {2}",
            [UiStringKeys.CreateParameterCurrentValue] = "Current {0}",
            [UiStringKeys.CreateParameterMissingHint] = "Not set in the draft (declared by the template)",
            [UiStringKeys.CreateParameterWarningsHeading] = "Warnings (not editable)",
            [UiStringKeys.CreateParameterWarningTemplateUnknown] =
                "{0}: template {1} is not declared by the catalog; the module cannot be edited here.",
            [UiStringKeys.CreateParameterWarningParameterUndeclared] =
                "{0}: key {1} is not declared by the template and cannot be edited.",
            [UiStringKeys.CreateParameterWarningModuleUnaddressable] =
                "{0}: the module has no string id ({1}) and cannot be addressed.",
            [UiStringKeys.CreateRecipeStatusHumanEditSaved] =
                "Version v{0} saved (human_edit) - confirm to queue it for build.",
            [UiStringKeys.CreateRecipeStatusParameterEditRejected] =
                "Edit rejected: {0} issue(s); nothing was saved.",
            [UiStringKeys.CreateRecipeStatusValidationFailedNotRetainedWithCode] =
                "Validation failed after {0} request(s): {1}. The failed draft was not retained: {2}.",
            [UiStringKeys.CreateRetentionNoticeSuperseded] =
                "The earlier confirmation in this lineage is now Superseded; that version can no longer be built.",
            [UiStringKeys.CreateRetentionNoticeTrimmed] =
                "Retention: {0} older version(s) of this lineage were trimmed.",
            [UiStringKeys.CreateRetentionNoticeEvicted] =
                "Retention: {0} inactive lineage(s) ({1} version(s)) were evicted from the store.",
            [UiStringKeys.CreateValidationEditRefusedSeePanel] =
                "Edit refused; the verdict is in the parameter panel report. The head draft is unchanged.",

            [UiStringKeys.CreateLineageHeading] = "Version chain",
            [UiStringKeys.CreateLineageNotice] =
                "Every retained version of the current lineage, oldest first. Select an older version and revert to "
                + "make it the head again; the newer versions are deleted after one explicit confirmation. No AI "
                + "request is made.",
            [UiStringKeys.CreateLineageVersionLabel] = "Version v{0}",
            [UiStringKeys.CreateLineageHeadMarker] = "head (current)",
            [UiStringKeys.CreateLineageCreatedLine] = "Created {0} UTC",
            [UiStringKeys.CreateLineageGuardLine] = "Guard restorations: {0}",
            [UiStringKeys.CreateLineageFeedbackLine] = "Feedback: {0}",
            [UiStringKeys.CreateLineageRevertAction] = "Revert to this version",
            [UiStringKeys.CreateLineageRevertConfirmPrompt] =
                "This will discard {0} newer version(s) ({1}); the truncation cannot be undone.",
            [UiStringKeys.CreateLineageConfirmRevertAction] = "Confirm revert",
            [UiStringKeys.CreateLineageCancelRevertAction] = "Cancel",
            [UiStringKeys.CreateLineageListFailedWithCode] = "Version list unavailable: {0}.",
            [UiStringKeys.CreateRecipeStatusRevertedToVersion] = "Reverted to v{0}; {1} newer version(s) deleted.",
            [UiStringKeys.CreateRecipeStatusRevertBlockedWithCode] =
                "Revert refused: {0}. A newer version is confirmed or built and is an audit record; start a new "
                + "lineage instead.",
            [UiStringKeys.CreateRecipeStatusRevertFailedWithCode] = "Revert failed: {0}.",

            [UiStringKeys.RecipeParameterEditNoChanges] = "No parameter value was changed; nothing was saved.",
            [UiStringKeys.RecipeParameterEditTargetNotFound] = "{0}: no declared parameter at this location.",
            [UiStringKeys.RecipeParameterEditValueNotInteger] = "{0}: '{1}' is not an integer; expected {2}.",
            [UiStringKeys.RecipeParameterEditValueNotFinite] = "{0}: '{1}' is not a finite number; expected {2}.",
            [UiStringKeys.RecipeParameterEditValueOutOfRange] =
                "{0}: {1} is outside the allowed range {2}; the value was not clamped.",
            [UiStringKeys.RecipeParameterEditDocumentNotEditable] =
                "The draft document cannot be parsed into editable stages.",
            [UiStringKeys.RecipeParameterEditDuplicateTarget] = "{0}: the same parameter is edited more than once.",

            [UiStringKeys.CreatePresetFireBoltTitle] = "Fire bolt",
            [UiStringKeys.CreatePresetFireBoltDescription] =
                "A single fiery core travelling in a straight line.",
            [UiStringKeys.CreatePresetTrailingFireballTitle] = "Trailing fireball",
            [UiStringKeys.CreatePresetTrailingFireballDescription] =
                "A fiery core with a motion trail following its flight.",
            [UiStringKeys.CreatePresetBurstingFireballTitle] = "Bursting fireball",
            [UiStringKeys.CreatePresetBurstingFireballDescription] =
                "A fiery core that ends in a burst of sparks on impact.",
            [UiStringKeys.CreatePresetShockImpactTitle] = "Shock impact",
            [UiStringKeys.CreatePresetShockImpactDescription] =
                "A fiery core whose impact sends out an expanding shockwave ring.",
            [UiStringKeys.CreatePresetLaunchFlashTitle] = "Launch flash",
            [UiStringKeys.CreatePresetLaunchFlashDescription] =
                "A bright launch flash followed by a fiery core.",
            [UiStringKeys.CreatePresetEmberStreakTitle] = "Ember streak",
            [UiStringKeys.CreatePresetEmberStreakDescription] =
                "A fiery core scattering embers along its flight.",

            [UiStringKeys.RecipeSuggestionChooseCatalogTemplate] =
                "Choose a templateId that the committed catalog declares.",
            [UiStringKeys.RecipeSuggestionMatchTemplateKind] =
                "Set the module kind to the kind its template declares.",
            [UiStringKeys.RecipeSuggestionAddMissingParameter] =
                "Add every parameter the template declares.",
            [UiStringKeys.RecipeSuggestionRemoveUnknownParameter] =
                "Remove parameters the template does not declare.",
            [UiStringKeys.RecipeSuggestionClampParameterToRange] =
                "Keep the value inside the inclusive [min, max] range.",
            [UiStringKeys.RecipeSuggestionUseParameterNumericType] =
                "Use the numeric type the template declares for this parameter.",
            [UiStringKeys.RecipeSuggestionAddMissingStageRoot] =
                "Add the missing stage root; launch, travel and impact are all required.",
            [UiStringKeys.RecipeSuggestionReorderStageRoots] =
                "Order the stage roots as launch, travel, impact.",
            [UiStringKeys.RecipeSuggestionReduceModuleCount] =
                "Reduce the module count to fit the strict build budget.",
            [UiStringKeys.RecipeSuggestionRemoveAttachment] =
                "Remove attachTo; module nesting is not allowed under the strict budget.",
            [UiStringKeys.RecipeSuggestionUseBuildableArchetype] =
                "Use an archetype the catalog can build.",
            [UiStringKeys.RecipeSuggestionUseBuildableDimension] =
                "Use a dimension the catalog can build.",
            [UiStringKeys.RecipeSuggestionRemoveUnknownField] = "Remove the unknown field.",
            [UiStringKeys.RecipeSuggestionAddRequiredField] = "Add the missing required field.",
            [UiStringKeys.RecipeSuggestionUseDeclaredValueType] =
                "Use the declared value type for this field.",
            [UiStringKeys.RecipeSuggestionUseAllowedEnumValue] =
                "Use one of the allowed enumeration values.",
            [UiStringKeys.RecipeSuggestionReturnOneJsonObject] = "Return exactly one JSON object.",

            [UiStringKeys.PreviewTitle] = "Preview",
            [UiStringKeys.PreviewDescription] =
                "Private image previews arrive only after an explicit ImageGeneration request.",
            [UiStringKeys.PreviewEmptyState] = "No private image preview is available",

            [UiStringKeys.PreviewImageGenerationHeading] = "Private image generation",
            [UiStringKeys.PreviewImageGenerationNotice] =
                "Generation is explicit. The returned provider artifact is decoded in memory for this preview only.",
            [UiStringKeys.PreviewImagePromptWatermark] = "Describe an image",
            [UiStringKeys.PreviewWidthLabel] = "Width",
            [UiStringKeys.PreviewHeightLabel] = "Height",
            [UiStringKeys.PreviewGenerateImageAction] = "Generate private image",

            [UiStringKeys.PreviewImageStatusNotConfigured] = "Image generation is not configured.",
            [UiStringKeys.PreviewImageStatusReady] = "Private image preview ready.",
            [UiStringKeys.PreviewImageStatusUnavailableWithCode] = "Image unavailable: {0}.",
            [UiStringKeys.PreviewImageStatusCancelled] = "Image generation cancelled.",
            [UiStringKeys.PreviewImageStatusUnavailable] = "Image unavailable.",

            [UiStringKeys.PatchTitle] = "Patch",
            [UiStringKeys.PatchDescription] =
                "Patch validation, diff and transactional apply arrive in Phase 3.",
            [UiStringKeys.PatchEmptyState] = "No patch is selected",

            [UiStringKeys.ReviewTitle] = "Review",
            [UiStringKeys.ReviewDescription] =
                "Evidence and authority remain separate, explicit and provenance-bound.",
            [UiStringKeys.ReviewEmptyState] = "No evidence is available",

            [UiStringKeys.ReviewMachineStatus] = "Machine: Not evaluated",
            [UiStringKeys.ReviewVisualStatus] = "Visual: VISUAL_PENDING",
            [UiStringKeys.ReviewUserVerdictStatus] = "User verdict: Not signed",
            [UiStringKeys.ReviewL3Status] = "L3: Not granted",
            [UiStringKeys.ReviewL4Status] = "L4: Not granted",
            [UiStringKeys.ReviewAuthorityNotice] =
                "Displayed state is not an authority grant. Visual verdicts and L3/L4 require their independent "
                + "issuers.",

            [UiStringKeys.JobsTitle] = "Jobs",
            [UiStringKeys.JobsDescription] =
                "Local serial job queue: strict FIFO, single global execution slot, durable across restarts.",
            [UiStringKeys.JobsEmptyState] = "No jobs are running",

            [UiStringKeys.JobsRefreshAction] = "Refresh",
            [UiStringKeys.JobsCancelAction] = "Cancel",
            [UiStringKeys.JobsConfirmCancelAction] = "Confirm cancel",
            [UiStringKeys.JobsKeepAction] = "Keep",
            [UiStringKeys.JobsResubmitAction] = "Re-enqueue",
            [UiStringKeys.JobsTimelineHeading] = "Timeline",
            [UiStringKeys.JobsItemLabel] = "item {0}",
            [UiStringKeys.JobsQueuedAtLabel] = "Queued {0}",
            [UiStringKeys.JobsStartedAtLabel] = "Started {0}",
            [UiStringKeys.JobsFinishedAtLabel] = "Finished {0}",

            [UiStringKeys.JobsQueueIdle] = "Queue idle.",
            [UiStringKeys.JobsQueueExecuting] = "Queue executing.",
            [UiStringKeys.JobsQueueWaitingProjectLock] =
                "Unity editor holds the project; the queue is waiting ({0}).",
            [UiStringKeys.JobsStoreUnavailableWithCode] = "Job store unavailable: {0}.",
            [UiStringKeys.JobsCancelRejectedWithCode] = "Cancel rejected: {0}.",
            [UiStringKeys.JobsResubmitRejectedWithCode] = "Re-enqueue rejected: {0}.",
            [UiStringKeys.JobsTimelineUnavailableWithCode] = "Timeline unavailable: {0}.",
            [UiStringKeys.JobsDiagnosticRetryHint] = "Retry is possible.",
            [UiStringKeys.JobsNoArtifacts] = "No artifacts",
            [UiStringKeys.JobsArtifactsWithIds] = "{0} artifact(s): {1}",
            [UiStringKeys.JobsBatchItemDetail] = "Batch item {0}",
            [UiStringKeys.JobsBatchGroupIndividual] = "Individual jobs · {0} job(s)",
            [UiStringKeys.JobsBatchGroupBatch] = "Batch {0} · {1} job(s)",

            [UiStringKeys.SettingsTitle] = "Settings",
            [UiStringKeys.SettingsDescription] =
                "Current-user provider profiles and explicit channel bindings.",
            [UiStringKeys.SettingsEmptyState] = "No provider profile is configured",

            [UiStringKeys.SettingsProviderProfilesHeading] = "Provider profiles",
            [UiStringKeys.SettingsProviderProfilesNotice] =
                "Profiles expose redacted endpoint summaries here. Select an identifier and choose Edit to "
                + "deliberately reveal its exact endpoint text.",
            [UiStringKeys.SettingsProfileIdentifierWatermark] = "Profile identifier",
            [UiStringKeys.SettingsNewAction] = "New",
            [UiStringKeys.SettingsEditAction] = "Edit",
            [UiStringKeys.SettingsDeleteAndRevokeAction] = "Delete + revoke",
            [UiStringKeys.SettingsProfileEditorHeading] = "Profile editor",
            [UiStringKeys.SettingsProfileIdentifierLabel] = "Identifier",
            [UiStringKeys.SettingsProfileNameLabel] = "Name",
            [UiStringKeys.SettingsProfileOriginLabel] = "Origin",
            [UiStringKeys.SettingsProfileProtocolLabel] = "Protocol",
            [UiStringKeys.SettingsProfileTimeoutLabel] = "Timeout",
            [UiStringKeys.SettingsProfileEnabledLabel] = "Enabled",
            [UiStringKeys.SettingsEndpointEditorNotice] =
                "Exact endpoint text is editable only in this focused editor; it is never copied into the summary "
                + "above.",
            [UiStringKeys.SettingsEndpointWatermark] =
                "Exact endpoint text (may include user-info, query, or fragment)",
            [UiStringKeys.SettingsChatCapabilityLabel] = "Chat capability",
            [UiStringKeys.SettingsChatModelLabel] = "Chat model",
            [UiStringKeys.SettingsImageCapabilityLabel] = "Image capability",
            [UiStringKeys.SettingsImageModelLabel] = "Image model",
            [UiStringKeys.SettingsSecretLabel] = "Secret",
            [UiStringKeys.SettingsSecretWatermark] = "Leave blank to preserve the existing secret",
            [UiStringKeys.SettingsRevokeSecretAction] = "Revoke secret",
            [UiStringKeys.SettingsSaveProfileAction] = "Save profile",
            [UiStringKeys.SettingsChannelBindingsHeading] = "Explicit channel bindings",
            [UiStringKeys.SettingsChannelBindingsNotice] =
                "Chat and Image each require their own profile, capability, and model. Saving one never changes or "
                + "falls back to the other.",
            [UiStringKeys.SettingsProfileIdWatermark] = "Profile ID",
            [UiStringKeys.SettingsCapabilityIdWatermark] = "Capability ID",
            [UiStringKeys.SettingsModelIdWatermark] = "Model ID",
            [UiStringKeys.SettingsSaveChatBindingAction] = "Save chat binding",
            [UiStringKeys.SettingsClearChatBindingAction] = "Clear chat binding",
            [UiStringKeys.SettingsSaveImageBindingAction] = "Save image binding",
            [UiStringKeys.SettingsClearImageBindingAction] = "Clear image binding",
            [UiStringKeys.SettingsLanguageHeading] = "Language",
            [UiStringKeys.SettingsLanguageNotice] =
                "The selection applies immediately and is stored for the current user only.",
            [UiStringKeys.SettingsLanguageEnglishOption] = "English",
            [UiStringKeys.SettingsLanguageChineseSimplifiedOption] = "简体中文",
            [UiStringKeys.SettingsGenerationModeHeading] = "Generation mode",
            [UiStringKeys.SettingsGenerationModeNotice] =
                "The selection applies immediately and is stored for the current user. Professional mode adds the "
                + "parameter panel, refinement, version chain and timeline on the Create page; simple mode keeps "
                + "the example cards and the AI entry. Switching sends no request and changes no draft.",
            [UiStringKeys.SettingsGenerationModeSimpleOption] = "Simple mode",
            [UiStringKeys.SettingsGenerationModeProfessionalOption] = "Professional mode",

            [UiStringKeys.SettingsSecurityNotice] =
                "Secrets are entry-only. Revoke detaches the selected secret and leaves its route fail-closed until "
                + "deliberate replacement. Endpoint text is shown only while editing this profile; normal summaries "
                + "are redacted.",
            [UiStringKeys.SettingsSecretConfigured] = "Secret configured",
            [UiStringKeys.SettingsSecretNotConfigured] = "No secret configured",
            [UiStringKeys.SettingsStatusNotLoaded] = "Provider settings have not been loaded.",
            [UiStringKeys.SettingsStatusNoProfile] = "No provider profile is configured.",
            [UiStringKeys.SettingsStatusLoaded] = "Provider settings loaded.",
            [UiStringKeys.SettingsStatusUnavailableWithCode] = "Provider settings unavailable: {0}.",
            [UiStringKeys.SettingsStatusUnavailable] = "Provider settings unavailable.",
            [UiStringKeys.SettingsStatusEditingNew] = "Editing a new provider profile.",
            [UiStringKeys.SettingsStatusEditingSelected] = "Editing selected provider profile.",
            [UiStringKeys.SettingsStatusProfileUnavailableWithCode] = "Profile unavailable: {0}.",
            [UiStringKeys.SettingsStatusProfileUnavailable] = "Profile unavailable.",
            [UiStringKeys.SettingsStatusProfileSaved] = "Provider profile saved.",
            [UiStringKeys.SettingsStatusProfileNotSavedWithCode] = "Provider profile not saved: {0}.",
            [UiStringKeys.SettingsStatusProfileNotSaved] = "Provider profile not saved.",
            [UiStringKeys.SettingsStatusProfileDeleted] = "Provider profile deleted and its secret revoked.",
            [UiStringKeys.SettingsStatusProfileNotDeletedWithCode] = "Provider profile not deleted: {0}.",
            [UiStringKeys.SettingsStatusProfileNotDeleted] = "Provider profile not deleted.",
            [UiStringKeys.SettingsStatusSecretRevoked] =
                "Secret detached. This profile is fail-closed until a new secret is saved.",
            [UiStringKeys.SettingsStatusSecretNotRevokedWithCode] = "Secret not revoked: {0}.",
            [UiStringKeys.SettingsStatusSecretNotRevoked] = "Secret not revoked.",
            [UiStringKeys.SettingsChatBindingLabel] = "Chat binding",
            [UiStringKeys.SettingsImageBindingLabel] = "Image binding",
            [UiStringKeys.SettingsBindingSaved] = "{0} saved.",
            [UiStringKeys.SettingsBindingNotSavedWithCode] = "{0} not saved: {1}.",
            [UiStringKeys.SettingsBindingNotSaved] = "{0} not saved.",
            [UiStringKeys.SettingsBindingCleared] = "{0} cleared.",
            [UiStringKeys.SettingsBindingNotClearedWithCode] = "{0} not cleared: {1}.",
            [UiStringKeys.SettingsBindingNotCleared] = "{0} not cleared.",
            [UiStringKeys.SettingsChannelUnavailableWithCode] = "Unavailable: {0}.",
            [UiStringKeys.SettingsChannelUnavailable] = "Unavailable.",
            [UiStringKeys.SettingsChannelStatusUnavailable] = "Unavailable",

            [UiStringKeys.DialogSelectProjectTitle] = "Select a Unity project",
        };

    private static readonly IReadOnlyDictionary<string, string> ChineseSimplifiedValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UiStringKeys.AppProductName] = "VFX Composer",

            [UiStringKeys.MainWindowReadOnlySessionNotice] = "只读普通用户会话；命令与写操作均已禁用",
            [UiStringKeys.MainWindowConnectAction] = "连接",
            [UiStringKeys.MainWindowSelectProjectAction] = "选择项目",
            [UiStringKeys.MainWindowReadAction] = "读取",
            [UiStringKeys.MainWindowRestartAction] = "重启",
            [UiStringKeys.MainWindowConnectionConnected] = "已连接",
            [UiStringKeys.MainWindowConnectionDisconnected] = "未连接",
            [UiStringKeys.MainWindowProjectNone] = "无已注册项目",
            [UiStringKeys.MainWindowProjectRegistered] = "已注册项目",
            [UiStringKeys.MainWindowProjectSelected] = "已选择项目",
            [UiStringKeys.MainWindowReadNone] = "无读取结果",
            [UiStringKeys.MainWindowReadAccepted] = "已读取 {0} 字节",
            [UiStringKeys.MainWindowReadRejected] = "读取被拒绝：{0}",

            [UiStringKeys.DashboardTitle] = "总览",
            [UiStringKeys.DashboardDescription] = "项目连接，以及来源相互独立的状态域。",
            [UiStringKeys.DashboardEmptyState] = "无已注册项目",
            [UiStringKeys.DashboardProjectConnectionHeading] = "项目连接",
            [UiStringKeys.DashboardStatusDomainsHeading] = "独立状态域",
            [UiStringKeys.DashboardMachineStatus] = "机器裁定：未评估",
            [UiStringKeys.DashboardVisualStatus] = "视觉：VISUAL_PENDING",
            [UiStringKeys.DashboardUserVerdictStatus] = "用户裁定：未签署",
            [UiStringKeys.DashboardL3Status] = "L3：未授予",
            [UiStringKeys.DashboardL4Status] = "L4：未授予",

            [UiStringKeys.LibraryTitle] = "制品库",
            [UiStringKeys.LibraryDescription] = "Recipe、Manifest、Contract 与 Trace 的只读投影在第 2 阶段提供。",
            [UiStringKeys.LibraryEmptyState] = "无已注册项目",

            [UiStringKeys.CreateTitle] = "创建",
            [UiStringKeys.CreateDescription] = "本地临时 recipe 草稿与显式 ChatLlm 提示。",
            [UiStringKeys.CreateEmptyState] = "草稿仅驻留内存，无法写入外部工作区。",

            [UiStringKeys.CreateRecipeDraftHeading] = "Recipe 草稿",
            [UiStringKeys.CreateRecipeNameWatermark] = "Recipe 名称",
            [UiStringKeys.CreateDraftNotesWatermark] = "草稿备注",
            [UiStringKeys.CreateGenerateRecipeHeading] = "生成 recipe",
            [UiStringKeys.CreateGenerateRecipeNotice] =
                "描述效果；一次显式点击会经已配置的 chat 绑定发送生成请求。下方草稿在你确认之前不会被构建。",
            [UiStringKeys.CreateEffectDescriptionWatermark] = "描述要生成的视觉效果",
            [UiStringKeys.CreateGenerateDraftAction] = "生成 recipe 草稿",
            [UiStringKeys.CreateCancelGenerationAction] = "取消",
            [UiStringKeys.CreateConfirmDraftAction] = "确认草稿",
            [UiStringKeys.CreateValidationWatermark] = "校验结果显示在此",
            [UiStringKeys.CreateDraftJsonWatermark] = "草稿 recipe JSON 显示在此",
            [UiStringKeys.CreateChatHeading] = "对话",
            [UiStringKeys.CreateChatNotice] = "此处经已配置的绑定发送一条显式 ChatLlm 提示。",
            [UiStringKeys.CreateChatPromptWatermark] = "向选定的 chat Provider 提问",
            [UiStringKeys.CreateSendChatAction] = "发送 chat 提示",
            [UiStringKeys.CreateChatResponseWatermark] = "已解析的 chat 响应显示在此",

            [UiStringKeys.CreateChatStatusNotConfigured] = "Chat 尚未配置。",
            [UiStringKeys.CreateChatStatusCompleted] = "Chat 已完成。",
            [UiStringKeys.CreateChatStatusUnavailableWithCode] = "Chat 不可用：{0}。",
            [UiStringKeys.CreateChatStatusCancelled] = "Chat 已取消。",
            [UiStringKeys.CreateRecipeStatusInitial] = "先描述一个效果，再生成草稿。",
            [UiStringKeys.CreateRecipeStatusGenerating] = "正在生成 recipe 草稿……",
            [UiStringKeys.CreateRecipeStatusDraftReady] = "{0} 次请求后草稿已就绪——确认后加入构建队列。",
            [UiStringKeys.CreateRecipeStatusValidationFailed] = "{0} 次请求后校验失败：{1}。",
            [UiStringKeys.CreateRecipeStatusGenerationCancelled] = "生成已取消。",
            [UiStringKeys.CreateRecipeStatusGenerationUnavailableWithCode] = "生成不可用：{0}。",
            [UiStringKeys.CreateRecipeStatusDraftStorageFailedWithCode] = "草稿存储失败：{0}。",
            [UiStringKeys.CreateRecipeStatusDraftConfirmed] = "草稿已确认——等待构建。",
            [UiStringKeys.CreateRecipeStatusConfirmationFailedWithCode] = "确认失败：{0}。",
            [UiStringKeys.CreateValidationPassed] = "L1 校验通过。",

            [UiStringKeys.CreateSimpleModeHeading] = "示例卡",
            [UiStringKeys.CreateCapabilityLine] =
                "目录 {4}（契约修订 {5}）：{0} 个模板、{1} 个参数；可构建原型：{2}；维度：{3}。",
            [UiStringKeys.CreateScopeNotice] =
                "诚实边界：当前仅能构建火系模板目录内的 {0} {1} 特效。",
            [UiStringKeys.CreatePresetApplyAction] = "使用此示例",
            [UiStringKeys.CreateRecipeStatusPresetApplied] =
                "示例草稿已就绪——未发起任何 AI 请求。可直接确认，也可描述修改后重新生成。",
            [UiStringKeys.CreateSuggestionsHeading] = "可以这样描述效果",
            [UiStringKeys.CreateSuggestionSentence1] = "一枚小巧的火焰弹，带短促的火焰尾迹，轻快迅捷。",
            [UiStringKeys.CreateSuggestionSentence2] = "一颗缓慢沉重的火球，命中时激起宽大的冲击波。",
            [UiStringKeys.CreateSuggestionSentence3] = "先是一道明亮的发射闪光，随后是散落余烬的紧凑弹体。",
            [UiStringKeys.CreateBuildHandoffHeading] = "确认之后",
            [UiStringKeys.CreateBuildHandoffNotice] =
                "构建仍需在命令行完成：先关闭 Unity 编辑器，将已确认的 recipe JSON 放入批量清单，"
                + "再在仓库根目录运行下方命令。批量路径不会更新本页的草稿状态。",

            [UiStringKeys.CreateParameterPanelHeading] = "参数面板",
            [UiStringKeys.CreateParameterPanelNotice] =
                "当前 head 草稿已声明的参数，可在目录含界 [min, max] 区间内编辑。"
                + "应用后落一个等待确认的新 human_edit 版本；不发起任何 AI 请求。",
            [UiStringKeys.CreateParameterApplyAction] = "应用修改",
            [UiStringKeys.CreateParameterReportWatermark] = "手改结果显示在此",
            [UiStringKeys.CreateParameterModuleHeader] = "stage {0}，模块 {1}：{2}（{3}）",
            [UiStringKeys.CreateParameterBoundsHint] = "{0}，区间 {1}，默认 {2}",
            [UiStringKeys.CreateParameterCurrentValue] = "当前 {0}",
            [UiStringKeys.CreateParameterMissingHint] = "草稿中未设置（模板已声明）",
            [UiStringKeys.CreateParameterWarningsHeading] = "预警（不可编辑）",
            [UiStringKeys.CreateParameterWarningTemplateUnknown] =
                "{0}：模板 {1} 未在目录中声明；该模块无法在此编辑。",
            [UiStringKeys.CreateParameterWarningParameterUndeclared] =
                "{0}：键 {1} 未被模板声明，不可编辑。",
            [UiStringKeys.CreateParameterWarningModuleUnaddressable] =
                "{0}：模块缺少字符串 id（{1}），无法定位。",
            [UiStringKeys.CreateRecipeStatusHumanEditSaved] =
                "已落新版本 v{0}（human_edit）——确认后加入构建队列。",
            [UiStringKeys.CreateRecipeStatusParameterEditRejected] =
                "手改被拒绝：{0} 条问题；未保存任何内容。",
            [UiStringKeys.CreateRecipeStatusValidationFailedNotRetainedWithCode] =
                "{0} 次请求后校验失败：{1}。失败草稿未能保留：{2}。",
            [UiStringKeys.CreateRetentionNoticeSuperseded] =
                "本链此前的确认已失效（Superseded）；该版本不可再构建。",
            [UiStringKeys.CreateRetentionNoticeTrimmed] =
                "保留策略：本链 {0} 个较早版本已被裁剪。",
            [UiStringKeys.CreateRetentionNoticeEvicted] =
                "保留策略：{0} 条不活跃版本链（共 {1} 个版本）已被淘汰。",
            [UiStringKeys.CreateValidationEditRefusedSeePanel] =
                "手改被拒绝；结论见参数面板报告。head 草稿未变。",

            [UiStringKeys.CreateLineageHeading] = "版本链",
            [UiStringKeys.CreateLineageNotice] =
                "当前版本链保留的全部版本，最老在前。选中较早版本并回退可使其重新成为 head；"
                + "较新版本在一次显式确认后被删除。不发起任何 AI 请求。",
            [UiStringKeys.CreateLineageVersionLabel] = "版本 v{0}",
            [UiStringKeys.CreateLineageHeadMarker] = "head（当前）",
            [UiStringKeys.CreateLineageCreatedLine] = "创建于 {0}（UTC）",
            [UiStringKeys.CreateLineageGuardLine] = "守卫还原：{0} 项",
            [UiStringKeys.CreateLineageFeedbackLine] = "反馈：{0}",
            [UiStringKeys.CreateLineageRevertAction] = "回到此版本",
            [UiStringKeys.CreateLineageRevertConfirmPrompt] =
                "将丢弃 {0} 个较新版本（{1}），此操作不可撤销。",
            [UiStringKeys.CreateLineageConfirmRevertAction] = "确认回退",
            [UiStringKeys.CreateLineageCancelRevertAction] = "取消",
            [UiStringKeys.CreateLineageListFailedWithCode] = "版本列表不可用：{0}。",
            [UiStringKeys.CreateRecipeStatusRevertedToVersion] = "已回到 v{0}，删除 {1} 个版本。",
            [UiStringKeys.CreateRecipeStatusRevertBlockedWithCode] =
                "回退被拒绝：{0}。较新版本已确认或已构建，属审计记录不可删除；请另起新链。",
            [UiStringKeys.CreateRecipeStatusRevertFailedWithCode] = "回退失败：{0}。",

            [UiStringKeys.RecipeParameterEditNoChanges] = "没有参数值被修改；未保存任何内容。",
            [UiStringKeys.RecipeParameterEditTargetNotFound] = "{0}：此位置没有已声明的参数。",
            [UiStringKeys.RecipeParameterEditValueNotInteger] = "{0}：'{1}' 不是整数；期望 {2}。",
            [UiStringKeys.RecipeParameterEditValueNotFinite] = "{0}：'{1}' 不是有限实数；期望 {2}。",
            [UiStringKeys.RecipeParameterEditValueOutOfRange] =
                "{0}：{1} 超出允许区间 {2}；数值未被夹取。",
            [UiStringKeys.RecipeParameterEditDocumentNotEditable] =
                "草稿文档无法解析为可编辑的 stage。",
            [UiStringKeys.RecipeParameterEditDuplicateTarget] = "{0}：同一参数被重复编辑。",

            [UiStringKeys.CreatePresetFireBoltTitle] = "火焰弹",
            [UiStringKeys.CreatePresetFireBoltDescription] = "一枚沿直线飞行的火焰核心。",
            [UiStringKeys.CreatePresetTrailingFireballTitle] = "拖尾火球",
            [UiStringKeys.CreatePresetTrailingFireballDescription] = "火焰核心飞行时拖着一条运动尾迹。",
            [UiStringKeys.CreatePresetBurstingFireballTitle] = "爆裂火球",
            [UiStringKeys.CreatePresetBurstingFireballDescription] = "火焰核心在命中时迸发出火花四溅的爆裂。",
            [UiStringKeys.CreatePresetShockImpactTitle] = "冲击波命中",
            [UiStringKeys.CreatePresetShockImpactDescription] = "火焰核心命中时扩散出一圈冲击波环。",
            [UiStringKeys.CreatePresetLaunchFlashTitle] = "发射闪光",
            [UiStringKeys.CreatePresetLaunchFlashDescription] = "一道明亮的发射闪光，随后是火焰核心。",
            [UiStringKeys.CreatePresetEmberStreakTitle] = "余烬流光",
            [UiStringKeys.CreatePresetEmberStreakDescription] = "火焰核心沿途散落点点余烬。",

            [UiStringKeys.RecipeSuggestionChooseCatalogTemplate] = "选择已入库目录声明的 templateId。",
            [UiStringKeys.RecipeSuggestionMatchTemplateKind] = "将模块 kind 设为其模板声明的 kind。",
            [UiStringKeys.RecipeSuggestionAddMissingParameter] = "补齐模板声明的全部参数。",
            [UiStringKeys.RecipeSuggestionRemoveUnknownParameter] = "移除模板未声明的参数。",
            [UiStringKeys.RecipeSuggestionClampParameterToRange] = "将数值保持在含界的 [min, max] 区间内。",
            [UiStringKeys.RecipeSuggestionUseParameterNumericType] = "使用模板为该参数声明的数值类型。",
            [UiStringKeys.RecipeSuggestionAddMissingStageRoot] =
                "补上缺失的 stage 根；launch、travel、impact 三者都必须存在。",
            [UiStringKeys.RecipeSuggestionReorderStageRoots] =
                "将 stage 根按 launch、travel、impact 的顺序排列。",
            [UiStringKeys.RecipeSuggestionReduceModuleCount] = "减少模块数量以满足 strict 构建预算。",
            [UiStringKeys.RecipeSuggestionRemoveAttachment] =
                "移除 attachTo；strict 预算下不允许模块嵌套。",
            [UiStringKeys.RecipeSuggestionUseBuildableArchetype] = "使用目录可构建的原型。",
            [UiStringKeys.RecipeSuggestionUseBuildableDimension] = "使用目录可构建的维度。",
            [UiStringKeys.RecipeSuggestionRemoveUnknownField] = "移除未知字段。",
            [UiStringKeys.RecipeSuggestionAddRequiredField] = "补上缺失的必填字段。",
            [UiStringKeys.RecipeSuggestionUseDeclaredValueType] = "为该字段使用声明的值类型。",
            [UiStringKeys.RecipeSuggestionUseAllowedEnumValue] = "使用允许的枚举值之一。",
            [UiStringKeys.RecipeSuggestionReturnOneJsonObject] = "只返回一个 JSON 对象。",

            [UiStringKeys.PreviewTitle] = "预览",
            [UiStringKeys.PreviewDescription] = "私有图像预览仅在显式 ImageGeneration 请求之后出现。",
            [UiStringKeys.PreviewEmptyState] = "暂无私有图像预览",

            [UiStringKeys.PreviewImageGenerationHeading] = "私有图像生成",
            [UiStringKeys.PreviewImageGenerationNotice] = "生成是显式的。返回的 Provider 产物仅在内存中解码，只供本次预览使用。",
            [UiStringKeys.PreviewImagePromptWatermark] = "描述一张图像",
            [UiStringKeys.PreviewWidthLabel] = "宽度",
            [UiStringKeys.PreviewHeightLabel] = "高度",
            [UiStringKeys.PreviewGenerateImageAction] = "生成私有图像",

            [UiStringKeys.PreviewImageStatusNotConfigured] = "图像生成尚未配置。",
            [UiStringKeys.PreviewImageStatusReady] = "私有图像预览已就绪。",
            [UiStringKeys.PreviewImageStatusUnavailableWithCode] = "图像不可用：{0}。",
            [UiStringKeys.PreviewImageStatusCancelled] = "图像生成已取消。",
            [UiStringKeys.PreviewImageStatusUnavailable] = "图像不可用。",

            [UiStringKeys.PatchTitle] = "补丁",
            [UiStringKeys.PatchDescription] = "补丁校验、差异与事务化应用在第 3 阶段提供。",
            [UiStringKeys.PatchEmptyState] = "未选择补丁",

            [UiStringKeys.ReviewTitle] = "评审",
            [UiStringKeys.ReviewDescription] = "证据与授权保持分离、显式且绑定来源。",
            [UiStringKeys.ReviewEmptyState] = "暂无证据",

            [UiStringKeys.ReviewMachineStatus] = "机器裁定：未评估",
            [UiStringKeys.ReviewVisualStatus] = "视觉：VISUAL_PENDING",
            [UiStringKeys.ReviewUserVerdictStatus] = "用户裁定：未签署",
            [UiStringKeys.ReviewL3Status] = "L3：未授予",
            [UiStringKeys.ReviewL4Status] = "L4：未授予",
            [UiStringKeys.ReviewAuthorityNotice] = "所显示的状态不构成授权。视觉裁定与 L3/L4 各自需要其独立的签发方。",

            [UiStringKeys.JobsTitle] = "任务",
            [UiStringKeys.JobsDescription] = "本地串行任务队列：严格 FIFO、全局单一执行槽、跨重启持久。",
            [UiStringKeys.JobsEmptyState] = "没有正在运行的任务",

            [UiStringKeys.JobsRefreshAction] = "刷新",
            [UiStringKeys.JobsCancelAction] = "取消",
            [UiStringKeys.JobsConfirmCancelAction] = "确认取消",
            [UiStringKeys.JobsKeepAction] = "保留",
            [UiStringKeys.JobsResubmitAction] = "重新入队",
            [UiStringKeys.JobsTimelineHeading] = "时间线",
            [UiStringKeys.JobsItemLabel] = "条目 {0}",
            [UiStringKeys.JobsQueuedAtLabel] = "入队 {0}",
            [UiStringKeys.JobsStartedAtLabel] = "开始 {0}",
            [UiStringKeys.JobsFinishedAtLabel] = "结束 {0}",

            [UiStringKeys.JobsQueueIdle] = "队列空闲。",
            [UiStringKeys.JobsQueueExecuting] = "队列正在执行。",
            [UiStringKeys.JobsQueueWaitingProjectLock] = "Unity 编辑器正占用项目；队列正在等待（{0}）。",
            [UiStringKeys.JobsStoreUnavailableWithCode] = "任务存储不可用：{0}。",
            [UiStringKeys.JobsCancelRejectedWithCode] = "取消被拒绝：{0}。",
            [UiStringKeys.JobsResubmitRejectedWithCode] = "重新入队被拒绝：{0}。",
            [UiStringKeys.JobsTimelineUnavailableWithCode] = "时间线不可用：{0}。",
            [UiStringKeys.JobsDiagnosticRetryHint] = "可以重试。",
            [UiStringKeys.JobsNoArtifacts] = "暂无产物",
            [UiStringKeys.JobsArtifactsWithIds] = "{0} 个产物：{1}",
            [UiStringKeys.JobsBatchItemDetail] = "批次条目 {0}",
            [UiStringKeys.JobsBatchGroupIndividual] = "独立任务 · {0} 个任务",
            [UiStringKeys.JobsBatchGroupBatch] = "批次 {0} · {1} 个任务",

            [UiStringKeys.SettingsTitle] = "设置",
            [UiStringKeys.SettingsDescription] = "当前用户的 Provider 配置档与显式通道绑定。",
            [UiStringKeys.SettingsEmptyState] = "未配置 Provider 配置档",

            [UiStringKeys.SettingsProviderProfilesHeading] = "Provider 配置档",
            [UiStringKeys.SettingsProviderProfilesNotice] =
                "此处配置档只展示脱敏后的 endpoint 摘要。选中标识符并点击“编辑”，才会显式显示其精确 endpoint 文本。",
            [UiStringKeys.SettingsProfileIdentifierWatermark] = "配置档标识符",
            [UiStringKeys.SettingsNewAction] = "新建",
            [UiStringKeys.SettingsEditAction] = "编辑",
            [UiStringKeys.SettingsDeleteAndRevokeAction] = "删除并吊销",
            [UiStringKeys.SettingsProfileEditorHeading] = "配置档编辑器",
            [UiStringKeys.SettingsProfileIdentifierLabel] = "标识符",
            [UiStringKeys.SettingsProfileNameLabel] = "名称",
            [UiStringKeys.SettingsProfileOriginLabel] = "来源",
            [UiStringKeys.SettingsProfileProtocolLabel] = "协议",
            [UiStringKeys.SettingsProfileTimeoutLabel] = "超时",
            [UiStringKeys.SettingsProfileEnabledLabel] = "启用",
            [UiStringKeys.SettingsEndpointEditorNotice] =
                "精确 endpoint 文本只能在此专用编辑器中编辑，绝不会复制进上方摘要。",
            [UiStringKeys.SettingsEndpointWatermark] = "精确 endpoint 文本（可含 user-info、query 或 fragment）",
            [UiStringKeys.SettingsChatCapabilityLabel] = "Chat 能力",
            [UiStringKeys.SettingsChatModelLabel] = "Chat 模型",
            [UiStringKeys.SettingsImageCapabilityLabel] = "Image 能力",
            [UiStringKeys.SettingsImageModelLabel] = "Image 模型",
            [UiStringKeys.SettingsSecretLabel] = "密钥",
            [UiStringKeys.SettingsSecretWatermark] = "留空则保留现有密钥",
            [UiStringKeys.SettingsRevokeSecretAction] = "吊销密钥",
            [UiStringKeys.SettingsSaveProfileAction] = "保存配置档",
            [UiStringKeys.SettingsChannelBindingsHeading] = "显式通道绑定",
            [UiStringKeys.SettingsChannelBindingsNotice] =
                "Chat 与 Image 各自需要独立的配置档、能力与模型。保存其中一个绝不会改动另一个，也不会回退到另一个。",
            [UiStringKeys.SettingsProfileIdWatermark] = "配置档 ID",
            [UiStringKeys.SettingsCapabilityIdWatermark] = "能力 ID",
            [UiStringKeys.SettingsModelIdWatermark] = "模型 ID",
            [UiStringKeys.SettingsSaveChatBindingAction] = "保存 Chat 绑定",
            [UiStringKeys.SettingsClearChatBindingAction] = "清除 Chat 绑定",
            [UiStringKeys.SettingsSaveImageBindingAction] = "保存 Image 绑定",
            [UiStringKeys.SettingsClearImageBindingAction] = "清除 Image 绑定",
            [UiStringKeys.SettingsLanguageHeading] = "语言",
            [UiStringKeys.SettingsLanguageNotice] = "选择后立即生效，并仅为当前用户保存。",
            [UiStringKeys.SettingsLanguageEnglishOption] = "English",
            [UiStringKeys.SettingsLanguageChineseSimplifiedOption] = "简体中文",
            [UiStringKeys.SettingsGenerationModeHeading] = "生成模式",
            [UiStringKeys.SettingsGenerationModeNotice] =
                "选择后立即生效，并仅为当前用户保存。专业模式会在 Create 页额外显示参数面板、精修、版本链与时间线；"
                + "简单模式保留示例卡与 AI 入口。切换不发起任何请求，也不改动任何草稿。",
            [UiStringKeys.SettingsGenerationModeSimpleOption] = "简单模式",
            [UiStringKeys.SettingsGenerationModeProfessionalOption] = "专业模式",

            [UiStringKeys.SettingsSecurityNotice] =
                "密钥仅可录入。吊销会分离选中的密钥，其路由在显式替换前保持 fail-closed。Endpoint 文本仅在编辑本配置档时"
                + "显示，常规摘要一律脱敏。",
            [UiStringKeys.SettingsSecretConfigured] = "已配置密钥",
            [UiStringKeys.SettingsSecretNotConfigured] = "未配置密钥",
            [UiStringKeys.SettingsStatusNotLoaded] = "Provider 设置尚未加载。",
            [UiStringKeys.SettingsStatusNoProfile] = "未配置 Provider 配置档。",
            [UiStringKeys.SettingsStatusLoaded] = "Provider 设置已加载。",
            [UiStringKeys.SettingsStatusUnavailableWithCode] = "Provider 设置不可用：{0}。",
            [UiStringKeys.SettingsStatusUnavailable] = "Provider 设置不可用。",
            [UiStringKeys.SettingsStatusEditingNew] = "正在编辑新的 Provider 配置档。",
            [UiStringKeys.SettingsStatusEditingSelected] = "正在编辑选中的 Provider 配置档。",
            [UiStringKeys.SettingsStatusProfileUnavailableWithCode] = "配置档不可用：{0}。",
            [UiStringKeys.SettingsStatusProfileUnavailable] = "配置档不可用。",
            [UiStringKeys.SettingsStatusProfileSaved] = "Provider 配置档已保存。",
            [UiStringKeys.SettingsStatusProfileNotSavedWithCode] = "Provider 配置档未保存：{0}。",
            [UiStringKeys.SettingsStatusProfileNotSaved] = "Provider 配置档未保存。",
            [UiStringKeys.SettingsStatusProfileDeleted] = "Provider 配置档已删除，其密钥已吊销。",
            [UiStringKeys.SettingsStatusProfileNotDeletedWithCode] = "Provider 配置档未删除：{0}。",
            [UiStringKeys.SettingsStatusProfileNotDeleted] = "Provider 配置档未删除。",
            [UiStringKeys.SettingsStatusSecretRevoked] = "密钥已分离。在保存新密钥前，本配置档保持 fail-closed。",
            [UiStringKeys.SettingsStatusSecretNotRevokedWithCode] = "密钥未吊销：{0}。",
            [UiStringKeys.SettingsStatusSecretNotRevoked] = "密钥未吊销。",
            [UiStringKeys.SettingsChatBindingLabel] = "Chat 绑定",
            [UiStringKeys.SettingsImageBindingLabel] = "Image 绑定",
            [UiStringKeys.SettingsBindingSaved] = "{0} 已保存。",
            [UiStringKeys.SettingsBindingNotSavedWithCode] = "{0} 未保存：{1}。",
            [UiStringKeys.SettingsBindingNotSaved] = "{0} 未保存。",
            [UiStringKeys.SettingsBindingCleared] = "{0} 已清除。",
            [UiStringKeys.SettingsBindingNotClearedWithCode] = "{0} 未清除：{1}。",
            [UiStringKeys.SettingsBindingNotCleared] = "{0} 未清除。",
            [UiStringKeys.SettingsChannelUnavailableWithCode] = "不可用：{0}。",
            [UiStringKeys.SettingsChannelUnavailable] = "不可用。",
            [UiStringKeys.SettingsChannelStatusUnavailable] = "不可用",

            [UiStringKeys.DialogSelectProjectTitle] = "选择 Unity 项目",
        };

    public static IReadOnlyList<UiLanguage> Languages { get; } =
        [UiLanguage.English, UiLanguage.ChineseSimplified];

    public static IReadOnlyDictionary<string, string> For(UiLanguage language) => language switch
    {
        UiLanguage.English => EnglishValues,
        UiLanguage.ChineseSimplified => ChineseSimplifiedValues,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>
    /// Resolves a catalog value. An unknown key is a programming error, not a runtime condition: the catalog is a
    /// closed set pinned by the parity test, so this throws instead of silently falling back to another language.
    /// </summary>
    public static string Resolve(UiLanguage language, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return For(language).TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown UI string key '{key}' for language {language}.");
    }
}

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

            [UiStringKeys.PreviewTitle] = "Preview",
            [UiStringKeys.PreviewDescription] =
                "Private image previews arrive only after an explicit ImageGeneration request.",
            [UiStringKeys.PreviewEmptyState] = "No private image preview is available",

            [UiStringKeys.PatchTitle] = "Patch",
            [UiStringKeys.PatchDescription] =
                "Patch validation, diff and transactional apply arrive in Phase 3.",
            [UiStringKeys.PatchEmptyState] = "No patch is selected",

            [UiStringKeys.ReviewTitle] = "Review",
            [UiStringKeys.ReviewDescription] =
                "Evidence and authority remain separate, explicit and provenance-bound.",
            [UiStringKeys.ReviewEmptyState] = "No evidence is available",

            [UiStringKeys.JobsTitle] = "Jobs",
            [UiStringKeys.JobsDescription] =
                "Local serial job queue: strict FIFO, single global execution slot, durable across restarts.",
            [UiStringKeys.JobsEmptyState] = "No jobs are running",

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
            [UiStringKeys.DashboardDescription] = "项目连接与各自独立取源的状态域。",
            [UiStringKeys.DashboardEmptyState] = "无已注册项目",
            [UiStringKeys.DashboardProjectConnectionHeading] = "项目连接",
            [UiStringKeys.DashboardStatusDomainsHeading] = "独立状态域",
            [UiStringKeys.DashboardMachineStatus] = "机器：未评估",
            [UiStringKeys.DashboardVisualStatus] = "视觉：VISUAL_PENDING",
            [UiStringKeys.DashboardUserVerdictStatus] = "用户裁定：未签署",
            [UiStringKeys.DashboardL3Status] = "L3：未授予",
            [UiStringKeys.DashboardL4Status] = "L4：未授予",

            [UiStringKeys.LibraryTitle] = "资源库",
            [UiStringKeys.LibraryDescription] = "Recipe、Manifest、Contract 与 Trace 的只读投影在第 2 阶段提供。",
            [UiStringKeys.LibraryEmptyState] = "无已注册项目",

            [UiStringKeys.CreateTitle] = "创建",
            [UiStringKeys.CreateDescription] = "本地临时 recipe 草稿与显式 ChatLlm 提示。",
            [UiStringKeys.CreateEmptyState] = "草稿仅驻留内存，无法写入外部工作区。",

            [UiStringKeys.PreviewTitle] = "预览",
            [UiStringKeys.PreviewDescription] = "私有图像预览仅在显式 ImageGeneration 请求之后出现。",
            [UiStringKeys.PreviewEmptyState] = "暂无私有图像预览",

            [UiStringKeys.PatchTitle] = "补丁",
            [UiStringKeys.PatchDescription] = "补丁校验、差异与事务化应用在第 3 阶段提供。",
            [UiStringKeys.PatchEmptyState] = "未选择补丁",

            [UiStringKeys.ReviewTitle] = "评审",
            [UiStringKeys.ReviewDescription] = "证据与授权保持分离、显式且绑定来源。",
            [UiStringKeys.ReviewEmptyState] = "暂无证据",

            [UiStringKeys.JobsTitle] = "任务",
            [UiStringKeys.JobsDescription] = "本地串行任务队列：严格 FIFO、全局单一执行槽、跨重启持久。",
            [UiStringKeys.JobsEmptyState] = "没有正在运行的任务",

            [UiStringKeys.SettingsTitle] = "设置",
            [UiStringKeys.SettingsDescription] = "当前用户的 provider 配置档与显式通道绑定。",
            [UiStringKeys.SettingsEmptyState] = "未配置 provider 配置档",

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

            [UiStringKeys.SettingsSecurityNotice] =
                "密钥仅可录入。吊销会分离选中的密钥，其路由在显式替换前保持 fail-closed。endpoint 文本仅在编辑本配置档时"
                + "显示，常规摘要一律脱敏。",
            [UiStringKeys.SettingsSecretConfigured] = "已配置密钥",
            [UiStringKeys.SettingsSecretNotConfigured] = "未配置密钥",
            [UiStringKeys.SettingsStatusNotLoaded] = "provider 设置尚未加载。",
            [UiStringKeys.SettingsStatusNoProfile] = "未配置 provider 配置档。",
            [UiStringKeys.SettingsStatusLoaded] = "provider 设置已加载。",
            [UiStringKeys.SettingsStatusUnavailableWithCode] = "provider 设置不可用：{0}。",
            [UiStringKeys.SettingsStatusUnavailable] = "provider 设置不可用。",
            [UiStringKeys.SettingsStatusEditingNew] = "正在编辑新的 provider 配置档。",
            [UiStringKeys.SettingsStatusEditingSelected] = "正在编辑选中的 provider 配置档。",
            [UiStringKeys.SettingsStatusProfileUnavailableWithCode] = "配置档不可用：{0}。",
            [UiStringKeys.SettingsStatusProfileUnavailable] = "配置档不可用。",
            [UiStringKeys.SettingsStatusProfileSaved] = "provider 配置档已保存。",
            [UiStringKeys.SettingsStatusProfileNotSavedWithCode] = "provider 配置档未保存：{0}。",
            [UiStringKeys.SettingsStatusProfileNotSaved] = "provider 配置档未保存。",
            [UiStringKeys.SettingsStatusProfileDeleted] = "provider 配置档已删除，其密钥已吊销。",
            [UiStringKeys.SettingsStatusProfileNotDeletedWithCode] = "provider 配置档未删除：{0}。",
            [UiStringKeys.SettingsStatusProfileNotDeleted] = "provider 配置档未删除。",
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

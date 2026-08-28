namespace VFXComposer.Desktop.ViewModels;

public sealed class CreateViewModel : WorkspacePageViewModel
{
    private string _recipeName = string.Empty;
    private string _draftNotes = string.Empty;

    public CreateViewModel()
        : base(
            "create",
            "Create",
            "Local transient Recipe draft. Validation and build commands arrive in Phase 3.",
            "Drafts are in memory only and cannot write a Unity project.")
    {
    }

    public string RecipeName
    {
        get => _recipeName;
        set => SetProperty(ref _recipeName, value ?? string.Empty);
    }

    public string DraftNotes
    {
        get => _draftNotes;
        set => SetProperty(ref _draftNotes, value ?? string.Empty);
    }
}

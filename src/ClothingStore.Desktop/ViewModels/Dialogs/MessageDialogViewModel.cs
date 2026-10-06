using ClothingStore.Core.Localization;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

public enum MessageKind
{
    Info,
    Warning,
    Error,
    Question,
}

/// <summary>Information, warning, error or yes/no question in the app's own style.</summary>
public sealed partial class MessageDialogViewModel(IDialogService dialogs, MessageKind kind, string title, string message, string? details = null)
    : DialogViewModelBase(dialogs)
{
    public override string Title { get; } = title;
    public MessageKind Kind { get; } = kind;
    public string Message { get; } = message;
    public string? Details { get; } = details;
    public bool IsQuestion => Kind == MessageKind.Question;

    public string Glyph => Kind switch
    {
        MessageKind.Warning => "",
        MessageKind.Error => "",
        MessageKind.Question => "",
        _ => "",
    };

    public string PrimaryText => IsQuestion ? Loc.T("Common.Yes") : Loc.T("Common.Ok");

    [RelayCommand]
    private void Accept() => Close(true);

    [RelayCommand]
    private void Decline() => Close(false);
}

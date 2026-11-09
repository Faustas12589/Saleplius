using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SalePlius.Client.Components;

/// <summary>
/// Wraps a card or row: while the mouse (or keyboard focus) is inside, the body map shows its muscles.
/// Renders as any element (Tag="li", Tag="a" …); other attributes such as class and href pass through.
/// </summary>
public class HoverZone : ComponentBase, IHandleEvent
{
    [Parameter] public string Tag { get; set; } = "div";
    [Parameter, EditorRequired] public MuscleHover Hover { get; set; } = default!;
    [Parameter] public IReadOnlyCollection<string> Primary { get; set; } = Array.Empty<string>();
    [Parameter] public IReadOnlyCollection<string> Secondary { get; set; } = Array.Empty<string>();
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, Tag);
        builder.AddMultipleAttributes(1, Attributes);
        builder.AddAttribute(2, "onmouseenter", EventCallback.Factory.Create<MouseEventArgs>(this, Show));
        builder.AddAttribute(3, "onmouseleave", EventCallback.Factory.Create<MouseEventArgs>(this, Hide));
        builder.AddAttribute(4, "onfocusin", EventCallback.Factory.Create<FocusEventArgs>(this, Show));
        builder.AddAttribute(5, "onfocusout", EventCallback.Factory.Create<FocusEventArgs>(this, Hide));
        builder.AddContent(6, ChildContent);
        builder.CloseElement();
    }

    private void Show() => Hover.Show(Primary, Secondary);

    private void Hide() => Hover.Clear();

    // Blazor normally re-renders after every event. Hovering only changes the body map (which listens
    // to MuscleHover itself), so skip re-rendering this zone and the page around it.
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
}

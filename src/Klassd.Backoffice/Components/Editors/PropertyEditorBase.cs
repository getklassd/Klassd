using Klassd.Core.Models;
using Microsoft.AspNetCore.Components;

namespace Klassd.Backoffice.Components.Editors;

/// <summary>
/// Base class for a property editor component. Custom property types point their
/// <c>EditorComponent</c> at a subclass of this; it receives the current string value,
/// the field metadata, and a change callback.
/// </summary>
public abstract class PropertyEditorBase : ComponentBase
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public PageFieldInfo Field { get; set; } = default!;

    protected Task SetValueAsync(string value) => ValueChanged.InvokeAsync(value);
}

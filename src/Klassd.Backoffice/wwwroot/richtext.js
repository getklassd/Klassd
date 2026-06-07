// Klassd rich text interop. Dependency-free vanilla ES module (no editor library, no build step).
// Served (as part of the RCL) at /_content/Klassd.Backoffice/richtext.js
//
// Uses a contenteditable surface + document.execCommand. execCommand is formally deprecated but
// is supported by every current browser and keeps the editor dependency- and build-free, which
// matches the rest of the backoffice. The .NET component owns the value; this module just pushes
// the surface's innerHTML back on every change.

// Makes `element` editable, seeds it with `initialHtml`, and reports edits to `dotnetRef`
// (invokable OnHtmlChanged(html)). Idempotent.
export function attach(element, dotnetRef, initialHtml) {
    if (!element || element._klassdRte) return;
    element.contentEditable = 'true';
    element.innerHTML = initialHtml || '';
    const onInput = () => { try { dotnetRef.invokeMethodAsync('OnHtmlChanged', element.innerHTML); } catch {} };
    element.addEventListener('input', onInput);
    element._klassdRte = { onInput };
}

// Runs a formatting command against the focused surface, then reports the result (execCommand
// does not always emit an 'input' event, so we push explicitly).
export function exec(element, command, value) {
    if (!element) return;
    element.focus();
    try { document.execCommand(command, false, value ?? undefined); } catch {}
    element._klassdRte?.onInput();
}

// Prompts for a URL and wraps the current selection in a link.
export function createLink(element) {
    const url = window.prompt('Link URL', 'https://');
    if (url) exec(element, 'createLink', url);
}

export function detach(element) {
    const h = element && element._klassdRte;
    if (!h) return;
    element.removeEventListener('input', h.onInput);
    delete element._klassdRte;
}

export default { attach, exec, createLink, detach };

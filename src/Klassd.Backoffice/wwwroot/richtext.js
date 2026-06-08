// Klassd rich text interop. Thin initializer over Quill (vendored — see quill.js / quill.snow.css
// / quill.LICENSE.txt in this folder), loaded globally via <script> in App.razor. No build step.
// Served (as part of the RCL) at /_content/Klassd.Backoffice/richtext.js
//
// The .NET component owns the value: Quill renders into a child element of `host` and we push the
// rendered HTML back on every change. `host` is an empty Blazor leaf, so its JS-created children
// are never touched by Blazor re-renders.

const TOOLBAR = [
    [{ header: [2, 3, false] }],
    ['bold', 'italic', 'underline'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['blockquote', 'link'],
    ['clean'],
];

export function attach(host, dotnetRef, initialHtml) {
    if (!host || host._klassdQuill) return;
    const Quill = window.Quill;
    if (!Quill) { console.error('Klassd: Quill is not loaded (expected quill.js <script> in App.razor)'); return; }

    const editor = document.createElement('div');
    host.appendChild(editor);

    const quill = new Quill(editor, { theme: 'snow', modules: { toolbar: TOOLBAR } });
    if (initialHtml) quill.clipboard.dangerouslyPasteHTML(initialHtml);

    quill.on('text-change', () => {
        // An empty editor reports "<p><br></p>"; normalize that to an empty string.
        const value = quill.getLength() <= 1 ? '' : quill.root.innerHTML;
        try { dotnetRef.invokeMethodAsync('OnHtmlChanged', value); } catch {}
    });

    host._klassdQuill = quill;
}

export function detach(host) {
    if (host) delete host._klassdQuill;
}

export default { attach, detach };

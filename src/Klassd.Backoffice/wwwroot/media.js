// Klassd media admin interop. Dependency-free vanilla ES module.
// Served (as part of the RCL) at /_content/Klassd.Backoffice/media.js

// Opens a file picker; for images, shows an interactive crop dialog (drag/resize a crop box,
// the result is optionally downscaled so its longest edge <= maxEdge), then POSTs the result
// as multipart/form-data to /api/media/{section}.
// Resolves to the created MediaRecord JSON, or null if the user cancelled (picker or crop).
// Throws on non-2xx.
export async function upload(section, maxEdge) {
    const file = await pickFile();
    if (!file) return null;

    let blob = file;
    if (file.type.startsWith('image/')) {
        const edited = await cropImage(file, maxEdge);
        if (edited === null) return null; // user cancelled the crop dialog -> cancel upload
        blob = edited;
    }
    return await postOne(section, blob, file.name);
}

// Picks MULTIPLE files and uploads them sequentially. Images are downscaled to maxEdge (no
// interactive crop — chaining N crop modals would be hostile). `progress` is an optional .NET
// object ref with an invokable OnProgress(done, total, fileName).
// Resolves to { records: MediaRecord[], errors: {fileName, message}[], cancelled: bool }.
export async function uploadMany(section, maxEdge, progress) {
    const files = await pickFile(true);
    if (!files.length) return { records: [], errors: [], cancelled: true };
    return await uploadFiles(section, maxEdge, files, progress);
}

// Uploads an explicit list of File objects sequentially (shared by the picker and drag-and-drop).
async function uploadFiles(section, maxEdge, files, progress) {
    const records = [], errors = [];
    const total = files.length;
    for (let i = 0; i < total; i++) {
        const file = files[i];
        try { await progress?.invokeMethodAsync('OnProgress', i, total, file.name); } catch {}
        try {
            let blob = file;
            if (file.type.startsWith('image/')) blob = await downscaleImage(file, maxEdge);
            records.push(await postOne(section, blob, file.name));
        } catch (e) {
            errors.push({ fileName: file.name, message: String(e?.message ?? e) });
        }
    }
    try { await progress?.invokeMethodAsync('OnProgress', total, total, null); } catch {}
    return { records, errors, cancelled: false };
}

// Wires native drag-and-drop file upload onto an element. The current section + max edge are read
// from the element's data-section / data-max-edge at drop time (so they track section changes).
// `handler` is a .NET object ref with invokables OnUploadStarted(), OnProgress(...), OnBulkUploaded(result).
// A `.drag-over` class is toggled on the element for styling (done here to avoid interop flicker).
export function enableDropZone(element, handler) {
    if (!element || element._klassdDrop) return;
    const onDragOver = e => {
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
        element.classList.add('drag-over');
    };
    const onDragLeave = e => { if (!element.contains(e.relatedTarget)) element.classList.remove('drag-over'); };
    const onDrop = async e => {
        e.preventDefault();
        element.classList.remove('drag-over');
        const files = e.dataTransfer ? Array.from(e.dataTransfer.files) : [];
        const section = element.dataset.section;
        if (!files.length || !section) return;
        const maxEdge = Number(element.dataset.maxEdge || 0);
        try { await handler.invokeMethodAsync('OnUploadStarted'); } catch {}
        const result = await uploadFiles(section, maxEdge, files, handler);
        try { await handler.invokeMethodAsync('OnBulkUploaded', result); } catch {}
    };
    element.addEventListener('dragover', onDragOver);
    element.addEventListener('dragenter', onDragOver);
    element.addEventListener('dragleave', onDragLeave);
    element.addEventListener('drop', onDrop);
    element._klassdDrop = { onDragOver, onDragLeave, onDrop };
}

export function disableDropZone(element) {
    const h = element && element._klassdDrop;
    if (!h) return;
    element.removeEventListener('dragover', h.onDragOver);
    element.removeEventListener('dragenter', h.onDragOver);
    element.removeEventListener('dragleave', h.onDragLeave);
    element.removeEventListener('drop', h.onDrop);
    delete element._klassdDrop;
}

// POSTs one blob as multipart/form-data; returns the created MediaRecord JSON, throws on non-2xx.
async function postOne(section, blob, fileName) {
    const formData = new FormData();
    formData.append('file', blob, fileName);
    const res = await fetch(`/api/media/${encodeURIComponent(section)}`, {
        method: 'POST', body: formData, credentials: 'include',
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(`Upload failed (${res.status}): ${text}`);
    }
    return await res.json();
}

// Non-interactive downscale so the longest edge <= maxEdge. Returns the original File when no
// resize is needed or on any failure (mirrors cropImage's untouched path).
function downscaleImage(file, maxEdge) {
    return new Promise(resolve => {
        if (!(maxEdge > 0)) { resolve(file); return; }
        const url = URL.createObjectURL(file);
        const img = new Image();
        img.onerror = () => { URL.revokeObjectURL(url); resolve(file); };
        img.onload = () => {
            const natW = img.naturalWidth, natH = img.naturalHeight;
            URL.revokeObjectURL(url);
            if (Math.max(natW, natH) <= maxEdge) { resolve(file); return; }
            const f = maxEdge / Math.max(natW, natH);
            const outW = Math.max(1, Math.round(natW * f));
            const outH = Math.max(1, Math.round(natH * f));
            try {
                const canvas = document.createElement('canvas');
                canvas.width = outW; canvas.height = outH;
                canvas.getContext('2d').drawImage(img, 0, 0, outW, outH);
                const type = (file.type === 'image/jpeg' || file.type === 'image/webp' || file.type === 'image/png')
                    ? file.type : 'image/png';
                const quality = (type === 'image/jpeg' || type === 'image/webp') ? 0.9 : undefined;
                canvas.toBlob(b => resolve(b || file), type, quality);
            } catch { resolve(file); }
        };
        img.src = url;
    });
}

// Returns { x, y } fractions (0..1) of where (clientX, clientY) falls inside imgElement's box.
export function clickFraction(imgElement, clientX, clientY) {
    const rect = imgElement.getBoundingClientRect();
    const x = rect.width > 0 ? (clientX - rect.left) / rect.width : 0;
    const y = rect.height > 0 ? (clientY - rect.top) / rect.height : 0;
    return { x: clamp01(x), y: clamp01(y) };
}

function clamp01(v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
function clamp(v, lo, hi) { return v < lo ? lo : v > hi ? hi : v; }

// multiple=false -> resolves a single File or null; multiple=true -> resolves a File[] (possibly empty).
function pickFile(multiple = false) {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.multiple = multiple;
        input.style.display = 'none';
        let settled = false;
        const done = v => { if (!settled) { settled = true; cleanup(); resolve(v); } };
        const cleanup = () => {
            window.removeEventListener('focus', onFocus, true);
            input.remove();
        };
        const empty = () => multiple ? [] : null;
        // Detect cancel: when the dialog closes the window regains focus; if no file
        // arrived shortly after, treat it as a cancellation.
        const onFocus = () => setTimeout(() => { if (!input.files || input.files.length === 0) done(empty()); }, 300);
        input.addEventListener('change', () => {
            const files = input.files ? Array.from(input.files) : [];
            done(multiple ? files : (files[0] ?? null));
        });
        window.addEventListener('focus', onFocus, true);
        document.body.appendChild(input);
        input.click();
    });
}

// Interactive crop dialog. Resolves to a cropped+downscaled Blob, or null if cancelled.
// The crop box starts covering the whole image; "Upload" applies whatever region is selected,
// downscaled so its longest edge is <= maxEdge (when maxEdge > 0). If the box still covers the
// whole image and no downscale is needed, the ORIGINAL file is returned untouched.
function cropImage(file, maxEdge) {
    return new Promise(resolve => {
        const url = URL.createObjectURL(file);
        const img = new Image();
        img.onerror = () => { URL.revokeObjectURL(url); resolve(file); };
        img.onload = () => {
            const natW = img.naturalWidth;
            const natH = img.naturalHeight;

            // Fit the image into the available dialog area, never upscaling beyond 1:1.
            const maxW = Math.min(window.innerWidth * 0.8, 1100);
            const maxH = window.innerHeight * 0.62;
            const fit = Math.min(maxW / natW, maxH / natH, 1);
            const dispW = Math.round(natW * fit);
            const dispH = Math.round(natH * fit);

            // ── DOM ──────────────────────────────────────────────────────
            const overlay = el('div', 'media-crop-overlay');
            const dialog = el('div', 'media-crop-dialog');
            const header = el('div', 'media-crop-header');
            header.innerHTML = `<strong>Crop image</strong><span class="media-crop-filename">${escapeHtml(file.name)}</span>`;

            const stage = el('div', 'media-crop-stage');
            stage.style.width = dispW + 'px';
            stage.style.height = dispH + 'px';
            const picture = el('img', 'media-crop-img');
            picture.src = url;
            picture.draggable = false;
            const box = el('div', 'media-crop-box');
            for (const h of ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w']) {
                const handle = el('span', 'media-crop-handle media-crop-handle-' + h);
                handle.dataset.handle = h;
                box.appendChild(handle);
            }
            stage.append(picture, box);

            const footer = el('div', 'media-crop-footer');
            const info = el('div', 'media-crop-info');
            const spacer = el('div', 'media-crop-spacer');
            const resetBtn = button('Reset', 'btn btn-ghost btn-sm');
            const cancelBtn = button('Cancel', 'btn btn-ghost btn-sm');
            const okBtn = button('Upload', 'btn btn-primary btn-sm');
            footer.append(info, spacer, resetBtn, cancelBtn, okBtn);

            dialog.append(header, stage, footer);
            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // ── Crop box state (in display pixels) ───────────────────────
            const MIN = 16;
            let crop = { x: 0, y: 0, w: dispW, h: dispH };
            const scale = natW / dispW; // display px -> natural px

            function render() {
                box.style.left = crop.x + 'px';
                box.style.top = crop.y + 'px';
                box.style.width = crop.w + 'px';
                box.style.height = crop.h + 'px';
                const nw = Math.round(crop.w * scale);
                const nh = Math.round(crop.h * scale);
                let outW = nw, outH = nh;
                if (maxEdge > 0 && Math.max(outW, outH) > maxEdge) {
                    const f = maxEdge / Math.max(outW, outH);
                    outW = Math.round(outW * f);
                    outH = Math.round(outH * f);
                }
                info.textContent = (outW === nw && outH === nh)
                    ? `${nw} × ${nh}px`
                    : `${nw} × ${nh}px → ${outW} × ${outH}px`;
            }
            render();

            // ── Drag / resize via Pointer Events ─────────────────────────
            let drag = null; // { mode:'move'|handle, startX, startY, orig }
            function onPointerDown(e) {
                const handle = e.target.dataset && e.target.dataset.handle;
                drag = {
                    mode: handle || 'move',
                    startX: e.clientX,
                    startY: e.clientY,
                    orig: { ...crop },
                };
                box.setPointerCapture?.(e.pointerId);
                e.preventDefault();
                e.stopPropagation();
            }
            function onPointerMove(e) {
                if (!drag) return;
                const dx = e.clientX - drag.startX;
                const dy = e.clientY - drag.startY;
                const o = drag.orig;
                if (drag.mode === 'move') {
                    crop.x = clamp(o.x + dx, 0, dispW - o.w);
                    crop.y = clamp(o.y + dy, 0, dispH - o.h);
                } else {
                    let left = o.x, top = o.y, right = o.x + o.w, bottom = o.y + o.h;
                    if (drag.mode.includes('w')) left = clamp(o.x + dx, 0, right - MIN);
                    if (drag.mode.includes('e')) right = clamp(o.x + o.w + dx, left + MIN, dispW);
                    if (drag.mode.includes('n')) top = clamp(o.y + dy, 0, bottom - MIN);
                    if (drag.mode.includes('s')) bottom = clamp(o.y + o.h + dy, top + MIN, dispH);
                    crop = { x: left, y: top, w: right - left, h: bottom - top };
                }
                render();
            }
            function onPointerUp() { drag = null; }

            box.addEventListener('pointerdown', onPointerDown);
            window.addEventListener('pointermove', onPointerMove);
            window.addEventListener('pointerup', onPointerUp);

            // ── Finish ───────────────────────────────────────────────────
            function cleanup() {
                window.removeEventListener('pointermove', onPointerMove);
                window.removeEventListener('pointerup', onPointerUp);
                window.removeEventListener('keydown', onKey);
                overlay.remove();
                URL.revokeObjectURL(url);
            }
            function onKey(e) {
                if (e.key === 'Escape') { cleanup(); resolve(null); }
                else if (e.key === 'Enter') confirm();
            }
            function confirm() {
                const sx = Math.round(crop.x * scale);
                const sy = Math.round(crop.y * scale);
                const sw = Math.max(1, Math.round(crop.w * scale));
                const sh = Math.max(1, Math.round(crop.h * scale));

                let outW = sw, outH = sh;
                if (maxEdge > 0 && Math.max(outW, outH) > maxEdge) {
                    const f = maxEdge / Math.max(outW, outH);
                    outW = Math.max(1, Math.round(outW * f));
                    outH = Math.max(1, Math.round(outH * f));
                }

                // Untouched (full image, no downscale) -> keep the original bytes/format.
                const fullImage = sx === 0 && sy === 0 && sw === natW && sh === natH;
                if (fullImage && outW === natW && outH === natH) { cleanup(); resolve(file); return; }

                try {
                    const canvas = document.createElement('canvas');
                    canvas.width = outW;
                    canvas.height = outH;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, sx, sy, sw, sh, 0, 0, outW, outH);
                    const type = (file.type === 'image/jpeg' || file.type === 'image/webp' || file.type === 'image/png')
                        ? file.type : 'image/png';
                    const quality = type === 'image/jpeg' || type === 'image/webp' ? 0.9 : undefined;
                    canvas.toBlob(b => { cleanup(); resolve(b || file); }, type, quality);
                } catch {
                    cleanup();
                    resolve(file); // canvas failed (e.g. tainted) -> upload original
                }
            }

            resetBtn.addEventListener('click', () => { crop = { x: 0, y: 0, w: dispW, h: dispH }; render(); });
            cancelBtn.addEventListener('click', () => { cleanup(); resolve(null); });
            okBtn.addEventListener('click', confirm);
            window.addEventListener('keydown', onKey);
        };
        img.src = url;
    });
}

function el(tag, className) {
    const e = document.createElement(tag);
    if (className) e.className = className;
    return e;
}
function button(text, className) {
    const b = el('button', className);
    b.type = 'button';
    b.textContent = text;
    return b;
}
function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

export default { upload, uploadMany, enableDropZone, disableDropZone, clickFraction };

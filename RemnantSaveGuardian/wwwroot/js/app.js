// Small helpers used by the Razor components.
window.rsg = {
    // Disables the browser's own context menu so right-click menus come from the app.
    suppressContextMenu: function () {
        document.addEventListener('contextmenu', function (e) {
            if (!(e.target instanceof HTMLInputElement) && !(e.target instanceof HTMLTextAreaElement)) {
                e.preventDefault();
            }
        });
    },

    // Scrolling text (replaces the WPF TextBlockPlus control): text in a ".rsg-marquee" element that
    // doesn't fit scrolls left while hovered (after 200ms, 250 px/s) and back when the mouse leaves
    // (1000 px/s). Holding the left button pauses it. Text that fits is left alone. One set of
    // document-level listeners covers every marquee, including ones rendered later.
    enableMarquee: function () {
        const rollingSpeed = 250, rollbackSpeed = 1000, delayMs = 200;
        const marqueeOf = el => el instanceof Element ? el.closest('.rsg-marquee') : null;
        const textOf = m => m.querySelector('.rsg-marquee-text');
        const currentX = t => new DOMMatrixReadOnly(getComputedStyle(t).transform).m41;

        const roll = m => {
            const t = textOf(m);
            if (!t) {
                return;
            }
            if (!m.classList.contains('rsg-marquee-active')) {
                // Measure while the text is still inline (with its ellipsis) inside the clipped box.
                const overflow = m.scrollWidth - m.clientWidth;
                if (overflow <= 1) {
                    return;
                }
                m.dataset.overflow = overflow;
                m.classList.add('rsg-marquee-active');
            }
            const overflow = Number(m.dataset.overflow);
            const remaining = overflow + currentX(t); // currentX is 0 or negative
            t.style.transition = `transform ${remaining / rollingSpeed}s linear ${delayMs}ms`;
            t.style.transform = `translateX(${-overflow}px)`;
        };

        const rollBack = m => {
            const t = textOf(m);
            if (!t || !m.classList.contains('rsg-marquee-active')) {
                return;
            }
            const distance = -currentX(t);
            t.style.transition = `transform ${distance / rollbackSpeed}s linear ${delayMs}ms`;
            t.style.transform = 'translateX(0px)';
        };

        document.addEventListener('mouseover', e => {
            const m = marqueeOf(e.target);
            if (m && !m.contains(e.relatedTarget)) {
                roll(m);
            }
        });
        document.addEventListener('mouseout', e => {
            const m = marqueeOf(e.target);
            if (m && !m.contains(e.relatedTarget)) {
                rollBack(m);
            }
        });
        document.addEventListener('mousedown', e => {
            const m = marqueeOf(e.target);
            const t = m && textOf(m);
            if (e.button === 0 && t && m.classList.contains('rsg-marquee-active')) {
                const x = currentX(t);
                t.style.transition = 'none';
                t.style.transform = `translateX(${x}px)`;
                m.dataset.paused = '1';
            }
        });
        document.addEventListener('mouseup', e => {
            document.querySelectorAll('.rsg-marquee[data-paused]').forEach(m => {
                delete m.dataset.paused;
                if (m.matches(':hover')) {
                    roll(m);
                } else {
                    rollBack(m);
                }
            });
        });
        document.addEventListener('transitionend', e => {
            const m = e.target instanceof Element && e.target.classList.contains('rsg-marquee-text') ? e.target.parentElement : null;
            if (m && currentX(e.target) === 0 && !m.matches(':hover')) {
                // Back at the start: restore the resting state (and its ellipsis).
                m.classList.remove('rsg-marquee-active');
                e.target.style.transition = '';
                e.target.style.transform = '';
            }
        });
    },

    // Lets files and folders dropped on an element with class "rsg-drop-zone" be imported as backups.
    // The dropped File objects are sent to the host with postMessageWithAdditionalObjects, which
    // exposes their real paths to .NET (MainWindow handles the "rsg-import-drop" message).
    // Drops anywhere else are ignored, so the web view never navigates to a dropped file.
    enableFileDrop: function () {
        let clearTimer = null;
        const isFileDrag = e => e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
        const zoneOf = e => e.target instanceof Element ? e.target.closest('.rsg-drop-zone') : null;
        const clear = () => document.querySelectorAll('.rsg-drag-over').forEach(z => z.classList.remove('rsg-drag-over'));

        document.addEventListener('dragover', function (e) {
            if (!isFileDrag(e)) {
                return;
            }
            e.preventDefault();
            const zone = zoneOf(e);
            e.dataTransfer.dropEffect = zone ? 'copy' : 'none';
            clear();
            if (zone) {
                zone.classList.add('rsg-drag-over');
            }
            // dragleave is unreliable across child elements; clear shortly after the last dragover.
            clearTimeout(clearTimer);
            clearTimer = setTimeout(clear, 150);
        });

        document.addEventListener('drop', function (e) {
            if (!isFileDrag(e)) {
                return;
            }
            e.preventDefault();
            clear();
            const zone = zoneOf(e);
            if (!zone || !e.dataTransfer.files.length || !window.chrome || !chrome.webview) {
                return;
            }
            chrome.webview.postMessageWithAdditionalObjects('rsg-import-drop', e.dataTransfer.files);
        });
    }
};

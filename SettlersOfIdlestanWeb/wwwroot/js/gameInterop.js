window.gameInterop = {
    _keyboardHandlers: {},
    _nextHandlerId: 0,

    registerKeyboardHandler: function (dotNetRef) {
        const id = ++this._nextHandlerId;
        const allowedLetters = new Set(['i', 'r', 'p', 's', 'c']);
        const allowedModifiers = new Set(['control', 'shift']);
        const allowedSpecial = new Set(['escape']);

        const keyDownHandler = (e) => {
            if (e.repeat) return;
            const lower = e.key.toLowerCase();
            if (allowedLetters.has(lower)) {
                dotNetRef.invokeMethodAsync('OnKeyDown', e.key.toUpperCase());
            } else if (allowedModifiers.has(lower)) {
                dotNetRef.invokeMethodAsync('OnKeyDown', e.key); // "Control" / "Shift" as-is
            } else if (allowedSpecial.has(lower)) {
                dotNetRef.invokeMethodAsync('OnKeyDown', e.key); // "Escape" as-is
            }
        };
        const keyUpHandler = (e) => {
            if (allowedModifiers.has(e.key.toLowerCase())) {
                dotNetRef.invokeMethodAsync('OnKeyUp', e.key);
            }
        };

        window.addEventListener('keydown', keyDownHandler);
        window.addEventListener('keyup', keyUpHandler);
        this._keyboardHandlers[id] = { down: keyDownHandler, up: keyUpHandler };
        return id;
    },

    unregisterKeyboardHandler: function (id) {
        const h = this._keyboardHandlers[id];
        if (h) {
            window.removeEventListener('keydown', h.down);
            window.removeEventListener('keyup', h.up);
            delete this._keyboardHandlers[id];
        }
    },

    getDevicePixelRatio: function () {
        return window.devicePixelRatio || 1;
    },

    getPhysicalPixelsPerCm: function () {
        const div = document.createElement('div');
        div.style.width = '1cm';
        div.style.position = 'absolute';
        div.style.visibility = 'hidden';
        document.body.appendChild(div);
        const cssPixelsPerCm = div.offsetWidth;
        document.body.removeChild(div);
        return cssPixelsPerCm * (window.devicePixelRatio || 1);
    },


    downloadFile: function (fileName, content) {
        const blob = new Blob([content], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    _visibilityHandler: null,
    _hiddenAt: null,

    registerVisibilityHandler: function (dotNetRef) {
        this._visibilityHandler = () => {
            if (document.visibilityState === 'hidden') {
                this._hiddenAt = Date.now();
            } else if (document.visibilityState === 'visible' && this._hiddenAt !== null) {
                const elapsed = (Date.now() - this._hiddenAt) / 1000;
                this._hiddenAt = null;
                if (elapsed > 0.5) {
                    dotNetRef.invokeMethodAsync('OnPageVisible', elapsed);
                }
            }
        };
        document.addEventListener('visibilitychange', this._visibilityHandler);
    },

    unregisterVisibilityHandler: function () {
        if (this._visibilityHandler) {
            document.removeEventListener('visibilitychange', this._visibilityHandler);
            this._visibilityHandler = null;
            this._hiddenAt = null;
        }
    },

    logError: function (message) {
        console.error('[SOI]', message);
    },

    _touchState: null,

    registerTouchHandler: function (dotNetRef) {
        let lastDist = null;
        let lastCx = 0, lastCy = 0;
        let isPinching = false;
        let lastX = 0, lastY = 0;

        const getDist = (t1, t2) => {
            const dx = t1.clientX - t2.clientX;
            const dy = t1.clientY - t2.clientY;
            return Math.sqrt(dx * dx + dy * dy);
        };

        const onTouchStart = (e) => {
            e.preventDefault();
            if (e.touches.length === 2) {
                if (!isPinching) dotNetRef.invokeMethodAsync('OnTouchEnd', lastX, lastY);
                isPinching = true;
                lastDist = getDist(e.touches[0], e.touches[1]);
                lastCx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
                lastCy = (e.touches[0].clientY + e.touches[1].clientY) / 2;
            } else if (e.touches.length === 1 && !isPinching) {
                lastX = e.touches[0].clientX;
                lastY = e.touches[0].clientY;
                dotNetRef.invokeMethodAsync('OnTouchStart', lastX, lastY);
            }
        };

        const onTouchMove = (e) => {
            e.preventDefault();
            if (e.touches.length === 2 && isPinching) {
                const d = getDist(e.touches[0], e.touches[1]);
                const cx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
                const cy = (e.touches[0].clientY + e.touches[1].clientY) / 2;
                if (lastDist !== null && lastDist > 0) {
                    const ratio = d / lastDist;
                    const panDx = cx - lastCx;
                    const panDy = cy - lastCy;
                    dotNetRef.invokeMethodAsync('OnPinch', ratio, cx, cy, panDx, panDy);
                }
                lastDist = d;
                lastCx = cx;
                lastCy = cy;
            } else if (e.touches.length === 1 && !isPinching) {
                lastX = e.touches[0].clientX;
                lastY = e.touches[0].clientY;
                dotNetRef.invokeMethodAsync('OnTouchMove', lastX, lastY);
            }
        };

        const onTouchEnd = (e) => {
            e.preventDefault();
            if (e.touches.length < 2 && isPinching) {
                isPinching = false;
                lastDist = null;
                if (e.touches.length === 1) {
                    lastX = e.touches[0].clientX;
                    lastY = e.touches[0].clientY;
                    dotNetRef.invokeMethodAsync('OnTouchStart', lastX, lastY);
                }
            }
            if (e.touches.length === 0) {
                if (e.changedTouches.length > 0) {
                    lastX = e.changedTouches[0].clientX;
                    lastY = e.changedTouches[0].clientY;
                }
                dotNetRef.invokeMethodAsync('OnTouchEnd', lastX, lastY);
            }
        };

        document.addEventListener('touchstart', onTouchStart, { passive: false });
        document.addEventListener('touchmove', onTouchMove, { passive: false });
        document.addEventListener('touchend', onTouchEnd, { passive: false });
        document.addEventListener('touchcancel', onTouchEnd, { passive: false });

        this._touchState = { start: onTouchStart, move: onTouchMove, end: onTouchEnd };
    },

    unregisterTouchHandler: function () {
        if (this._touchState) {
            document.removeEventListener('touchstart', this._touchState.start);
            document.removeEventListener('touchmove', this._touchState.move);
            document.removeEventListener('touchend', this._touchState.end);
            document.removeEventListener('touchcancel', this._touchState.end);
            this._touchState = null;
        }
    },

    openFilePicker: function () {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = '.json';
            input.style.display = 'none';
            document.body.appendChild(input);
            const cleanup = () => {
                if (document.body.contains(input))
                    document.body.removeChild(input);
            };
            input.onchange = async (e) => {
                const file = e.target.files[0];
                cleanup();
                if (file) {
                    try { resolve(await file.text()); }
                    catch { resolve(null); }
                } else {
                    resolve(null);
                }
            };
            input.oncancel = () => { cleanup(); resolve(null); };
            input.click();
        });
    }
};

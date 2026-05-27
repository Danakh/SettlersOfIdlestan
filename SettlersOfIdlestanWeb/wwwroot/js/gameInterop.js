window.gameInterop = {
    _keyboardHandlers: {},
    _nextHandlerId: 0,

    registerKeyboardHandler: function (dotNetRef) {
        const id = ++this._nextHandlerId;
        const allowed = new Set(['i', 'r', 'p', 's', 'c']);
        const handler = (e) => {
            if (allowed.has(e.key.toLowerCase())) {
                dotNetRef.invokeMethodAsync('OnKeyDown', e.key.toUpperCase());
            }
        };
        window.addEventListener('keydown', handler);
        this._keyboardHandlers[id] = handler;
        return id;
    },

    unregisterKeyboardHandler: function (id) {
        const h = this._keyboardHandlers[id];
        if (h) {
            window.removeEventListener('keydown', h);
            delete this._keyboardHandlers[id];
        }
    },

    getDevicePixelRatio: function () {
        return window.devicePixelRatio || 1;
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

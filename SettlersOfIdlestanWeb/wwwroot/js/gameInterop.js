window.gameInterop = {
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

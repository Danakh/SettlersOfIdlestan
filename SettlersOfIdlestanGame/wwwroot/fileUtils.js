// Helper to download a text file in the browser
window.downloadFile = (fileName, content) => {
    try {
        const blob = new Blob([content], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error('downloadFile error', e);
    }
};

// Trigger click on an element with the given id
window.clickElementById = (id) => {
    const el = document.getElementById(id);
    if (el) el.click();
};

// Local storage helpers for autosave
window.setLocalSave = (key, content) => {
    try {
        localStorage.setItem(key, content);
    } catch (e) {
        console.error('setLocalSave error', e);
    }
};

window.getLocalSave = (key) => {
    try {
        return localStorage.getItem(key);
    } catch (e) {
        console.error('getLocalSave error', e);
        return null;
    }
};

window.removeLocalSave = (key) => {
    try {
        localStorage.removeItem(key);
    } catch (e) {
        console.error('removeLocalSave error', e);
    }
};

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

// Test page helper: register/unregister click listener on an element and call back into .NET
window._testPageListeners = window._testPageListeners || {};
window.testPageRegister = (dotNetRef, selector) => {
    try {
        const el = document.querySelector(selector);
        if (!el) return;
        const handler = () => {
            // call instance method on the .NET object reference
            dotNetRef.invokeMethodAsync('NotifyClicked').catch(err => console.error('Invoke NotifyClicked failed', err));
        };
        window._testPageListeners[selector] = handler;
        el.addEventListener('click', handler);
    } catch (e) {
        console.error('testPageRegister error', e);
    }
};

window.testPageUnregister = (selector) => {
    try {
        const el = document.querySelector(selector);
        const handler = window._testPageListeners[selector];
        if (el && handler) {
            el.removeEventListener('click', handler);
            delete window._testPageListeners[selector];
        }
    } catch (e) {
        console.error('testPageUnregister error', e);
    }
};

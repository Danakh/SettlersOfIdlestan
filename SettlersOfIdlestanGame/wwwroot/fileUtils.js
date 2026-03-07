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

// Register hover handlers using event delegation so they survive Blazor re-renders
window.registerHoverSelector = (dotNetRef, selector, enterMethod, leaveMethod) => {
    try {
        if (!window._hoverListeners) window._hoverListeners = {};
        if (window._hoverListeners[selector]) {
            window.unregisterHoverSelector(selector);
        }

        let hovered = null;

        const overHandler = (e) => {
            const el = e.target.closest(selector);
            if (!el || el === hovered) return;
            hovered = el;
            const id = el.getAttribute('data-jsid');
            dotNetRef.invokeMethodAsync(enterMethod, id).catch(err => console.error('hover enter invoke failed', err));
        };

        const outHandler = (e) => {
            if (!hovered) return;
            const el = e.target.closest(selector);
            if (!el || el !== hovered) return;
            const related = e.relatedTarget;
            if (related && el.contains(related)) return;
            const id = el.getAttribute('data-jsid');
            hovered = null;
            dotNetRef.invokeMethodAsync(leaveMethod, id).catch(err => console.error('hover leave invoke failed', err));
        };

        document.addEventListener('mouseover', overHandler);
        document.addEventListener('mouseout', outHandler);
        window._hoverListeners[selector] = { over: overHandler, out: outHandler };
    } catch (e) {
        console.error('registerHoverSelector error', e);
    }
};

window.unregisterHoverSelector = (selector) => {
    try {
        if (!window._hoverListeners) return;
        const handlers = window._hoverListeners[selector];
        if (!handlers) return;
        document.removeEventListener('mouseover', handlers.over);
        document.removeEventListener('mouseout', handlers.out);
        delete window._hoverListeners[selector];
    } catch (e) {
        console.error('unregisterHoverSelector error', e);
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

// Register click handlers using event delegation so they survive Blazor re-renders
window.registerSelector = (dotNetRef, selector, methodName) => {
    try {
        if (!window._selectorListeners) window._selectorListeners = {};
        if (window._selectorListeners[selector]) {
            window.unregisterSelector(selector);
        }

        const handler = (e) => {
            const el = e.target.closest(selector);
            if (!el) return;
            const id = el.getAttribute('data-jsid');
            dotNetRef.invokeMethodAsync(methodName, id).catch(err => console.error('invokeMethodAsync failed', err));
        };

        document.addEventListener('click', handler);
        window._selectorListeners[selector] = handler;
    } catch (e) {
        console.error('registerSelector error', e);
    }
};

window.unregisterSelector = (selector) => {
    try {
        if (!window._selectorListeners) return;
        const handler = window._selectorListeners[selector];
        if (!handler) return;
        document.removeEventListener('click', handler);
        delete window._selectorListeners[selector];
    } catch (e) {
        console.error('unregisterSelector error', e);
    }
};

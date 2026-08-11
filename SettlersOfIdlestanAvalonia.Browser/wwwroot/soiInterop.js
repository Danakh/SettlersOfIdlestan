// Module d'interop du head navigateur, importe par BrowserInterop.InitializeAsync.
//
// Il ne couvre que ce qu'Avalonia.Browser n'expose pas : stockage, fichiers, plein ecran,
// visibilite de l'onglet. Le rendu, le pointeur, le clavier et le DPI sont geres par Avalonia.

// ── Stockage local ──────────────────────────────────────────────────────────────
// localStorage peut lever (mode navigation privee de Safari, quota depasse) : une exception
// ici remonterait dans le runtime .NET et interromprait la sauvegarde automatique.

export function getStorageItem(key) {
    try { return localStorage.getItem(key); }
    catch (e) { console.error('[SOI] lecture localStorage impossible', e); return null; }
}

export function setStorageItem(key, value) {
    try { localStorage.setItem(key, value); }
    catch (e) { console.error('[SOI] ecriture localStorage impossible', e); }
}

export function removeStorageItem(key) {
    try { localStorage.removeItem(key); }
    catch (e) { console.error('[SOI] suppression localStorage impossible', e); }
}

// ── Fichiers ────────────────────────────────────────────────────────────────────

// Enregistrement explicite (« Sauvegarder »). showSaveFilePicker laisse le joueur designer le
// fichier a ecrire, comme le selecteur natif du head bureau — il ecrase une sauvegarde existante
// au lieu d'accumuler savegame(1).json, savegame(2).json dans le dossier de telechargement.
//
// L'API n'existe que sur Chromium en contexte securise : ailleurs (Firefox, Safari, iframe
// bridee) on retombe sur le telechargement, qui reste le seul moyen de sortir la sauvegarde.
export function saveFilePicker(fileName, content) {
    if (typeof window.showSaveFilePicker !== 'function') {
        downloadFile(fileName, content);
        return Promise.resolve();
    }
    return writeThroughPicker(fileName, content);
}

async function writeThroughPicker(fileName, content) {
    let handle;
    try {
        handle = await window.showSaveFilePicker({
            suggestedName: fileName,
            types: [{
                description: 'Settlers of Idlestan',
                accept: { 'application/json': ['.json'] },
            }],
        });
    } catch (e) {
        // Boite fermee par le joueur : c'est un abandon, pas une erreur — on n'ecrit rien et on
        // ne declenche surtout pas le telechargement de repli, qu'il n'a pas demande.
        if (e && e.name === 'AbortError') return;
        console.error('[SOI] selecteur d\'enregistrement indisponible', e);
        downloadFile(fileName, content);
        return;
    }

    try {
        const stream = await handle.createWritable();
        await stream.write(content);
        await stream.close();
    } catch (e) {
        console.error('[SOI] ecriture de la sauvegarde impossible', e);
    }
}

export function downloadFile(fileName, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

export function openFilePicker() {
    return new Promise((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json';
        input.style.display = 'none';
        document.body.appendChild(input);
        const cleanup = () => {
            if (document.body.contains(input)) document.body.removeChild(input);
        };
        input.onchange = async (e) => {
            const file = e.target.files[0];
            cleanup();
            if (!file) { resolve(null); return; }
            try { resolve(await file.text()); }
            catch { resolve(null); }
        };
        // Annuler doit resoudre la promesse : laissee en suspens, elle bloquerait
        // definitivement le chargement cote jeu.
        input.oncancel = () => { cleanup(); resolve(null); };
        input.click();
    });
}

// ── Fenetre ─────────────────────────────────────────────────────────────────────

export function setFullscreen(fullscreen) {
    if (fullscreen) {
        const el = document.documentElement;
        const fn = el.requestFullscreen || el.webkitRequestFullscreen;
        if (fn) fn.call(el);
    } else {
        const fn = document.exitFullscreen || document.webkitExitFullscreen;
        if (fn) fn.call(document);
    }
}

export function openUrl(url) {
    window.open(url, '_blank', 'noopener');
}

export function closeWindow() {
    window.close();
}

export function logError(message) {
    console.error('[SOI]', message);
}

export function hasQueryFlag(name) {
    return new URLSearchParams(window.location.search).has(name);
}

// ── Evenements du navigateur ────────────────────────────────────────────────────

let hiddenAt = null;

export function registerVisibilityHandler(onVisible) {
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            hiddenAt = Date.now();
        } else if (document.visibilityState === 'visible' && hiddenAt !== null) {
            const elapsed = (Date.now() - hiddenAt) / 1000;
            hiddenAt = null;
            // Sous le demi-seconde, l'ecart vient d'un simple changement de focus :
            // le rattraper ferait sauter le jeu a chaque aller-retour.
            if (elapsed > 0.5) onVisible(elapsed);
        }
    });
}

export function registerFullscreenHandler(onChanged) {
    const notify = () => onChanged(!!(document.fullscreenElement || document.webkitFullscreenElement));
    document.addEventListener('fullscreenchange', notify);
    document.addEventListener('webkitfullscreenchange', notify);

    // Le F11 natif du navigateur ne declenche pas fullscreenchange : on le remplace par
    // l'API JS pour que le reglage du jeu reste aligne sur l'etat reel.
    document.addEventListener('keydown', (e) => {
        if (e.key !== 'F11') return;
        e.preventDefault();
        setFullscreen(!(document.fullscreenElement || document.webkitFullscreenElement));
    });
}

// ── Ecran de chargement ─────────────────────────────────────────────────────────

export function appReady() {
    document.getElementById('loading')?.remove();
}

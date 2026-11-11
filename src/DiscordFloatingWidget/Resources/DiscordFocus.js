// Experimental presentation only. Never read text, account state, credentials,
// network responses, or React internals. No messages/DOM are sent to native code.
(() => {
    if (window !== window.top || location.origin !== 'https://discord.com') return;
    if (window.__discordWidgetFocus) return;
    let enabled = true;
    let timer = 0;
    let marked = [];
    let observer;
    const style = document.createElement('style');
    style.id = 'discord-widget-focus-style';
    style.textContent = `
      [data-dw-hidden] { display: none !important; }
      [data-dw-path] { display: flex !important; flex-direction: column !important;
        flex: 1 1 auto !important; width: 100% !important; height: 100% !important;
        min-width: 0 !important; min-height: 0 !important; max-width: none !important;
        margin: 0 !important; padding: 0 !important; gap: 0 !important; }
      html[data-dw-focus], html[data-dw-focus] body { width: 100%; height: 100%; overflow: hidden; }
      [data-dw-chat] { flex: 1 1 auto !important; min-width: 0 !important; min-height: 0 !important;
        width: 100% !important; max-width: none !important; height: 100% !important;
        margin: 0 !important; border: 0 !important; background: #202126 !important; }
      [data-dw-chat] form { flex-shrink: 0 !important; margin: 0 8px 8px !important; padding: 0 !important; }
      [data-dw-chat] [class^="channelTextArea_"], [data-dw-chat] [class*=" channelTextArea_"] {
        min-width: 0 !important; margin-bottom: 0 !important; border-radius: 8px !important; }
      [data-dw-chat] form [class^="buttons_"], [data-dw-chat] form [class*=" buttons_"],
      [data-dw-chat] form [class^="attachWrapper_"], [data-dw-chat] form [class*=" attachWrapper_"],
      [data-dw-chat] [class^="channelAppLauncher_"], [data-dw-chat] [class*=" channelAppLauncher_"] { display: none !important; }
      [data-dw-chat] [role="textbox"][contenteditable="true"] { font-size: 13px !important; min-width: 0 !important; }
      [data-dw-chat] [data-list-id="chat-messages"] { min-width: 0 !important; }
    `;
    function clean() {
        for (const [node, attribute] of marked) node.removeAttribute(attribute);
        marked = [];
        document.documentElement?.removeAttribute('data-dw-focus');
    }
    function mark(node, attribute) { node.setAttribute(attribute, ''); marked.push([node, attribute]); }
    function isOverlay(node) {
        return node.matches('[role="dialog"], [role="alert"], [role="status"]') ||
            node.querySelector('[role="dialog"], [aria-modal="true"]') ||
            [...node.classList].some(name => /^(layerContainer|modal|tooltip|toast|popout)_/.test(name));
    }
    function refresh() {
        clearTimeout(timer); timer = 0;
        clean();
        if (!enabled || !/^\/channels\/@me\/\d+\/?$/.test(location.pathname)) return;
        const mount = document.getElementById('app-mount');
        const editors = document.querySelectorAll('form [role="textbox"][contenteditable="true"]');
        // Ambiguous layouts, login pages, unavailable DMs and unknown structures
        // are left intact. Keep Discord responsible for its own composer.
        if (!mount || editors.length !== 1) return;
        const editor = editors[0];
        const chat = editor.closest('main');
        if (!chat || !mount.contains(chat) || !chat.querySelector('[data-list-id="chat-messages"]')) return;
        if (!style.isConnected) document.head.append(style);
        let node = chat;
        while (node && node !== mount) {
            const parent = node.parentElement;
            if (!parent) { clean(); return; }
            mark(parent, 'data-dw-path');
            for (const sibling of parent.children) {
                if (sibling !== node && !isOverlay(sibling) && !['STYLE', 'SCRIPT', 'LINK'].includes(sibling.tagName))
                    mark(sibling, 'data-dw-hidden');
            }
            node = parent;
        }
        mark(chat, 'data-dw-chat');
        document.documentElement.setAttribute('data-dw-focus', '');
        const box = editor.getBoundingClientRect();
        // If changing layout obscures the typing box, undo all presentation changes.
        if (box.width < 100 || box.height < 16 || box.top < 0 || box.bottom > innerHeight + 1) clean();
    }
    function schedule() {
        if (enabled && !document.hidden && !timer) timer = setTimeout(refresh, 150);
    }
    function setEnabled(value) {
        enabled = value === true;
        observer?.disconnect();
        clearTimeout(timer); timer = 0;
        if (enabled) {
            observer ??= new MutationObserver(schedule);
            observer.observe(document.documentElement, {childList: true, subtree: true});
            refresh();
        } else { clean(); style.remove(); }
    }
    window.__discordWidgetFocus = {setEnabled, refresh};
    window.addEventListener('resize', schedule);
    window.addEventListener('popstate', schedule);
    document.addEventListener('visibilitychange', schedule);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => setEnabled(enabled), {once: true});
    else setEnabled(enabled);
})();

// Serializes tray popup visibility and leaves enough room for Windows' native
// context menus to open after a tray icon is right-clicked.
export function createMenuToggle(widget, onError = console.error) {
  let open = false, version = 0, completed = 0, running = false;
  let position, blurTimer, wasFocused = false, contextMenuUntil = 0;
  const cancelBlur = () => { clearTimeout(blurTimer); blurTimer = undefined; };

  async function drain() {
    if (running) return;
    running = true;
    try {
      while (completed !== version) {
        const current = version, show = open, placement = position;
        if (show) {
          if (placement?.desiredPosition) {
            await widget.adjustAndSetPosition(placement.desiredPosition.x, placement.desiredPosition.y, placement.alignX, placement.alignY);
            if (current !== version) continue;
          }
          await widget.show();
          if (current !== version) continue;
          await widget.focus();
        } else {
          await widget.hide();
        }
        completed = current;
      }
    } catch (error) {
      open = false; completed = version;
      onError(error);
      try { await widget.hide(); } catch (hideError) { onError(hideError); }
    } finally {
      running = false;
      if (completed !== version) void drain();
    }
  }

  function close() {
    cancelBlur();
    contextMenuUntil = 0;
    if (!open) return;
    open = false; version++; void drain();
  }

  return {
    toggle(payload) {
      cancelBlur(); contextMenuUntil = 0;
      position = payload; open = !open; version++; void drain();
    },
    close,
    preserveContextMenu() { contextMenuUntil = Date.now() + 10000; },
    focusChanged(focused) {
      if (focused) { contextMenuUntil = 0; wasFocused = true; cancelBlur(); return; }
      if (!wasFocused) return;
      wasFocused = false;
      if (!open) return;
      const current = version;
      // The tray icon action opens a separate native menu that temporarily
      // takes focus. Let it complete before treating focus loss as dismissal.
      const delay = Math.max(160, contextMenuUntil - Date.now());
      cancelBlur();
      blurTimer = setTimeout(() => {
        if (current !== version) return;
        if (Date.now() < contextMenuUntil) {
          blurTimer = setTimeout(() => { if (current === version) close(); }, contextMenuUntil - Date.now());
        } else close();
      }, delay);
    },
  };
}

# Desktop widgets

The first Clean11 desktop widgets are Clock, Calendar, To-do, and Notes. Open **Clean11 Settings → Widgets**, or **Top bar → Clean11 → Desktop widgets**. Both open the same Settings window, with the gallery embedded in its Widgets page.

## Arrange your desktop

- The gallery adds cards and shows or hides saved cards. Multiple cards of the same kind are supported.
- Enable **Edit layout**, then drag a card's header. Cards snap to an eight-pixel layout grid and nearby cards. Leaving the Widgets page or closing Settings ends layout editing.
- Use the size selector in Settings, right-click a card, or use its **⋯** menu to choose Small, Medium, or Large. Clock also supports 12/24-hour time.
- Removing a card from the desktop keeps its content. Use **Show** in the gallery to bring it back.
- **Show desktop widgets** switches the whole collection off without deleting it.

Calendar is a local month view with previous/next navigation and today's date highlighted. It does not connect to calendar accounts. To-do supports adding, completing, and deleting tasks. Notes are plain text. Neither has cloud sync.

## Shared material and desktop behavior

Cards and the gallery follow the shared Appearance palette, glass visibility, and blur radius. Desktop cards use Clean11's cached, wallpaper-based frosted material: applications cover them as ordinary desktop content. They are not always-on-top overlays and do not sample application contents behind them. Fullscreen applications hide cards on the affected display; a one-second timer updates this state.

Cards run in the separate **Clean11.Widgets.exe** host, started with the dock. The host exits when Clean11 exits, and checks the parent process as a fallback. Normal cards do not appear in the taskbar. The gallery is part of Clean11 Settings, a normal application window. Settings uses the same wallpaper-based glass material directly in its own window, without a companion glass window.

Only widget window handles receive positioning immediately above the desktop. Cards are not owned by Explorer/Progman: cross-process ownership implicitly couples input queues, including other windows on the widget thread. The gallery runs inside Settings on the dock process, separately from the desktop card process. Clean11 does not change other applications' ownership or stacking for this feature. Bottom-order message handling is adapted from DeskWidgets; its cross-process ownership technique is not used. Explorer restarts, Show Desktop, and mixed-DPI monitor changes still need broader validation.

Settings sends widget commands over a current-user-only local pipe. The host is the sole writer of widget data; Settings receives layout/status summaries without note or task text. Widget changes save automatically, while other Settings preferences retain their Apply changes workflow.

## Local storage

Content and layout live in `%LOCALAPPDATA%\Clean11\Widgets\widgets.json`. Edits save after a short debounce, and graceful exit flushes pending changes. Saving atomically replaces the previous file and retains a `.bak` copy. If the primary file cannot be read, it is preserved before recovery from the backup. If neither can be read, the host reports an error and preserves the files.

Notes and tasks are plain-text data on the local Windows account, not encrypted storage. Uninstall preserves this data. Do not attach the file to public bug reports if it contains personal information.

## Attribution

The bottom-order implementation selectively adapts **[DeskWidgets](https://github.com/VaticUI/DeskWidgets)** by VaticUI, under MIT. See [third-party notices](../THIRD-PARTY-NOTICES.md) and the [full license](../licenses/DESKWIDGETS-LICENSE.txt). The widget controls, gallery, persistence, and shared glass integration are implemented in C#/WPF for Clean11. No Python, Qt runtime, upstream artwork, or Seelen dependency is required by the widgets.

Both the source build and preview MSI include the widget executable and license notices. This source feature is newer than the published v0.2.0 Alpha release.

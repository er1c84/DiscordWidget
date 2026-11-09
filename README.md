# DiscordWidget-
A lightweight floating Discord chat widget for Windows built with C# and WPF.
An independent, unofficial Discord companion.

## Milestone 3: global show/hide shortcut

Press **Ctrl + Shift + D** from the widget or another desktop application:

- If the widget is visible, it hides and continues running.
- If it is hidden or minimized, the same window reappears and requests foreground
  focus. Its layout and previously focused control are retained where possible.
- Holding the keys down toggles once; release and press again for another toggle.
- While a modal warning dialog is open, the shortcut does not hide its owner.
- Closing the widget unregisters the shortcut and exits. Hiding does not exit.

If Windows cannot register the shortcut, a warning appears and the app continues
without it. Minimize, expand, close and dragging/resizing still work. Close the
other app reserving the shortcut and restart this widget to retry. As selected
for this milestone, the shortcut is fixed; there is no shortcut editor or automatic
fallback. A second instance may encounter the same conflict; single-instance
enforcement and tray integration are planned for Milestone 4.

**There is no tray icon yet.** Use the shortcut again to recover a hidden widget.
Before exiting, show it and click Close or press Alt+F4 while it is active. If an
unexpected problem prevents recovery, use Task Manager to end only this app.

### Manual shortcut verification

1. Launch one instance using the commands below. Focus an ordinary app, such as
   Notepad, then press Ctrl+Shift+D: the visible widget should hide. Press again:
   it should return in front of ordinary windows, with keyboard focus when Windows
   permits it. Confirm Task Manager still shows the same widget process.
2. Move and resize it, focus the expand button with Tab, and toggle twice. Its
   position and dimensions should stay the same, and keyboard focus should return
   to the previous control. Hold the shortcut for two seconds: it should toggle
   only once. Repeat several separate presses and check for one toggle per press.
3. Minimize through the header, focus another app, then press Ctrl+Shift+D. The
   widget should return in its normal size. Test hide/show while maximized too:
   a hidden maximized widget should return maximized.
4. Hide the widget, change monitor arrangement/scaling if your setup permits it,
   then show it. Its header must remain reachable. Recheck native dragging,
   edge/corner resizing and the compact layout after several toggles.
5. For a conflict check, leave the first instance visible and start a second
   instance from another terminal. The second should warn that Ctrl+Shift+D is
   unavailable and remain usable. Close it, show the first if necessary, and close
   the first. Restart once: the shortcut should register again, confirming release.
   During a warning dialog, the shortcut must not hide that dialog's owner.

Windows controls foreground activation; secure desktop prompts and some fullscreen
or elevated applications may restrict it. The app requests focus without changing
system security settings. Automated checks exercise actual Windows registration,
conflict/release and native message handling with an unused test combination, plus
WPF hide/show, minimized restoration, maximized visibility, logical focus and layout.
Physical key-repeat and cross-application focus behavior still need the manual steps.

## Milestone 2: floating window and persistent layout

Milestone 1 supplied the dark desktop shell and custom header. Milestone 2 adds:

- Always-on-top behavior above normal application windows.
- Native header dragging and edge/corner resizing through WPF `WindowChrome`.
- A minimum size of **180 × 120 WPF device-independent units (DIPs)**, with
  secondary text, decoration, and the footer hidden as space becomes limited.
  The compact view retains minimize, expand/restore, close, and an expand button.
- A default size of 360 × 500 DIPs; expand grows toward 720 × 700 DIPs, bounded
  by the current monitor's usable work area. Click the header expand button
  again to restore the previous size. That temporary toggle size is session-only.
- Saved normal position and dimensions, restored on the next launch.
- Recovery onto the nearest available monitor after disconnection, resolution,
  scaling, or work-area changes. Negative desktop coordinates are supported.

### Run in VS Code on Windows

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
and the Microsoft C# extension for VS Code. Open this repository folder and use
its integrated PowerShell terminal:

```powershell
dotnet --version
dotnet build .\DiscordFloatingWidget.slnx
dotnet test --solution .\DiscordFloatingWidget.slnx
dotnet run --project .\src\DiscordFloatingWidget\DiscordFloatingWidget.csproj
```

Development is verified with SDK **10.0.403**. `global.json` permits newer .NET 10
feature bands and selects Microsoft.Testing.Platform for the xUnit tests.
The first test build restores packages from NuGet. F5 builds and debugs the
widget; **Terminal > Run Task > run widget** is also available.

### Desktop shortcut on this computer

Build the Release app once, then create the shortcut from the repository's
PowerShell terminal:

```powershell
dotnet build .\src\DiscordFloatingWidget\DiscordFloatingWidget.csproj --configuration Release
.\scripts\Create-DesktopShortcut.ps1
```

Double-click **Discord Widget** on your desktop to launch the Release app without
opening VS Code or a terminal. The script finds your Windows desktop automatically,
including when it is redirected to OneDrive. It can refresh its own shortcut but
refuses to replace a shortcut pointing elsewhere. Preview with `-WhatIf`.

Keep the repository and its Release build files in place. If you move the
repository, remove the old desktop shortcut and rerun the script from the new
location. After code changes, close the widget and rebuild Release to update what
the shortcut launches. The normal .NET 10 Desktop Runtime is still required;
the .NET 10 SDK used for development includes it. This shortcut does not install
the application or enable automatic startup. A distributable installer that
creates shortcuts for other users is planned for a future packaging milestone.

### Manual verification on Windows

1. Launch the app. On first launch, expect a centered 360 × 500 DIP dark window
   with **Your chat, within reach.** Later launches should restore saved bounds.
2. Put an ordinary application in front of it: the widget should stay above it.
   Other topmost windows, exclusive fullscreen apps, and secure desktop prompts
   can still cover it.
3. Drag an empty part of the header. Resize using each of the four edges and four
   corners. Movement and resizing should remain smooth. Buttons should still
   work, and dragging should allow crossing monitor boundaries.
4. Resize to about 300 × 300, 240 × 200, and the 180 × 120 minimum. Secondary
   content should disappear, the title shortens to **DW**, and essential controls
   remain reachable. At small sizes, use **Expand widget**, then the header's
   **Restore compact size** button. Tab and Shift+Tab through controls and activate
   them with Space; check the visible focus outline.
5. Move and resize the widget, wait at least half a second, close it, and relaunch.
   Verify position and size. Repeat after minimizing and closing from the taskbar,
   and after maximizing by double-clicking the header. It should restart normally
   at the last normal bounds, never minimized or maximized.
6. Move to a secondary monitor, including one arranged left of or above the primary
   monitor. Close and relaunch there. Then disconnect that display and relaunch;
   the whole widget, including its header, should be inside a remaining work area.
   Also disconnect the display while the widget is open and while it is minimized,
   then restore from the taskbar.
7. Move between monitors at 100%, 125%, 150%, and 200% scaling where available.
   Change scale and resolution while it is open and between launches. The UI
   should scale clearly and keep its header reachable. Expand near the bottom or
   right edge: it should fit above/beside the taskbar. Test a taskbar on another
   edge if that configuration is available.
8. Close the app and back up `%LOCALAPPDATA%\DiscordFloatingWidget\settings.json`.
   Replace its contents with invalid JSON and relaunch. Expect a warning and safe
   default placement; the invalid file must remain intact and saving is disabled
   for that session. Close, then restore the backup or rename/remove the file.
9. Minimize restores through the taskbar. Close and Alt+F4 exit the process.
   The global shortcut now hides/shows the widget; tray integration comes later.

Real monitor disconnection and mixed-DPI changes require a multi-monitor setup.
Automated geometry tests do not replace that hardware checklist.

### Local settings and recovery

Only layout is stored in:

```text
%LOCALAPPDATA%\DiscordFloatingWidget\settings.json
```

Desktop positions are physical pixel coordinates; dimensions are DIPs with the
original DPI scale recorded for monitor selection. At startup the hidden native
window moves to the selected monitor before its current DPI is read. Placement
is clamped to that monitor's **work area**, excluding its taskbar. Modern WPF
handles DPI changes through the per-monitor-aware application manifest; recovery
runs after WPF processes those messages, and after an interactive move/resize ends.
Normal movement remains unrestricted between monitors. An offscreen or straddling
window is fitted to one work area after dragging ends. On exceptionally small
work areas, the minimum size is reduced enough to keep the window reachable.

Saves are asynchronous, serialized, and debounced by 500 ms; closing waits for the
final save. JSON is written to a temporary file in the same folder and then
replaced. Minimized/maximized coordinates are never persisted. Abrupt termination
can lose the last half second of movement. Invalid, oversized, unsupported-version,
or unreadable settings produce a warning and remain untouched; saving is disabled
for that session. Write failures warn without preventing window use. Reset layout
by closing the app and renaming/removing the settings file. No credentials or
browser data are part of these settings; local settings are excluded from Git.

### Project layout and implementation

- `DiscordFloatingWidget.slnx`: desktop application and focused xUnit tests.
- `src/DiscordFloatingWidget/DiscordFloatingWidget.csproj`: .NET 10 WPF build.
- `App.xaml` / `App.xaml.cs`: theme, asynchronous settings load, global shortcut
  registration, conflict warning and cleanup on close/exit.
- `Resources/Theme.xaml`: existing dark colors and keyboard focus styles.
- `Views/MainWindow.xaml`: topmost custom header and responsive placeholder views.
- `Views/MainWindow.xaml.cs`: responsive presentation, window buttons and visibility/
  focus restoration on the existing window.
- `Models/WindowLayout.cs`: validated saved geometry, with explicit units.
- `Services/LayoutGeometry.cs`: pure geometry calculations, independently tested.
- `Services/SettingsService.cs`: versioned JSON, safe loading and serialized writes.
- `Services/HotkeyService.cs`: UI-thread Win32 hotkey registration, `MOD_NOREPEAT`,
  `WM_HOTKEY` routing, conflict reporting and idempotent cleanup. Uses a window
  message hook, with no keyboard polling, keyboard hook or added runtime package.
- `Services/WindowsDisplay.cs`: small Win32 boundary for bounds, work areas and DPI.
- `Services/WindowStateService.cs`: placement, display messages, persistence lifecycle,
  and native hook/event cleanup. No polling or extra runtime package is required.
- `app.manifest`: per-monitor DPI awareness with standard user privileges.
- `tests/DiscordFloatingWidget.Tests/`: geometry, settings, native hotkey and WPF
  visibility/layout regression tests. Test settings use isolated temporary files.
- `.vscode/`: existing build/run tasks and debugger configuration.
- `global.json`: .NET 10 SDK and test-runner configuration.
- `.gitignore`: build output, local settings and future browser profile exclusions.

`WindowChrome` still supplies native dragging and resizing without transparent
window rendering. View behavior stays in code-behind, and persistence/display
logic lives in services; no unused ViewModel infrastructure is introduced.

References: [Microsoft's DPI guidance](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows),
[monitor selection](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromrect),
[global shortcut registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
and [.NET 10 test-runner configuration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

### Scope and commit

Milestone 3 stops at the global show/hide shortcut. Discord Web/WebView2, favorites,
tray integration, notifications, and automatic startup are not implemented. This
placeholder does not claim that Discord Web will be usable at the minimum window
size. Milestone 4 adds tray integration when requested; Discord login/chat is
planned for Milestone 5.

Commit message:

```text
feat: implement global widget toggle shortcut
```

Do not push until ready to publish.

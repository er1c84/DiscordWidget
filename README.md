# DiscordWidget-
A lightweight floating Discord chat widget for Windows built with C# and WPF.
An independent, unofficial Discord companion.

## Milestone 4: tray, single instance, and optional startup

The widget now stays available in the Windows notification area while it runs:

- **X** and **Alt+F4** hide it to the tray; neither exits nor releases the hotkey.
- **Minimize** still minimizes to the taskbar.
- Double-click the **Discord Widget** tray icon to show and focus the widget.
- Right-click the icon for **Show Widget / Hide Widget**, **Start with Windows**,
  and **Exit**. A minimized widget gets **Show Widget**.
- **Ctrl + Shift + D** keeps its existing show/hide behavior.
- Opening the desktop shortcut again restores the existing window rather than
  starting another widget, including when the existing window is hidden/minimized.
- Launches start visible. **Exit** saves layout, removes the tray icon, releases
  the hotkey and instance ownership, and fully closes the process.

Windows may put the tray icon behind the notification area's overflow arrow.
Its tooltip is **Discord Widget**. If tray initialization fails, a warning appears
and X retains ordinary close-to-exit behavior for that session. Modal warning
owners cannot be hidden by tray commands or the hotkey.

### Start with Windows

This is **off by default**. Right-click the tray icon and select **Start with
Windows** to enable it for your account; select it again to disable it. The check
mark reflects whether this app has a Windows startup registration. Merely launching
or closing the app does not create that registration. No administrator permission
is needed. Windows Task Manager/Settings can separately disable a registered startup
app, and Windows controls when startup apps actually run.

The setting manages only the `DiscordFloatingWidget` value under:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

The value contains the quoted absolute path of the running widget executable.
Keep that executable and its supporting files in a permanent location. After moving
the app, turn startup off and on from the new location to refresh the path. Launching
a DLL through `dotnet.exe`, a missing executable, or a command longer than Windows'
260-character Run-key limit cannot be enabled. Read/write failures leave the widget
usable and either explain the unavailable menu option or show a warning. Startup
registration is independent of the layout JSON, so invalid layout data does not
prevent you from changing this option. No installer is included yet.

### Manual tray and lifecycle verification

Close any old pre-Milestone-4 widget first, then build Release and launch using the
desktop shortcut below. Earlier builds do not participate in single-instance checks.

1. Confirm the window starts visible with a tray icon. Click X: the window should
   disappear while the tray icon remains. Double-click the icon: the same window
   should return with its size/position retained. Repeat using Alt+F4.
2. Minimize: the widget should remain on the taskbar. Double-click the tray icon
   or select **Show Widget** to restore it. Double-click again while visible: it
   should remain visible, not toggle off. Verify **Hide Widget** hides it.
3. With the widget hidden, open the desktop shortcut twice. The existing window
   should reappear, with one long-running widget process and one tray icon. Repeat
   while minimized and already visible, and try two launches close together.
4. From another ordinary application, use Ctrl+Shift+D twice. Also move/resize,
   expand/restore, and change monitors/scaling as described in Milestones 2 and 3.
   Verify the window remains reachable and the shortcut still works after X hides it.
5. Inspect **Start with Windows**: it should initially be unchecked. Enable it,
   exit, and relaunch: it should remain checked. Sign out/in when convenient:
   the widget should start visible. Disable it, then sign out/in again: it should
   no longer start automatically. Restore whichever preference you want afterward.
6. Move/resize, hide it, and select tray **Exit**. Its process and icon should go
   away. Relaunch: the saved layout should return and the hotkey should register.
   Windows sign-out/shutdown saves layout synchronously without being canceled by
   close-to-tray. Physical sign-out and shutdown require a manual check.

Automated checks cover native tray menu actions, close-to-tray without shutdown,
minimized restoration, repeated show requests, explicit Exit with saved layout,
session-ending preparation, single-instance election/activation/release, queued
startup requests, invalid/disconnected launchers, abandoned mutex recovery, and
startup enable/disable with quoted paths and unrelated values preserved. Tests use
unique temporary registry keys, never your actual Run entry. Physical tray clicks,
cross-application focus, and actual Windows logon remain manual checks. Windows
can restrict foreground focus, especially with elevated or exclusive fullscreen apps.

## Milestone 3: global show/hide shortcut

Press **Ctrl + Shift + D** from the widget or another desktop application:

- If the widget is visible, it hides and continues running.
- If it is hidden or minimized, the same window reappears and requests foreground
  focus. Its layout and previously focused control are retained where possible.
- Holding the keys down toggles once; release and press again for another toggle.
- While a modal warning dialog is open, the shortcut does not hide its owner.
- Tray **Exit** unregisters the shortcut and exits. X and hiding do not exit.

If Windows cannot register the shortcut, a warning appears and the app continues
without it. Tray controls, minimize, expand and dragging/resizing still work. Close the
other app reserving the shortcut and restart this widget to retry. As selected
for this milestone, the shortcut is fixed; there is no shortcut editor or automatic
fallback. Milestone 4 now prevents duplicate widget instances and adds tray controls.

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
5. If another application owns Ctrl+Shift+D, expect a warning and a usable widget
   with tray controls. Exit the widget and close the conflicting application, then
   relaunch to retry registration. Opening a second widget now restores the first;
   it is no longer a way to test a hotkey conflict. During a warning dialog, the
   hotkey and tray actions must not hide that dialog's owner.

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
   Verify position and size. Repeat after minimizing and using tray **Exit**,
   and after maximizing by double-clicking the header. It should restart normally
   at the last normal bounds, never minimized or maximized.
6. Move to a secondary monitor, including one arranged left of or above the primary
   monitor. Use tray **Exit** and relaunch there. Then disconnect that display and relaunch;
   the whole widget, including its header, should be inside a remaining work area.
   Also disconnect the display while the widget is open and while it is minimized,
   then restore from the taskbar.
7. Move between monitors at 100%, 125%, 150%, and 200% scaling where available.
   Change scale and resolution while it is open and between launches. The UI
   should scale clearly and keep its header reachable. Expand near the bottom or
   right edge: it should fit above/beside the taskbar. Test a taskbar on another
   edge if that configuration is available.
8. Use tray **Exit** and back up `%LOCALAPPDATA%\DiscordFloatingWidget\settings.json`.
   Replace its contents with invalid JSON and relaunch. Expect a warning and safe
   default placement; the invalid file must remain intact and saving is disabled
   for that session. Exit through the tray, then restore the backup or rename/remove the file.
9. Minimize restores through the taskbar. X and Alt+F4 hide to the tray.
   Use tray **Exit** to save layout and exit the process.

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

Saves are asynchronous, serialized, and debounced by 500 ms; tray **Exit** waits for the
final save. JSON is written to a temporary file in the same folder and then
replaced. Minimized/maximized coordinates are never persisted. Abrupt termination
can lose the last half second of movement. Invalid, oversized, unsupported-version,
or unreadable settings produce a warning and remain untouched; saving is disabled
for that session. Write failures warn without preventing window use. Reset layout
by using tray **Exit** and renaming/removing the settings file. No credentials or
browser data are part of these settings; local settings are excluded from Git.

### Project layout and implementation

- `DiscordFloatingWidget.slnx`: desktop application and focused xUnit tests.
- `src/DiscordFloatingWidget/DiscordFloatingWidget.csproj`: .NET 10 WPF build.
- `App.xaml` / `App.xaml.cs`: theme, asynchronous settings load, global shortcut
  registration, tray/instance coordination, and explicit shutdown cleanup.
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
- `Services/TrayIconService.cs`: native notification icon, dynamic menu and cleanup.
- `Services/SingleInstanceService.cs`: per-user/session mutex and current-user-only
  named-pipe activation, with foreground permission requested by the launcher.
- `Services/StartupService.cs`: opt-in per-user Run entry, quoted path and error handling.
- `Services/WindowStateService.cs`: placement, display messages, persistence lifecycle,
  and native hook/event cleanup. No polling or extra runtime package is required.
- `app.manifest`: per-monitor DPI awareness with standard user privileges.
- `tests/DiscordFloatingWidget.Tests/`: geometry, settings, native hotkey and WPF
  visibility/layout, tray, single-instance and startup regression tests. Test settings
  use isolated temporary files and registry keys.
- `.vscode/`: existing build/run tasks and debugger configuration.
- `global.json`: .NET 10 SDK and test-runner configuration.
- `.gitignore`: build output, local settings and future browser profile exclusions.

`WindowChrome` still supplies native dragging and resizing without transparent
window rendering. View behavior stays in code-behind, and persistence/display
logic lives in services; no unused ViewModel infrastructure is introduced.

References: [Microsoft's DPI guidance](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows),
[monitor selection](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromrect),
[global shortcut registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
[notification icons](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon),
[Windows Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys),
and [.NET 10 test-runner configuration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

### Scope and commit

Milestone 4 adds tray integration, one instance per Windows user/session, and an
optional Start with Windows setting. Discord Web/WebView2, favorites, notifications,
and a distributable installer remain unimplemented. This placeholder does not
claim that Discord Web will be usable at the minimum window size. Discord
login/chat is planned for Milestone 5.

Commit message:

```text
feat: add tray integration and optional Windows startup
```

Commit locally; do not push for this milestone.

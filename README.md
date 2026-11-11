# DiscordWidget-
A lightweight floating Discord chat widget for Windows built with C# and WPF.
An independent, unofficial Discord companion.

## Compact chat prototype

The prototype uses a dark, compact frame with two small, labeled tabs: **Discord**
and **Favorites**. Both stay visible at the minimum size, with a highlighted active
tab and a clear keyboard focus outline. The 28-DIP tabs share a slim toolbar with
Add and Options. Switching tabs keeps the same browser and unsent draft.
The Options menu uses a dark background, light text, and clear highlighted and
disabled states to match the widget.
It restores your last selected favorite on launch; if none is saved, it shows
**Your favorites**. New windows start at 320 × 420 DIPs and can shrink to 260 × 300
without replacing the conversation with an Expand prompt. Existing saved window
sizes are preserved: choose **⋯ → Compact size** to try the new size.

**Chat-only view (experimental)** attempts to hide Discord's navigation around a
recognized DM conversation. The message list and typing box are still Discord's.
This is an experimental webpage presentation change, not an official Discord
integration or a native replacement client. Unknown layouts stay in their normal
view. Turn the option off under **⋯** to restore the standard webpage at any time.
The selectors need real-account verification and may need updates when Discord
changes its page. See [prototype details and acceptance checks](docs/COMPACT-PROTOTYPE.md).

After 60 seconds hidden or minimized, the widget asks WebView2 to suspend the page.
It resumes when shown, without intentionally reloading the conversation or clearing
the draft. Updates pause while suspended; suspension is best effort. The app still
runs Discord's browser engine while active, so cosmetic changes alone do not
establish lower CPU or memory use.

### Run the current build

Fully exit any running widget through its tray menu before rebuilding. X only
hides it, so a rebuild can fail with a locked-executable warning while it is running.

```powershell
dotnet build .\DiscordFloatingWidget.slnx --configuration Release
dotnet test --solution .\DiscordFloatingWidget.slnx --configuration Release --no-build
dotnet run --project .\src\DiscordFloatingWidget\DiscordFloatingWidget.csproj --configuration Release --no-build
```

The desktop shortcut opens that same Release build. Reopening it or using the
last command restores the running widget without rebuilding. If Microsoft Edge
WebView2 Runtime is missing, the app provides its official download link and an
**Open in Browser** fallback. Install the Runtime yourself and choose Reload to
retry. The app uses Microsoft's stable WebView2 SDK package **1.0.4314.35** and the
installed Evergreen Runtime, with normal browser security enabled.

### Favorites and navigation

- **+** asks for a name, friend/group type, and Discord conversation URL.
  Open the conversation in Discord Web in a normal browser and copy its address.
- Friend and group DM links use `https://discord.com/channels/@me/CONVERSATION_ID`.
  Existing saved server links are preserved in **Favorites**. New server favorites
  are outside this prototype.
- A copied message link is reduced to its conversation. Query strings and fragments
  are removed; login routes, non-Discord hosts, and invalid IDs cannot be saved.
- Open **Favorites**, then use the pencil to edit a favorite and X beside it to remove it with confirmation.
  Names are unique ignoring case; the initial limit is 100 favorites.
- Select a saved conversation in **Favorites** to open the **Discord** tab.
  Click **Discord** to return to the current conversation without reloading it.
  Use Tab to focus the tab strip and arrow keys to switch tabs.
  **⋯** contains favorites management, compact size, Back, Reload, Open Discord,
  Open in Browser, the focus-view toggle, and Clear browser data.
- Clicking an ordinary external HTTP/HTTPS link opens your default browser. Other
  protocols and automatic external redirects are not launched by the app.
- The browser stays visible at small sizes. Expand/restore remains available in the
  native title bar for login screens or layouts that need more room.
- Returning to Favorites keeps the same browser instance. Hiding/minimizing starts
  the 60-second sleep timer; restoring cancels it or resumes the sleeping page.
  Network failures, missing Runtime, and browser-process failures provide recovery
  messages, Reload, and browser fallback rather than bypassing website restrictions.

### Login, local data, and clearing the browser profile

Sign in yourself using Discord's actual login page, including any QR, 2FA or CAPTCHA
that Discord requires. The widget does not collect credentials, import another
browser's session, request a user token, extract Discord messages, or automate messages.

Files are kept outside the repository under `%LOCALAPPDATA%\DiscordFloatingWidget`:

- `favorites.json`: versioned, manually saved names/types/conversation URLs.
- `settings.json`: the existing window layout.
- `preferences.json`: last favorite ID and the experimental focus-view preference.
- `WebView2\`: the widget's dedicated browser cookies, site data and cache.

The dedicated profile is reused between launches so Discord can preserve login
when its own session rules allow it. Password autosave and general autofill are
disabled. Browser profile files and favorites are excluded from Git. Corrupt or
future-version favorites files are preserved and editing is disabled until the
file is renamed after exiting; a warning explains recovery. Writes replace a
same-folder temporary file, and failed saves keep the previous list.

Use **⋯ → Clear browser data** or the tray's **Clear browser data**
command. It asks for confirmation, unloads Discord, and clears this widget's browser
profile using WebView2's native API. It signs you out while preserving favorites,
window layout, startup preference, and other browsers. Reopen Discord to sign in.

### Verification and current limits

The Release build and automated tests cover favorites validation/persistence,
malformed data and failed writes, URL trust boundaries, native picker startup and
compact layout, WebView2 initialization with isolated data, synthetic-cookie
persistence across browser restarts, profile clearing, and the existing layout,
tray, hotkey, single-instance and startup features. An explicit integration check
successfully loaded Discord's public page (HTTP 200) in a temporary unsigned-in
profile. Authentication, reading/sending real messages, and Discord session renewal
still require your manual check; this does not claim complete Discord compatibility.

Try the following on your account:

1. Open Discord and complete sign-in yourself. Expand manually if login needs more room.
2. Open a DM and group DM. Read existing messages, scroll and
   send a test message yourself in a suitable conversation.
3. Save a few copied conversation URLs. Test opening, editing and removing them.
   Exit and relaunch: favorites and the last selected conversation should return.
   Verify your Discord session persists when Discord permits it.
4. Use the options menu and an ordinary website link. Check that external
   links open in your default browser and Open in Browser opens the current
   conversation. Your default browser has its own sign-in session.
5. Resize, expand/restore, hide/show, minimize, use Ctrl+Shift+D, and relaunch through
   the desktop shortcut. Confirm one widget instance and no unnecessary reload.
6. Cancel Clear browser data once, then confirm it when ready. Check that the
   widget signs out, favorites/layout remain, and your normal browser stays signed
   in. Reopen Discord and sign in again yourself.
7. Disconnect/reconnect the network, then try Reload. If Discord rejects the
   embedded environment, use Open in Browser and report the issue; do not bypass it.

This milestone focuses on text chat. Voice/video, notifications/unread counts,
attachment downloads inside the widget, favorite reordering/custom icons, and an
installer remain future work. Camera/microphone/location/notification permissions
are not granted; clipboard read follows normal browser consent. Download attempts
show a message directing you to Open in Browser. No native API or message bridge
is exposed to Discord's page. See [compatibility notes](docs/LIMITATIONS.md).

## Milestone 4: tray, single instance, and optional startup

The widget now stays available in the Windows notification area while it runs:

- **X** and **Alt+F4** hide it to the tray; neither exits nor releases the hotkey.
- **Minimize** still minimizes to the taskbar.
- Double-click the **Discord Widget** tray icon to show and focus the widget.
- Right-click the icon for **Show Widget / Hide Widget**, **Start with Windows**,
  **Clear browser data**, and **Exit**. A minimized widget gets **Show Widget**.
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
- The compact prototype now uses a **260 × 300 WPF device-independent unit (DIP)**
  minimum so the conversation and typing area remain available.
- A default size of 320 × 420 DIPs; expand grows toward 720 × 700 DIPs, bounded
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

1. Launch the app. On first launch, expect a centered 320 × 420 DIP dark window
   with the **Your favorites** picker. Later launches restore bounds and the last favorite.
2. Put an ordinary application in front of it: the widget should stay above it.
   Other topmost windows, exclusive fullscreen apps, and secure desktop prompts
   can still cover it.
3. Drag an empty part of the header. Resize using each of the four edges and four
   corners. Movement and resizing should remain smooth. Buttons should still
   work, and dragging should allow crossing monitor boundaries.
4. Resize to about 300 × 350 and the 260 × 300 minimum. The conversation stays
   visible without an Expand replacement. Try the header's expand/restore button,
   then **⋯ → Compact size**. Tab and Shift+Tab through controls and activate
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
- `Views/MainWindow.xaml`: topmost custom header and responsive Discord content host.
- `Views/DiscordView.xaml` / `.cs`: native favorites picker, toolbar, compact view and browser lifecycle.
- `Views/FavoriteDialog.xaml` / `.cs`: named conversation URL editor.
- `Models/FavoriteConversation.cs`: validated saved favorite and conversation type.
- `Services/FavoritesService.cs`: versioned, atomic favorites persistence.
- `Services/DiscordWebService.cs`: lazy WebView2, persistent profile, navigation and clearing.
- `Services/DiscordUrlPolicy.cs` / `ExternalBrowser.cs`: trusted navigation and browser fallback.
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

Milestone 5 integrates Discord Web with persistent browser data and brings a basic
favorites picker forward from Milestone 6. Voice/video, notifications/unread state,
favorite reordering/custom icons, and public installer packaging remain future work.

Commit message:

```text
feat: integrate Discord Web with a favorites picker
```

Commit locally; do not push for this milestone.

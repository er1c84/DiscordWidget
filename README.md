# DiscordWidget-
A lightweight floating Discord chat widget for Windows built with C# and WPF.
An independent, unofficial Discord companion.

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
   Tray hiding and the global shortcut belong to later milestones.

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
- `App.xaml` / `App.xaml.cs`: theme and asynchronous settings load before showing.
- `Resources/Theme.xaml`: existing dark colors and keyboard focus styles.
- `Views/MainWindow.xaml`: topmost custom header and responsive placeholder views.
- `Views/MainWindow.xaml.cs`: responsive presentation and window buttons.
- `Models/WindowLayout.cs`: validated saved geometry, with explicit units.
- `Services/LayoutGeometry.cs`: pure geometry calculations, independently tested.
- `Services/SettingsService.cs`: versioned JSON, safe loading and serialized writes.
- `Services/WindowsDisplay.cs`: small Win32 boundary for bounds, work areas and DPI.
- `Services/WindowStateService.cs`: placement, display messages, persistence lifecycle,
  and native hook/event cleanup. No polling or extra runtime package is required.
- `app.manifest`: per-monitor DPI awareness with standard user privileges.
- `tests/DiscordFloatingWidget.Tests/`: geometry and settings regression tests.
- `.vscode/`: existing build/run tasks and debugger configuration.
- `global.json`: .NET 10 SDK and test-runner configuration.
- `.gitignore`: build output, local settings and future browser profile exclusions.

`WindowChrome` still supplies native dragging and resizing without transparent
window rendering. View behavior stays in code-behind, and persistence/display
logic lives in services; no unused ViewModel infrastructure is introduced.

References: [Microsoft's DPI guidance](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows),
[monitor selection](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromrect),
and [.NET 10 test-runner configuration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

### Scope and commit

Milestone 2 stops at the native floating shell. Discord Web/WebView2, favorites,
global shortcuts, tray integration, notifications, and automatic startup are not
implemented. This placeholder does not claim that Discord Web will be usable at
the minimum window size. The next milestone is the global shortcut, when requested.

Commit message:

```text
feat: add floating window controls and persistent layout
```

Do not push until ready to publish.

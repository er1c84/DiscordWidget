# DiscordWidget-
A lightweight floating Discord chat widget for Windows built with C# and WPF.

## Milestone 1: desktop shell

This initial version displays a dark, compact WPF window with a custom title bar
and a placeholder for future Discord Web integration. It is an independent,
unofficial Discord companion.

### Run in VS Code on Windows

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
and the Microsoft C# extension for VS Code. Open this repository folder, then
use the integrated PowerShell terminal:

```powershell
dotnet --version
dotnet build .\DiscordFloatingWidget.slnx
dotnet run --project .\src\DiscordFloatingWidget\DiscordFloatingWidget.csproj
```

The SDK version should start with `10.`. You can also press F5 to build and debug,
or select **Terminal > Run Task > run widget**.

### What to verify

- The window starts centered at 360 × 500 WPF device-independent units.
- The dark header displays **Discord Floating Widget**.
- The placeholder reads **Your chat, within reach.**
- Drag the header to move the window; drag edges/corners to resize it.
- Minimize restores through the taskbar.
- Expand enlarges the widget; clicking it again restores the prior size.
- Tab between header buttons and press Space to activate them. Focus is visible.
- Close or Alt+F4 exits the process. This milestone does not hide to the tray.

### Project layout

- `DiscordFloatingWidget.slnx`: solution containing the desktop project.
- `src/DiscordFloatingWidget/DiscordFloatingWidget.csproj`: .NET 10 WPF build configuration.
- `App.xaml` / `App.xaml.cs`: application startup and resource loading.
- `Resources/Theme.xaml`: shared colors and accessible header button styling.
- `Views/MainWindow.xaml`: custom header and chat placeholder layout.
- `Views/MainWindow.xaml.cs`: minimize, expand/restore, and close controls.
- `.vscode/`: build/run tasks, debugger setup, and C# extension recommendation.
- `.gitignore`: excludes build output, local settings, and browser profiles.

WindowChrome supplies native title-bar dragging and edge resizing without
transparent-window rendering. View-specific button handlers remain in code-behind;
there is no application state yet that needs a ViewModel or service.

### Scope and next steps

Milestone 1 has no Discord connection, login, WebView2, favorites, global shortcut,
tray icon, notifications, saved geometry, or automatic Windows startup.
The initial minimum size is 300 × 240; compact responsive layouts come later.

Milestone 2 adds always-on-top behavior, smaller responsive layouts, persistent
window geometry, and monitor/display-scaling recovery. It should be implemented
only after this milestone is verified on Windows.

WPF runs on Windows. A Linux build check can use
`dotnet build DiscordFloatingWidget.slnx -p:EnableWindowsTargeting=true`, but this
does not verify the Windows UI or runtime behavior. No automated tests are added
for this initial presentation-only shell; use the manual checklist above.

### Commit this milestone

```powershell
git add README.md .gitignore .vscode DiscordFloatingWidget.slnx src
git commit -m "feat: initialize Discord floating widget WPF application"
git push origin HEAD
```

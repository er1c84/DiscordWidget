# Compact chat prototype

## Scope

- Dark native frame, 320 × 420 DIP default, 260 × 300 DIP minimum.
- A 28-DIP tab strip labeled Discord and Favorites, with visible selection and
  keyboard focus. Both tabs, Add, and Options fit at the minimum window size.
  Favorites opens the native saved-conversation list; Discord returns to the same
  browser without navigation or loss of an unsent draft.
- Restore the last selected favorite on restart. Missing/deleted favorites fall
  back to the native picker. Older saved channels remain in Manage favorites.
- Remove the 480 × 320 browser cutoff and the automatic Expand replacement.
- Optional experimental chat-only webpage presentation, enabled by default in
  this prototype. **⋯ → Chat-only view (experimental)** restores normal Discord.
- Suspend after 60 seconds hidden/minimized; resume on show. Keep one browser,
  its profile and its unsent draft, without forced reloads.

## Presentation boundary

Discord's website still supplies, renders and sends the messages. The prototype
does not implement a second composer, acquire account tokens, use private APIs,
read message text, or automate sending. The script runs only in the top-level
HTTPS discord.com document. It activates only on numeric `/channels/@me/…` routes
with one recognizable composer inside a main chat region and a message-list
marker. It inspects element structure; no page content returns to native code.

The script marks ancestor layout containers and unrelated siblings, applies a
small stylesheet, and verifies that the typing box remains in view. If recognition
or geometry checks fail, it removes its changes. Login/other routes are unstyled.
Overlay containers and dialogs remain available. DOM child-list changes are
debounced; there is no periodic message polling. Disabling the view disconnects
the observer and removes its marks/styles without replacing Discord's elements.

This is an unsupported presentation experiment. The fixture resembles a Discord
layout but is not evidence that Discord currently uses those exact elements.
Discord updates, permissions prompts and alternate layouts can require the normal
view. Do not call this production-ready until the real-account checks below pass.

## Resource behavior

The window tells the browser when it is hidden or minimized. A single 60-second
timer requests `TrySuspendAsync`; showing the window cancels the timer and resumes
the browser. A late suspend completion after restore is followed by another resume.
Suspension can be declined by WebView2. Updates are paused while asleep and catch
up after reopening. Draft retention is tested on a fixture; Discord itself controls
real drafts and reconnect behavior. There is no guarantee of a particular memory
limit or savings while actively chatting.

Optional isolated diagnostics:

```powershell
$env:DISCORD_WIDGET_SCREENSHOT = "$PWD\TestResults\prototype-picker.png"
$env:DISCORD_WIDGET_PERFORMANCE = "$PWD\TestResults\prototype-performance.txt"
dotnet test --solution .\DiscordFloatingWidget.slnx --configuration Release
```

The performance check samples the test host plus its temporary WebView2 process
group for five seconds while visible and five seconds while suspended, after a
five-second startup warmup and a one-second suspension settling period. It uses
a synthetic conversation and a shortened timer for the native test. It reports
machine-normalized CPU, working set, private bytes, and sample duration. These
numbers are diagnostic, not a signed-in Discord benchmark or a before/after
comparison against Milestone 5. The production delay is tested separately as
60 seconds. No real profile, credentials or messages are inspected.

## Acceptance checks on your account

1. Exit the old widget, then open the desktop shortcut. Choose **⋯ → Compact size**.
2. Open a saved friend. Confirm the servers/sidebar/member list disappear, messages
   remain readable, and Discord's own typing box is visible at 320 × 420 and 260 × 300.
3. Type an unsent draft. Toggle chat-only view off/on; confirm text and selection
   remain usable. Send a message yourself in a suitable conversation.
4. Switch to a saved group. Verify scrolling, replies, links, emojis, and relevant
   popovers. This text prototype intentionally hides some composer accessory buttons.
5. Select a favorite, Exit and reopen. Confirm the same favorite and login return.
6. Leave an unsent draft, hide/minimize for more than 60 seconds, then restore via
   shortcut or Ctrl+Shift+D. Verify the draft survives and new messages catch up.
7. Toggle the focus view off and verify normal navigation returns. If recognition
   fails, report whether the normal page remains usable; use Expand/Open in Browser.
8. Compare the complete widget/WebView2 process group's CPU and memory under the
   same real conversation when visible and after sleeping. Do not compare only the
   small native executable against Discord's entire process group.

References: [WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance),
[TrySuspendAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.trysuspendasync).

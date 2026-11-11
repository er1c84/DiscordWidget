# Discord Web compatibility and data

Milestone 5 embeds the official Discord website using the installed Microsoft
Edge WebView2 Evergreen Runtime. It uses a separate local profile; it does not
share cookies or sign-in with Discord Desktop, Edge, or Firefox.

## Verification

Automated checks verify local favorites and native Windows behavior, WebView2
initialization, synthetic persistent cookies in an isolated test profile, and
clearing that profile. An opt-in integration check loaded Discord's public page
successfully with HTTP 200. These checks do not log in or send messages.

Real-account login, DM/group/server navigation, message reading/sending, and session
persistence are manual checks listed in the README. Discord can change browser
compatibility, login policy, session expiration, or page layout independently of
this app. An HTTP 200 alone does not establish that every Discord feature works.

## Supported approach

- Use Discord's own login and text UI. Complete authentication yourself.
- Use manually named conversation URL shortcuts. Friends/server membership are
  not discovered via private APIs or DOM scraping.
- Keep browser security and certificate checks enabled. The explicitly enabled
  compact prototype adds a presentation script to top-level HTTPS discord.com
  pages. It checks layout structure and changes styles only for recognized DM
  routes. It does not read message text, credentials, cookies, React state or
  private APIs, or send page content to the app. No account automation, host
  objects, or web-to-native message bridge are used. This experimental departure
  from Milestone 5's unmodified-page approach can be disabled in the options menu.
- If login or chat is blocked in the embedded environment, use Open in Browser
  and report the limitation. The browser uses its own Discord session.
- Non-Discord HTTP/HTTPS links open externally only for user-initiated navigation;
  automatic external redirects and arbitrary protocols are not launched.
- Voice/video, web notifications/unread counts and in-widget attachment downloads
  are outside this milestone. Permission handling and download messages reflect
  that scope. Browser clipboard read uses the normal consent behavior.

## Profile management

`%LOCALAPPDATA%\DiscordFloatingWidget\WebView2` contains the widget's local browser
state. Clear browser data requires confirmation, unloads the page before using
WebView2's ClearBrowsingDataAsync(AllProfile), and recreates the control when it
is next needed. No recursive filesystem profile deletion is used. Favorites and
layout are separate files and remain intact. The app does not log credentials,
cookies, authentication headers, message text, or navigation query strings.

WebView2 may need a short interval to exit its browser process after its final
control is disposed. Tests wait for BrowserProcessExited before removing their
own temporary directories. Test captures contain synthetic favorite names only.

The compact prototype retains one WebView2 instance. It requests suspension after
60 seconds hidden/minimized and resumes it when shown. Suspension is best effort,
pauses page updates, and does not guarantee a fixed CPU or memory reduction. The
minimum usable window is now 260 × 300 DIPs (or the available work area on a
smaller display). See [prototype verification](COMPACT-PROTOTYPE.md).

References:

- [WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [WebView2 user data folders](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder)
- [WebView2 threading model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model)
- [ClearBrowsingDataAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.clearbrowsingdataasync)

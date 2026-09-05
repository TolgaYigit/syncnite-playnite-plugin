# Syncnite

The [Playnite](https://playnite.link) plugin for [Syncnite](https://syncnite.com) — it
mirrors your Playnite library to the cloud so you can browse, tag, and organize it from any
browser, with edits syncing straight back to Playnite on your PC.

## Requirements

- Playnite, with a plugin built against [PlayniteSDK](https://api.playnite.link/) 6.11.0
- .NET Framework 4.6.2 (matches Playnite's own runtime — nothing extra to install)

## What it does

Two one-way streams, so they can never conflict:

- **Playnite → web**: install status, playtime, last played, source, date added, and (if you
  use the [PlayniteAchievements](https://github.com/justin-delano/PlayniteAchievements) plugin)
  achievement counts.
- **Web → Playnite**: tags, categories, notes, completion status, favorites, and hidden status.

Each field only ever has one side editing it.

## Supported plugins

Syncnite works fine on its own, but plays specifically well with a couple of others:

| Plugin | What Syncnite does with it |
| --- | --- |
| [PlayniteAchievements](https://github.com/justin-delano/PlayniteAchievements) | Reads its local achievement cache (read-only) and pushes unlocked/total counts per game. Off by default in Syncnite's settings until it detects the plugin installed and enabled. |
| [Duplicate Hider](https://github.com/felixkmh/DuplicateHider) | Duplicate Hider hides all but one copy of a game you own on multiple sources. Syncnite still pushes every copy, hidden ones included — that's what lets the web app's game detail page show an "Also owned on" switcher between them. |

Nothing to configure for Duplicate Hider specifically — it works because Syncnite doesn't skip
hidden games, not because of any direct integration between the two.

## Installing

Grab the latest `.pext` from [Releases](../../releases) and drag it onto a running Playnite
window (or double-click it). Then, in Playnite:

1. Create a free account at [syncnite.com](https://syncnite.com) if you don't
   have one.
2. On the web, go to Settings and click **Generate pairing code**.
3. In Playnite, go to **Add-ons → Extension Settings → Syncnite**, paste the code in, and
   click **Connect**.
4. Click **Sync Now**, or just leave it — it syncs automatically on startup and whenever your
   library changes.

## Building from source

Also needs a local Playnite install, for `Toolbox.exe`, used to package the `.pext`.

```powershell
./pack.ps1
```

Packages the built plugin to `dist/`. Pass `-ToolboxPath` explicitly if it can't find your
Playnite install automatically.

## Testing

```powershell
dotnet test PlayniteCloudSync.Tests/PlayniteCloudSync.Tests.csproj
```

(There's no `.sln` tying the two projects together, so a bare `dotnet test` in the repo root
picks up the main plugin project instead - which isn't a test project - and quietly does
nothing. Point it at the test project explicitly, as above.)

Covers `Batching` (the chunking logic behind large-library sync) and `CloudSyncApiClient`
(request/response handling, including the 413-with-no-body case that motivated writing these
in the first place) against a fake `HttpMessageHandler` - no real network calls, no Playnite
install needed. Runs automatically on every push and PR via GitHub Actions.

## Feedback

Bugs and feature requests go to
[syncnite-feedback](https://github.com/TolgaYigit/syncnite-feedback) — Issues for bugs,
Discussions for ideas and questions. That repo covers the whole project, not just the plugin.

## Development

Built with the help of [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding
assistant).

## License

[MIT](LICENSE) © Tolga Yigit

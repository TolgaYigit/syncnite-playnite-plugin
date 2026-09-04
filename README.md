# Syncnite

The [Playnite](https://playnite.link) plugin for [Syncnite](https://syncnite.vercel.app) — it
mirrors your Playnite library to the cloud so you can browse, tag, and organize it from any
browser, with edits syncing straight back to Playnite on your PC.

This repo is the plugin only. The web app and backend are a separate, private project — this
side has no secrets of its own (no API keys, no credentials), which is why it can live here in
the open.

## What it does

Two one-way streams, so they can never conflict:

- **Playnite → web**: install status, playtime, last played, source, date added, and (if you
  use the [PlayniteAchievements](https://github.com/justin-delano/PlayniteAchievements) plugin)
  achievement counts.
- **Web → Playnite**: tags, categories, notes, completion status, favorites, and hidden status.

Each field only ever has one side editing it.

## Installing

Grab the latest `.pext` from [Releases](../../releases) and drag it onto a running Playnite
window (or double-click it). Then, in Playnite:

1. Create a free account at [syncnite.vercel.app](https://syncnite.vercel.app) if you don't
   have one.
2. On the web, go to Settings and click **Generate pairing code**.
3. In Playnite, go to **Add-ons → Extension Settings → Syncnite**, paste the code in, and
   click **Connect**.
4. Click **Sync Now**, or just leave it — it syncs automatically on startup and whenever your
   library changes.

## Building from source

Requires .NET Framework 4.6.2 and a local Playnite install (for `Toolbox.exe`, used to package
the `.pext`).

```powershell
./pack.ps1
```

Packages the built plugin to `dist/`. Pass `-ToolboxPath` explicitly if it can't find your
Playnite install automatically.

## Feedback

Bugs and feature requests go to
[syncnite-feedback](https://github.com/TolgaYigit/syncnite-feedback) — Issues for bugs,
Discussions for ideas and questions. That repo covers the whole project, not just the plugin.

## Development

Built with the help of [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding
assistant).

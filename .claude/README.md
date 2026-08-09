# `.claude/`

Project-level configuration for Claude Code. Everything here is checked in and
shared by everyone working on the repo, with one exception noted below.

| Path                  | Purpose |
| --------------------- | ------- |
| `settings.json`       | Shared settings: permission allowlist, enabled MCP servers, hooks. Checked in. |
| `settings.local.json` | Personal overrides. Git-ignored, never commit it. |
| `skills/`             | Reusable procedures Claude loads on demand. See `skills/README.md`. |
| `commands/`           | Slash commands. Each `.md` becomes `/<filename>`; subfolders namespace it as `/<folder>:<name>`. |
| `agents/`             | Subagent definitions. Each `.md` needs `name` and `description` frontmatter, plus an optional `tools` list to narrow what it may do. |

Project instructions themselves live in `CLAUDE.md` at the repo root, not here.

Note that `commands/` and `agents/` load *every* `.md` file in them, so a stray
`README.md` in either folder turns into a real slash command or a broken agent.
Documentation for those two goes here instead, which is why they hold only a
`.gitkeep` until there is something real to put in them.

## Conventions

- Keep anything in this folder tool-agnostic where possible. If a procedure is
  really about the game, write it down in `CLAUDE.md` or a skill, not in a
  settings file.
- Prefer adding a skill over growing `CLAUDE.md`. `CLAUDE.md` is loaded into
  every session; skills are loaded only when relevant.
- Settings changes that affect the whole team go in `settings.json`. Anything
  tied to one machine (absolute paths, personal MCP servers) goes in
  `settings.local.json`.

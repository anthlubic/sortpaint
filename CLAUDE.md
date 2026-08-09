- Never use em-dashes, anywhere: not in code comments, doc comments, commit messages, Markdown, or
  strings the game shows. Use a comma, colon, semicolon, parentheses, or two sentences instead.
- This project must be written in C# instead of GDScript
- This is a Godot 4.7.x game
- Scenes must be authored in a way that a human game developer can use the Godot UI to tweak the scene. Everything that cannot be represented in the scene can be moved to C#.
- C# source goes under src/. Pure game logic lives in src/Core/ with no Godot dependencies so it can be unit tested; Godot-facing code (nodes, resources, views) goes in src/
- The scripts/ folder is for utility scripts only (tooling, asset generation), never game code
- Tests should go under tests/.  As much as possible, extract pure game logic to separate cs files so it can be tested.  Be pragmatic about deciding what to extract and what to leave in scenes.
- Build and export optimizations go in by default, not after measuring whether they are needed. A
  web publish relinks the engine and a Pages deploy on top of that is minutes per attempt, so
  "ship it and see if it is a problem" costs far more than just doing it. Same for anything that
  shrinks the bundle or the deploy. Prefer fixing it at the source (an MSBuild property that stops
  a file being emitted) over trimming the output afterwards: the bundle is a manifest of assets the
  runtime fetches at boot, and deleting one it lists breaks the build in a way that only shows up
  in the browser console. See the `exporting` skill.

## Godot MCP

The project is wired to [godot-mcp](https://github.com/tugcantopaloglu/godot-mcp), cloned and built at `/home/anth/code/godot-mcp`. Config lives in `.mcp.json`, which pins `GODOT_PATH` and restricts the server to this project via `GODOT_MCP_ALLOWED_DIRS`.

`addons/godot_mcp/mcp_interaction_server.gd` is the one deliberate exception to the C#-only rule. It is vendored third-party GDScript, not game code, and it exists only because the MCP `game_*` runtime tools need an autoload inside the running game. Registered as the `McpInteractionServer` autoload in `project.godot`; it opens a TCP listener on 127.0.0.1:9090 while the game runs. Do not write game logic in it, and do not treat it as precedent for new .gd files.

It carries one local edit against upstream: `_ready` returns early unless `OS.is_debug_build()`, so exported release builds never open the port. Re-apply that guard if the file is ever re-copied from godot-mcp.
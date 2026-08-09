# Skills

A skill is a folder holding a `SKILL.md` plus any supporting files. Claude reads
the frontmatter of every skill at startup and pulls the body into context only
when the `description` matches what is being asked. That makes skills the right
home for procedures that are long, occasional, or easy to get subtly wrong.

## Layout

```
.claude/skills/
  my-skill/
    SKILL.md          # required
    references/       # optional: docs Claude reads when it needs detail
    scripts/          # optional: helper executables the skill invokes
    assets/           # optional: templates, fixtures, sample data
```

The folder name is the skill name. Use lowercase kebab-case.

## `SKILL.md` template

```markdown
---
name: my-skill
description: What this does and when to use it. Mention the concrete triggers - file types, commands, task phrasing - because this line is the only thing Claude sees before deciding to load the skill.
---

# My Skill

## When to use

Short list of situations that call for this. Just as important, note when *not*
to use it.

## Steps

1. Concrete, ordered instructions.
2. Include exact commands and file paths.
3. Say what a correct result looks like so the outcome can be checked.

## Notes

Gotchas, edge cases, links to `references/` files for anything long.
```

## Writing guidance

- The `description` does all the routing work. Write it for a reader deciding
  "is this relevant right now", and include the words someone would actually
  use when asking.
- Keep `SKILL.md` under a few hundred lines. Push long reference material into
  `references/` and link to it, so it is read only when needed.
- Write imperatively and concretely. Real commands beat prose about commands.
- One skill per procedure. If a skill needs "and also" in its description, it is
  probably two skills.
- Test a new skill by starting a fresh session and phrasing a request the way a
  teammate would. If it does not load, the description is the thing to fix.

## What is here

- `new-level/`: adding a level from an image, whether the developer supplies
  one or it is generated. Wraps `scripts/import_level.py` and the fixture and
  verification steps around it.

## Candidates for this project

Things worth capturing as they stabilise: the build and test loop, and the
scene conventions that keep scenes editable in the Godot UI.

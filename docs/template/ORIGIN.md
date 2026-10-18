# Origin

This tree is the **ProjectSurvive** game repository with the game removed: the shell,
the data pipeline, the tooling and the documentation that any project needs, and
nothing that only a survivor-like needs. It was extracted on the `template/main`
branch in four steps (`docs/template/inventory.md`, `t1`–`t4`), from the finished
prototype tagged `survive-stage-10`.

**The old name is kept here on purpose, and only here** (plus
[`examples/`](examples/), which keeps the game's own ADRs and notes as read-only
examples). Everywhere else the identity is yours:

- `tools/rename.ps1` is the one command that changes it — six spellings at once, in
  file contents, file names and folder names. Run it once, on a fresh copy, before the
  game's first commit:

  ```powershell
  pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To MyGameName -DryRun
  pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To MyGameName
  pwsh -NoProfile -File tools/gen.ps1
  ```

- This file and `docs/template/examples/**` are excluded from that rename (see the
  tool's header), so `git grep -i projectsurvive` in a renamed game finds exactly two
  things: this record of where the tree came from, and the examples. That is the test
  the rehearsal in [`rename-rehearsal.md`](rename-rehearsal.md) runs.

Nothing in `docs/`, `llmwiki/` or the code links into this file: it is provenance, not
documentation a game should follow. A game normally deletes `docs/template/` wholesale
once it has read the parts it wants.

# A docs link into git-ignored output passes locally and is broken in every clone

**What happens.** A document links a path that only exists because a tool wrote it on this
machine:

```markdown
Raw logs: [`artifacts/rename-rehearsal/`](../../artifacts/rename-rehearsal/)
```

`artifacts/` is git-ignored (`.gitignore:108`), so that directory is absent from every
clone, every CI checkout and every game that copies the tree. The author's `check-links`
run says `all resolve`, because on the author's machine the folder is right there. The
first person who reads the document anywhere else gets a dead link, and the gate that
exists to catch exactly this reported PASS.

**Same trap, other paths.** `UnityProject/Builds/**`, `UnityProject/Library/**`,
`tests/web/node_modules/**`, `tools/vendor/**`, any `obj`/`bin` output. The symptom is
identical: a link that resolves for whoever ran the build, and nowhere else — including
`docs/template/rename-rehearsal.md` at T5, which linked the rehearsal's own artifact
directory.

**Why it is easy to miss.** The path is real, the file listing confirms it, and the
"link check passed" line is right there. Nothing distinguishes "exists" from "exists in
the repository" unless the check asks git.

**How to tell.** Ask git, not the filesystem:

```bash
git check-ignore -v artifacts/rename-rehearsal   # names the rule that ignores it
git ls-files artifacts/rename-rehearsal          # empty: nothing tracked under it
```

**The rule.** A document in the live tree may *name* an ignored path in prose or code, and
must not *link* it: write `` `artifacts/rename-rehearsal/` `` instead of a markdown link.
Since T5 `tools/check-links.mjs` enforces that half — every target must be a path a clone
would have (a file git tracks, or an untracked file git would add; for a directory, at
least one such file under it) — so the mistake now fails the gate instead of waiting for a
clone. A planted probe link into an ignored directory exits 1.

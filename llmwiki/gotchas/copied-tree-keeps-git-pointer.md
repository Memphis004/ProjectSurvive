# A copied tree keeps git's pointer file — and the copy's git writes to the original

**What happens.** In the main repository `.git` is a directory, but in a **linked
worktree** it is a one-line *file*:

```text
gitdir: C:/Workspace/ProjectSurvive/.git/worktrees/ProjectSurvive-template
```

Every copy tool that excludes `.git` **by directory name** — `robocopy /XD .git`,
`xcopy /EXCLUDE`, a `cp -r` glob — happily copies that file, because it is not a
directory. The copy now looks like a fresh tree with no history, and it is not: git reads
the pointer, treats the copy as a worktree of the *original* repository, and every git
command run in the copy writes into the original's `.git`:

* `git add -A` **replaces the original's index** with the copy's file list.
* `git commit` **moves the original's branch** to a commit whose tree is the copy.

Seen at T5. The sweep copy was made with `robocopy /XD .git`, `git init` printed
`warning: re-init: ignored --initial-branch=main` (the tell), the sweep's commit landed on
`template/main` — and the original repo's `git status` listed every file as modified,
plus a rename of every `ProjectSurvive*` file to the copy's name. Nothing in the tree was
damaged: git had merely re-pointed its own bookkeeping at the copy.

**Why it looks like a code bug and is not.** The copy is a plain directory; the pointer is
a 75-byte file nobody opens; and there is no error. The damage only becomes visible the
next time someone runs `git status` in the *original* — which now describes a tree that is
not there.

**How to tell, before running git in a copy.**

```bash
cat .git                                                   # a "gitdir:" line means: not your repository
git rev-parse --git-dir                                    # a path outside the copy means the same
git init -b main .                                         # "re-init" in the output means STOP
```

**How to undo it, in the original.** The worktree files were never touched, so nothing is
lost: find the commit before the accidental one and reset the branch back.

```bash
git reflog -5                                              # e.g. 582b5b2 (sweep) on top of 3a5034a (real)
git reset --mixed <the real sha>                           # index and branch restored; uncommitted work stays
git status --short                                         # only the edits you meant to make
```

The stray commit object stays in the object store until gc; it is unreachable and
harmless.

**The rule.** A copy of the tree is not a copy of the repository. Delete the pointer
before any git command in it (`rm -f <copy>/.git`), or take the tracked files without
`.git` at all (`git archive HEAD | tar -x -C <copy>`, then copy `tools/vendor/` and run
`setup-tools.ps1`). `docs/template/baseline.md` §7 and the T4 rehearsal both say so now.

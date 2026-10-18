# A generator-owned folder can hold hand-written files

**Symptom.** After renaming the project, a fresh copy still has the old name in tracked
files under `lib/Runtime/Generated/` (two `.asmdef` files, in this template), and the
first Unity compile fails with an assembly that cannot be resolved — an error that
mentions the *new* name in every referencing `.asmdef` and the *old* name only in the
assembly that declares it.

**Cause.** The rename rule was "skip `lib/Runtime/Generated/**`, `gen.ps1` owns it".
`gen.ps1` owns the `*.cs` files there — it deletes and republishes them — but the
`.asmdef` files in that folder are hand-written and tracked, and nothing regenerates
them. Skipping the folder skipped them too, so the assembly kept its old name while
every reference to it changed.

**Fix (measured).** Exclude what the generator writes, not where it writes:
`lib/Runtime/Generated/**/*.cs`. The `.asmdef` and `.meta` files follow the rename like
any other hand-written file, and `git grep -i <old name>` then finds only the two places
the tool is told to leave alone (`docs/template/ORIGIN.md` and the examples archive).
The trap was found by the T4 rehearsal, in a throwaway copy, before any game was built
on it — see [`../../docs/template/rename-rehearsal.md`](../../docs/template/rename-rehearsal.md) §5.

**General rule.** "This folder is generated" is a claim about the files a tool writes,
which is checkable (`gen.ps1` writes `.cs`), and rarely about a whole directory. Any
exclusion, `.gitignore` entry or clean-up step that names a directory where a human has
also put something is a bug waiting for the next rename, delete or `git clean`.

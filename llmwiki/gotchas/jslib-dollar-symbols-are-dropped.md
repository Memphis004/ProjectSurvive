# A `$symbol: value` declaration in a Unity `.jslib` is dropped by the build

**Symptom.** The player boots, then halts on the first frame with
`$ProjectSurviveHarnessQueue is not defined` — a JavaScript `ReferenceError` raised from inside the
generated framework. Nothing in C# throws, `tools/build-web.ps1` reports `0 occurrence(s)` of C#
errors, and the failure is only visible in the browser console (`[pageerror]`, then
`Halting program`).

**Cause.** Unity 6.3's jsifier does not emit an initialiser for a library symbol declared as a
`$`-prefixed member of the `mergeInto(LibraryManager.library, { ... })` object:

```js
$ProjectSurviveHarnessQueue: [],   // ← listed in framework.js `unexportedSymbols`, never defined
```

The generated `Web.framework.js` contains nine references to the name and no `var` or assignment
for it. The symbol still appears in the framework's `unexportedSymbols` list, which makes the build
look like it did the right thing.

**Why an existing symbol appears to work.** `$ProjectSurviveFlushState` (Stage 12) is also never
declared in the output. It works because every read happens *after* an assignment
(`$ProjectSurviveFlushState = 1;`), and the framework is not in strict mode, so the assignment
creates an implicit global. A symbol that is **read before anything assigns it** — an array, a
counter initialised by code — throws instead.

**Fix.** Do not rely on the `$symbol: value` form. Initialise the state from the function that
first needs it, behind a `typeof` guard, and keep every later function behind that initialiser:

```js
ProjectSurvive_HarnessEnable: function (sendNamePtr) {
  if (typeof $ProjectSurviveHarnessQueue === 'undefined') {
    $ProjectSurviveHarnessQueue = [];   // sloppy mode: creates the global
  }
  ...
}
```

`typeof` is the one operator that is safe on an undeclared identifier, so the guard itself cannot
throw. Measured on the Stage 14 Development build: the first build halted in `PlayerLoop` with the
`ReferenceError`; after the guard the same harness published `ready:true` and the browser spec
advanced floor 1 to `Won` in 5.2 s.

**How to check a build for it.** Search the generated framework for the symbol and then for an
initialiser:

```powershell
python - <<'EOF'
s = open("UnityProject/Builds/Web/Build/Web.framework.js", encoding="utf-8", errors="replace").read()
print(s.count("$ProjectSurviveHarnessQueue"))      # references
print("var/first assignment?", "ProjectSurviveHarnessQueue =" in s)
EOF
```

A non-zero reference count with no assignment is the bug.

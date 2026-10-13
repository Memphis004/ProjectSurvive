# Gotcha — `TryAddCharacters`' "missing" list does not mean the glyphs are missing

`TMP_FontAsset.TryAddCharacters(string characters, out string missing)` (and
TextCore's `FontAsset.TryAddCharacters`) returns `false` and reports the **entire
input** as missing in two situations that have nothing to do with glyph coverage.

## 1. The font face failed to load

Verified in `TMP_FontAsset.cs`:

```csharp
if (LoadFontFace() != FontEngineError.Success)
{
    missingUnicodes = unicodes.ToArray();   // every code point, "missing"
    return false;
}
```

`LoadFontFace` fails when the source font is not ready (a fresh asset whose font is
still importing) or when the face is already resident, which is the normal case for
an asset that has been loaded and baked before. The glyphs may all be sitting in the
atlas; the return value still says "all missing".

**Measured, Stage 03:** the same call on an already-populated asset reported all 182
characters missing while the atlas in fact held 183 glyphs (verified by reading the
`m_Unicode` entries straight out of the `.asset`).

## 2. The asset is Static

`TryAddCharacters` refuses to add to a Static asset — it logs
`Unable to add characters to font asset [...] because its AtlasPopulationMode is
set to Static` and returns `false` with `missingUnicodes = null`. A `false` return
with a **null** missing list therefore means "wrong mode", and a retry loop that
only checks `returned false` will spin on a Static asset forever.

## Rule

- Use `TryAddCharacters` only to **populate** (and see
  [`tmp-font-assets-in-builds.md`](tmp-font-assets-in-builds.md) for the
  bake-then-freeze sequence).
- To **ask about coverage**, use `HasCharacter`/`HasCharacters` on an asset whose
  tables are the ones that ship — i.e. after it has been frozen to Static and
  verified. `characterTable.Count` is the primary fact; the boolean is a summary.
- Never let a bool from `TryAddCharacters` be the only evidence in a report or a
  test: read the character table back (the Thai spike publishes per-string missing
  counts from `HasCharacter`, and the browser test asserts on those numbers).

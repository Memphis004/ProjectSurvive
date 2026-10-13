# Gotcha — a Dynamic TMP/TextCore font asset loses its baked tables at build time

**Found:** Stage 03, after the second Web build. The Editor said `missingGlyphs=0`;
the shipped player reported `70` missing glyphs across six strings, and the
on-disk font asset had `m_GlyphTable: []` and `m_CharacterTable: []`.

## Cause

`TMP_PreBuildProcessor` (in `com.unity.ugui`, `Editor/TMP/`) runs on every build:

```csharp
if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic ||
    fontAsset.atlasPopulationMode == AtlasPopulationMode.DynamicOS)
{
    if (fontAsset.clearDynamicDataOnBuild && fontAsset.atlasTexture.width != 0)
        fontAsset.ClearCharacterAndGlyphTablesInternal();   // on the PROJECT asset
}
```

`clearDynamicDataOnBuild` defaults to true for assets created through
`TMP_FontAsset.CreateFontAsset`, and the clear is **written back to the asset in
`Assets/`** — it is not undone after the build. So:

- an offline bake on a Dynamic asset is destroyed by the next build, silently;
- after the first build the asset is empty both in the player and in the Editor;
- the intent of the default is "the player will rasterise glyphs on demand from the
  font file", which makes coverage unprovable — `HasCharacter` in the player then
  answers *"is this glyph already rasterised?"*, not *"can the shipped font draw
  it?"*.

## Rule

**Bake, then freeze the asset to Static.**

```csharp
font.atlasPopulationMode = AtlasPopulationMode.Dynamic;  // TryAddCharacters refuses to run on Static
font.ReadFontAssetDefinition();
font.ClearFontAssetData();                               // deterministic: never depend on prior state
font.TryAddCharacters(requiredCharacters, out string missing);
font.atlasPopulationMode = AtlasPopulationMode.Static;   // ships the tables; the pre-build processor skips Static
```

Then **verify what landed**, not what the call claimed:
`ReadFontAssetDefinition()` → `characterTable.Count` → `HasCharacters(requiredSet)`,
and throw if the set is not covered. `ProjectSetup.BakeGlyphCoverage` does exactly
this and writes `artifacts/thai-font-bake.json`.

Notes that cost time:

- `TMPro.AtlasPopulationMode` and `UnityEngine.TextCore.Text.AtlasPopulationMode`
  are **two different enums**; using both namespaces in one file needs
  fully-qualified names (`CS0104: ambiguous reference`).
- A successful `TryAddCharacters` on TextCore leaves its `out missing` parameter
  `null` (it does not hand back an empty list), so "missing null" is not an error.
- Static assets do not rasterise new code points at runtime: the required set must
  be decided at build time. Ours is printable ASCII plus the **assigned** Thai code
  points (`U+0E01..U+0E3A`, `U+0E3F..U+0E5B`) — 182 characters. Adding a script
  means extending `ProjectSetup.BuildBakedCharacterSet()` and re-running the menu
  item.

## See also

- [`tmp-tryaddcharacters-lies.md`](tmp-tryaddcharacters-lies.md) — why
  `TryAddCharacters`' return value cannot be trusted on an already-populated asset
- [`il2cpp-di-and-reflection.md`](il2cpp-di-and-reflection.md) — the other
  Editor-green / player-broken failure from the same stage

# Gotcha — `.unity` YAML is not hand-editable

A scene is a graph of `fileID`s and GUIDs: component references store the local file
ID of another object in the same file, prefab links store a GUID plus a file ID, and
the `m_Children` order of a `RectTransform` is the draw order. Editing the YAML by
hand produces a file that opens, looks nearly right, and loses a reference the first
time Unity reserializes it — or a merge conflict nobody can resolve.

**Create scenes through the Unity Editor API** — a menu item or an `-executeMethod`
entry point that calls `EditorSceneManager` — never by writing YAML. The template
does this with one builder per scene under
`UnityProject/Assets/Main/Scripts/Editor/` (`BootSceneBuilder`, …), each with its own
menu path.

**Tolerate the regeneration diff:** re-running a builder creates fresh `fileID`s, so
the scene shows a large harmless diff. Either commit it with a note that says so, or
make the builder patch an existing scene instead of recreating it.

**Never** hand-merge a scene conflict. Re-run the builder on the intended state and
commit its output — that is the only version of the file that is guaranteed to be
internally consistent.

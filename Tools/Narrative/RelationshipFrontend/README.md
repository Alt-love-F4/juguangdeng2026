# Narrative Relationship Frontend

This is a standalone, read-only browser view of the generated NCS configuration.
It reads the official Luban JSON files under `Assets/Settings/Resources/Luban/`;
it does not start `NarrativeRuntime`, evaluate live conditions, execute results,
or write saves.

From the repository root, start a local static server:

```powershell
python -m http.server 8765 --bind 127.0.0.1
```

Then open:

```text
http://127.0.0.1:8765/Tools/Narrative/RelationshipFrontend/
```

Use the page's **重读** command after the generated tables change. If the source
workbooks changed, run the existing official Luban generation pipeline first.
The page builds a display-only relationship index from those generated tables;
condition, result-owner, and edge semantics should stay aligned with
`Assets/Scripts/Narrative/Graph/NarrativeRelationshipGraph.cs`.

Dates are layout hints, not causal edges. A missing table producer is not proof
that a condition has no runtime provider. Configured, reachable, and naturally
playtested content remain separate states.

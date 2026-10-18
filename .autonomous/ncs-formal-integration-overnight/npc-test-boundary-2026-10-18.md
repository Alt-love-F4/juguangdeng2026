# NPC Test Narrative Boundary Evidence

Date: 2026-10-18
Scope: Task 9 of `ncs-formal-integration-overnight`
Status: Verified. No project asset, workbook, generated-data file, scene, runtime source, or unrelated test artifact was deleted or modified by this task.

## Invariant

The removed `npc_a`, `npc_b`, `npc_c`, and `npc_d` test narratives must not be restored, copied into formal content, or used as formal triggers. Existing legacy/manual references are preserved unless an explicit removal requirement identifies them as removable test junk.

## Authoritative source and generated-data checks

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`: zero `npc_a` through `npc_d` matches across every sheet.
- `Assets/ConfigTables/Data/Shared/Text.xlsx`: zero `npc_a` through `npc_d` matches across the Text sheet.
- `Assets/Settings/Resources/Luban/tbncsobject.json`: 45 objects, zero `npc_a` through `npc_d` IDs.
- `Assets/Settings/Resources/Luban/tbncsevent.json`: 38 events, zero NPC IDs in event IDs or serialized event rows.
- `Assets/Settings/Resources/Luban/tbncstrigger.json`: one trigger, zero NPC IDs in trigger IDs or event IDs.
- A wider scan of the generated narrative JSON files found no `npc_a` through `npc_d` references.
- A scan of `Assets/Scripts` found no exact `npc_a` through `npc_d` references. The only similarly prefixed code fixture is `npc_chen.trust`, which is unrelated to the removed A/B/C/D narratives.

Generated hashes captured during this verification:

```text
AE31B8D7DD1CB2590A67393557F4E42804F0BE889BEC45FDE05DB06DBE0D2935  Assets/Settings/Resources/Luban/tbncsobject.json
53709938E97091A62B8DEA5818AAED4A8532E588BDC8160AF4BEEFB400767544  Assets/Settings/Resources/Luban/tbncsevent.json
074ED94516D6F78794EF61A14A377F9B71F6DCC0FA4845E444267097461929B5  Assets/Settings/Resources/Luban/tbncstrigger.json
```

## Runtime verification

Unity `6000.3.5f2` loaded the current Luban configuration successfully. The evaluated configuration reported:

```text
objects=45;npcObjects=0;npcEvents=0;npcTriggers=0;npcSpeakers=0;events=38;triggers=1
```

The runtime character loader in `Assets/Scripts/Narrative/CharacterCatalog.cs` constructs its catalog from `LubanNarrativeLoader.Load()` and `config.objects`; it does not read the manual `Assets/Resources/Characters/CharacterCatalog.asset` as an authoritative source.

## Remaining non-authoritative references

The following tracked assets still contain the old IDs and were intentionally not deleted:

- `Assets/Resources/Characters/CharacterCatalog.asset`: legacy/manual snapshot asset with four old test profiles.
- `Assets/Resources/DetailsDisplayConfig.asset`: legacy/manual details configuration with old test profiles and bars.

Their presence does not restore a narrative or formal trigger because neither is the Luban source or the runtime table loader. Their current hashes are:

```text
722A23D1CD1844296351A8FA31AF831ED4381C298F8B77C5B5674C9388A6768E  Assets/Resources/Characters/CharacterCatalog.asset
09F40602E28339B90AFC156F07704A54CA1E05110FF583761C08D0BF7DDB6E41  Assets/Resources/DetailsDisplayConfig.asset
```

Documentation also retains historical boundary references, notably `Docs/RuntimeCharacters.md` and `Docs/NcsFormalDependencies.md`. These references describe the old test-data boundary; they are not generated narrative rows or active formal routes. No source/config rewrite was warranted for this verification task.

## Formal-trigger check

The formal event inventory and queue contain no `npc_a` through `npc_d` event, trigger, action, speaker, or prerequisite. The only retained Revolution and formal story IDs are independent of the removed test narratives. No `npc_a` through `npc_d` definition was restored, copied, or enabled as part of the current worktree changes.

## Boundary

This evidence proves the authoritative source, generated data, runtime loader, and formal trigger boundary. It does not claim that every historical manual asset or documentation reference has been purged; those were intentionally preserved as unrelated legacy/test artifacts pending an explicit cleanup requirement.

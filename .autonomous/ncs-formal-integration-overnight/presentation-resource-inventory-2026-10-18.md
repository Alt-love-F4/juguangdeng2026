# Formal NCS Presentation Resource Inventory

Date: 2026-10-18
Scope: Task 7 of `ncs-formal-integration-overnight`
Status: Static inventory plus connected-Unity resource-resolution check. No project asset, workbook, generated file, scene, or runtime code was deliberately changed by this task.

## Evidence sources

- Formal root set: `event-inventory-2026-10-18.md` and `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv`.
- Generated presentation data: `Assets/Settings/Resources/Luban/tbncspresentationindex.json`, `tbncsconversationpage.json`, `tbncsconversationline.json`, `tbncsreadingpage.json`, `tbncsreadingline.json`, `tbncsobject.json`, and `tbtext.json`.
- Runtime presentation ownership: `Assets/Scripts/Narrative/NarrativePresenter.cs`, `Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs`, `NarrativeStageVisuals.cs`, `ReadingEventPlayback.cs`, and `LetterEventPlayback.cs`.
- Presentation assets: `Assets/Resources/Dialogue/NarrativeStageVisuals.asset`, `CafeSceneVisuals.asset`, `PassiveCheckPresentation.asset`, `PresentationEffectLibrary.asset`, `Assets/Resources/Art/UIPrefab/DialoguePanel.prefab`, `ReadingPanel.prefab`, and `FirstDayLetterPanel.prefab`.
- Character source and runtime binding: `tbncsobject.json`, `Assets/Scripts/Narrative/CharacterCatalog.cs`, `Assets/Scripts/GameFlow/UI/Presentation/NarrativeCharacterResolver.cs`, and `Assets/Resources/Characters/*`.

## Connected Unity verification

Connected instance: Unity `6000.3.5f2`, instance `聚光灯2026@bdd6223993d7aadf`.

The following checks were executed through the connected Editor using `Resources.Load`, `NarrativeStageVisuals.Resolve`, and `CharacterCatalog.LoadFromTables`:

- `Dialogue/NarrativeStageVisuals` loaded successfully.
- `scene.cafe` resolved with `useSceneBackground=true`, no texture, no solid color, and no placeholder flag.
- `oct07.cafe.day`, `oct07.cafe.night`, `oct07.street.day`, `neutral.market.day`, `neutral.market.shop.day`, `rose.club.exterior`, `rose.club.reception`, and `church.cathedral.day` resolved to textures.
- `rose.club.blindfold` resolved as an explicit solid-color entry; it is not a missing-resource fallback.
- `rose.club.knight_guidance` resolved as an explicit placeholder entry with no texture.
- Runtime table loading found every formal speaker object. Real portrait/avatar/world sprites were present for `adele`, `colette`, `sparrow`, and `sylvain`; the other formal speaker IDs listed below were found but have no configured portrait resource.
- The `revolution.1014.s1` context override is present. Its source key `oct07.cafe.day` resolves to the `cafe.dawn` texture for the `revolution.1014.s1.*` context while retaining the requested key in the snapshot.

The focused lifecycle verification immediately before this inventory also passed `Spotlight.Narrative.Tests.NarrativeEntryTests`: 14 passed, 0 failed, 0 skipped.

The focused `NarrativeBackgroundAssetsTests` run was not counted as evidence: one attempt was rejected while the Editor was entering Play Mode, and a retry remained uninitialized while a separate PlayMode test job was running. That concurrent job reported three unrelated existing MainMenu failures (`FormalMenuRetainsAllFourFlowBindingsAndPsdArtwork`, `MenuLayoutIsFiniteAndCoversCanvasWithoutScreenMotion`, and `AmbienceKeepsBackgroundInsideItsOverscanMargin`). The Editor was stopped afterward. Any `InitTestScene*` or screenshot artifacts produced by the existing test harness remain preserved under the worktree boundary; they were not deleted or rewritten here.

## Shared presentation resource set

| Resource | Role | State |
|---|---|---|
| `Dialogue/NarrativeStageVisuals` | Stable background-key registry, aliases, and context overrides | Loaded; formal keys resolve as classified below |
| `Art/UIPrefab/DialoguePanel` | Conversation presentation | Present |
| `Art/UIPrefab/ReadingPanel` | Generic reading presentation | Present |
| `Art/UIPrefab/FirstDayLetterPanel` | Letter reveal/read presentation used by NCS letter steps | Present and reused; not a parallel story system |
| `Dialogue/PassiveCheckPresentation` | Passive-check hint words | Present; fixed word exists for `oct07.s2.perception` |
| `Dialogue/PresentationEffectLibrary` | Portrait, shake, flash, camera/zoom, and transition effect presets | Present |
| `Dialogue/CafeSceneVisuals` | Opening/debug cafe scene visual asset | Present; `backgroundKey=scene.cafe`; it is not a formal-root image background |
| `Characters/*` | Runtime sprites loaded from NCS object paths | Present for authored character IDs with paths; missing paths remain null rather than invented |

## Formal-root inventory

Background paths below are the authored `ConversationPage.bgOverride` values after inheritance. Blank page values inherit the most recent non-empty key, as implemented by `NarrativePresenter.ConversationPages`.

| Root | Conversation data | Resolved backgrounds and source assets | Reading data | Character resources and known gaps |
|---|---|---|---|---|
| `oct07.s1` | `oct07.s1.conversation`; 2 pages / 40 lines | `oct07.cafe.day` -> `Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png`; `oct07.street.day` -> `Assets/Art/Background/game/SCE_圣徒街场景1.png` | None | `adele` has real portrait/avatar/world sprites. `player` and `oct07.passerby.a` are offstage (`side=none`) and have no portrait resource. |
| `oct07.s2` | `oct07.s2.conversation`; 7 pages / 34 lines; passive check `oct07.s2.perception` | `oct07.cafe.night` -> `Assets/Art/Background/game/SCE_一楼大厅场景夜晚.png` | `oct07.s2.letter`; 1 reading page, 7 reading lines (`oct07.s2.p024` through `p030`); title key `oct07.s2.p023`; `imageKey` is empty | Conversation speaker `player` is offstage and has no portrait. Reading lines are narration and use no portrait/image resource. |
| `neutral.1008.s1` | `neutral.1008.s1`; 9 pages / 60 lines; choice/check branch `neutral.1008.s1.q1b` | `neutral.market.day` -> `Assets/Art/Background/game/SCE_大堂区露天市场场景日景.png`; `neutral.market.shop.day` -> `Assets/Art/Background/game/SCE_大堂区杂货铺内部场景日景.png` | None | `colette` has real portrait/avatar/world sprites. `neutral.1008.shopkeeper`, `neutral.1008.unknown`, and `player` are offstage and have no portrait resource. |
| `rose.1009.s1` | `rose.1009.s1.conversation`; 9 pages / 67 lines; passive check `rose.1009.s1.perception` | `rose.club.exterior` -> `Assets/Art/Background/game/SCE_布塔克俱乐部门口场景底图.png`; `rose.club.reception` -> `Assets/Art/Background/game/SCE_布塔克俱乐部会客室场景底图.png`; `rose.club.blindfold` -> explicit solid black; `rose.club.knight_guidance` -> explicit placeholder with no texture | None | `rose.butake`, `rose.envoy`, and `player` are all offstage and have no portrait resource. `rose.club.knight_guidance` is the known visual placeholder for the perception-success page; it must remain explicit until authoritative art exists. |
| `rose.1011.s1` | `rose.1011.s1.conversation`; 27 pages / 123 lines; checks `sparrow`, `portrait`, and authored `wisdom` choice conditions | `oct07.cafe.day` and `cafe.day` both resolve to `Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png` | `rose.1011.s1.card`; 1 reading page, 3 reading lines (`p128`-`p130`); `imageKey` is empty; text keys exist | `adele`, `sparrow`, and `sylvain` have real portrait/avatar/world sprites. The generated conversation rows currently place all five speaker IDs (`adele`, `sparrow`, `sylvain`, `player`, and narration) at `side=none`, so these real portraits are not currently used as left/right/center dialogue slots by the authored rows. `player` has no portrait resource. |
| `church.1009.s2` | `church.1009.s2.conversation`; 4 pages / 36 lines; two choices | `church.cathedral.day` -> `Assets/Art/Background/game/SCE_圣卷大教堂场景日景.png` | None | `church.broadcast`, `church.crowd`, `church.duke`, and `church.man` are offstage and have no portrait resource. |
| `revolution.1013.s2` | `revolution.1013.s2.conversation`; 6 pages / 52 lines; 3 choices | `oct07.cafe.night` -> `Assets/Art/Background/game/SCE_一楼大厅场景夜晚.png` | None | `adele` has a real portrait/avatar/world sprite. `player` is offstage and has no portrait resource. |
| `revolution.1014.s1` | `revolution.1014.s1.conversation`; 23 pages / 171 lines; checks `wisdom4`, `perception6`, and `wisdom5`; dependency-gated page is present | Authored key is `oct07.cafe.day`; context override for `revolution.1014.s1.*` selects `cafe.dawn` -> `Assets/Art/Background/game/SCE_一楼大厅场景清晨.png` | `revolution.1014.s1.letter`; 1 reading page, 1 reading line (`ncs.revolution.1014.s1.p017`); `imageKey` and title key are empty; text key exists | `sorel` is a right-side speaker with emotes (`neutral`, `thinking`, `happy`, `confused`, `excited`, `embarrassed`) but `tbncsobject.json` has no portrait path and tags it `C-R04-missing-portrait`; all expression requests therefore have no real sprite and use the configured fallback path (which remains null). `player` is offstage and has no portrait resource. |
| `revolution.1028.s1` | `revolution.1028.s1.conversation`; 10 pages / 144 lines; 3 choices; `C-B06.history-specialization` dependency page remains authored | `oct07.cafe.night` -> `Assets/Art/Background/game/SCE_一楼大厅场景夜晚.png` | None | `adele` has a real portrait/avatar/world sprite. `player` is offstage and has no portrait resource. |

## Resource boundaries and placeholders

- `scene.cafe` is a real-scene marker, not a texture, black screen, or missing-resource fallback. Its registry entry deliberately disables the panel-owned background layer.
- The current formal roots use dialogue-composition textures, mapped location textures, the intentional black blindfold, or the real-scene marker. They do not use the operating base images `SCE_一楼大厅场景底图.png`, `SCE_二楼会客室场景底图.png`, or `SCE_ 三楼宿舍场景底图.png` as dialogue backgrounds.
- The special composite `cafe.door.thibaut` remains registered and resolves to `SCE_咖啡馆闭门场景蒂博剪影.png`, but no current formal root page references it. It must not be bulk-replaced by `scene.cafe` when later special cafe content is connected.
- `rose.club.blindfold` is an authored black composition and is not classified as missing art.
- `rose.club.knight_guidance` is the only explicit background placeholder used by a current formal root.
- Missing portrait paths are data gaps, not permission to borrow a different character identity or restore removed `npc_a` through `npc_d` content. The runtime catalog is built from `tbncsobject.json`; the legacy/manual `Assets/Resources/Characters/CharacterCatalog.asset` is not the authoritative loader source.
- No new raster art, fallback portrait, parallel presentation field, or generated JSON was added by this task.

## Acceptance boundary

This inventory proves current resource registration and connected-Unity resolution. It does not prove visual correctness in the real Map -> CoffeeShop -> presentation path, scene loading, cancellation, retry, save/restore, or formal settlement. Those remain ordered lifecycle and event-acceptance tasks. The placeholder and missing-portrait rows above must remain visible in later acceptance reports rather than being counted as completed art integration.

# Shared Presentation Debug Scene Verification

Captured: 2026-10-18 (Executor session 2, Task 5)

## Scene and ownership

- Scene: `Assets/Scenes/PresentationDebugScene.unity`
- File present: 418,686 bytes; last write `2026-10-16 18:41:33`.
- Builder: `Assets/Editor/GameFlow/PresentationDebugSceneBuilder.cs`.
- Runtime controller: `Assets/Scripts/GameFlow/UI/Presentation/PresentationDebugSceneController.cs`.
- Controller constants: `DebugTag = presentation-debug`; `ScenePath = Assets/Scenes/PresentationDebugScene.unity`.
- The builder's `Open`, `CreateMissing`, `Rebuild`, and `UpgradeLayout` menu paths all target the shared scene. No replacement scene was created.

## Connected Editor load check

`open_scene(path=Assets/Scenes/PresentationDebugScene.unity)` succeeded. The connected Editor scene snapshot reported:

- Scene name `PresentationDebugScene`, active and loaded, `isDirty=false`.
- Root `DebugCamera` with `Camera` and `UniversalAdditionalCameraData`.
- Root `EventSystem` with `EventSystem` and `InputSystemUIInputModule`.
- Root `DebugRoomCanvas` with `Canvas`, `CanvasScaler`, `GraphicRaycaster`, and `PresentationDebugSceneController`; children include `Background`, `Stage`, `Title`, `Description`, and `AgentNotice`.
- Root `DebugHelpOverlay` with `Canvas`, `CanvasScaler`, `GraphicRaycaster`, and `CanvasGroup`; child `ShortcutSurface` contains the scrollable help content.

The serialized scene contains the expected sorting orders: `DebugRoomCanvas=50`, `DebugHelpOverlay=1000`, and the nested help surface canvas `=60`. The runtime controller's `Update` method uses the Input System `Keyboard.current` and guards all shortcuts behind `enableKeyboardShortcuts` and external-input blocking.

## Debug inputs verified in source

- Shared configured chapters: `F1` oct07 S1, `F2`/`F3` oct07 S2 low/high perception, `F4` perception node, `F5` letter.
- General previews: `Q` date change, `W` letter, `E` dialogue menu, `C` options, `V` reading, `B` notice, `N` cutscene, `R` pre-business, `T` cafe opening, `Y` business end, `U` city map, `I` pre-night, `O` home night, `P` day end, `F` introduction, `Z` prologue, `G` effects, `L` long loading, `K` short loading, `M` details.
- Global controls: `Esc` close, `Backspace` replay, `H` hide help, `F9` time pause, `F12` restore help, `1`-`0` effect cues, and `Space`/`Enter` continuation through the active panel.
- The controller serializes `openingPresentationId=first_day_cafe`, `letterBodyKey=opening.letter.body`, and `letterSignatureKey=opening.letter.signature` for read-only NCS preview.

## Shared presentation prefabs

The controller's panel-open paths resolve all required prefabs through the existing UIKit/Resources path. The following 16 assets are present under `Assets/Resources/Art/UIPrefab/`:

`DialoguePanel`, `FirstDayLetterPanel`, `ReadingPanel`, `NoticePanel`, `CutScenePanel`, `PreBusinessPanel`, `CafeOpeningPanel`, `BusinessEndPanel`, `CityMapPanel`, `PreNightPanel`, `HomeNightPanel`, `DayEndPanel`, `LoadingPanel`, `ShortLoadingPanel`, `DetailsPanel`, and `OpeningProloguePanel`.

## Test and build boundary

- `PresentationDebugSceneTests.cs` and `PresentationDebugSequenceTests.cs` load `PresentationDebugSceneController.ScenePath` directly; they do not create a replacement presentation scene.
- The scene is present on disk but absent from formal Build Settings. Current enabled build scenes remain the startup scene, `CoffeeShop`, and `Map`.
- This task only opened the shared scene and read its hierarchy/source bindings. It did not rebuild, save, add the scene to Build Settings, create a Canvas, or create an input system.

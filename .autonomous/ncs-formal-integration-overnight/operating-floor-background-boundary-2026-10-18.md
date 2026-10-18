# Task 27: Operating-Floor Base Image Boundary

Date: 2026-10-18

## Forbidden assets

The three operating-floor source images are preserved under `Assets/Art/Background/game/`:

- `SCE_一楼大厅场景底图.png` (`c4f85c5dc81c18241bf191de175c6f92`)
- `SCE_二楼会客室场景底图.png` (`83834550e0fbe6943875adfd8fe80e4f`)
- `SCE_ 三楼宿舍场景底图.png` (`fccab2da4dae43144b95d7d363ca80d7`)

They are used by the operating `CoffeeShop` scene. The scene also contains the no-chair first-floor operating variant, which is separate from the dialogue-background rule.

## Source and generated data

The authoritative `Narrative.xlsx` workbook contains no occurrence of the three forbidden base-image names in its narrative cells. The generated `tbncsconversationpage.json` contains 112 pages with zero forbidden base-image references. The generated `tbopeningpresentation.json` contains zero forbidden base-image references.

The `ConversationPage.bgOverride` audit reports:

- `forbiddenConversationPages=0`
- `scene.cafe` remains the ordinary in-cafe route.
- Complete dialogue-scene textures remain available through their own registered keys.

## DialogueVisualsAsset audit

Unity `AssetDatabase.FindAssets("t:Spotlight.GameFlow.DialogueVisualsAsset")` found 3 assets. Two declare a texture background:

- `Assets/Resources/Dialogue/DialoguePreview.asset` -> `Assets/Resources/Art/Gameplay/DayFlow/phase_business_transition.png`
- `Assets/Resources/Dialogue/FirstDayIntroductionVisuals.asset` -> `Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png`

`Assets/Resources/Dialogue/CafeSceneVisuals.asset` has no texture background and uses `backgroundKey=scene.cafe`. None of the three `DialogueVisualsAsset.background` values points at an operating-floor base image.

## Registry alias audit

The existing `NarrativeStageVisuals` aliases for the three source filenames resolve safely to complete dialogue-scene textures:

- first floor base filename -> `SCE_一楼大厅对话场景日景.png`
- second floor base filename -> `SCE_二楼会客室对话场景日景.png`
- third floor base filename -> `SCE_三楼宿舍对话场景日景.png`

The aliases do not expose the base-image GUIDs as dialogue textures. The base files remain available for the actual operating scene and were not deleted, moved, or overwritten.

## Direct Unity result

The connected Unity `6000.3.5f2` Editor direct contract check passed:

`conversationPages=112;forbiddenConversationPages=0;dialogueVisualsAssets=3;dialogueBackgrounds=2;backgroundPaths=Assets/Resources/Dialogue/DialoguePreview.asset -> Assets/Resources/Art/Gameplay/DayFlow/phase_business_transition.png | Assets/Resources/Dialogue/FirstDayIntroductionVisuals.asset -> Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png;safeRegistryAliases=SCE_一楼大厅场景底图.png -> Assets/Art/Background/game/SCE_一楼大厅对话场景日景.png | SCE_二楼会客室场景底图.png -> Assets/Art/Background/game/SCE_二楼会客室对话场景日景.png | SCE_ 三楼宿舍场景底图.png -> Assets/Art/Background/game/SCE_三楼宿舍对话场景日景.png`

No source workbook, generated data, scene, Canvas, input system, dialogue texture, or user save slot was changed by this task.

Status: Task 27 evidence complete; Task 28 remains open.
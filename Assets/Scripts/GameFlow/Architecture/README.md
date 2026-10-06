# GameFlow Architecture

This folder is the single YokiFrame Architecture composition root for shared project services.

## Rules

- Register one shared service type in `SpotlightArchitecture.OnInit()`.
- Prefer an existing YokiFrame Kit directly for framework infrastructure: SceneKit for scene flow, UIKit for panels, EventKit for cross-module notifications, AudioKit for audio, ResKit for resources, and SaveKit for persistence.
- Do not add `SceneManager`, `AudioManager`, `UIManager`, `EventManager`, or parallel resource/pool/state-machine infrastructure here.
- Scene-owned MonoBehaviours remain owned by their scene or by `GameFlowBootstrap`; they are not hidden global services.
- Services must have explicit initialization and disposal paths. A service must query dependencies from the architecture instead of constructing other services.
- Feature modules should depend on a service contract or existing GameFlow hook, not on another feature's concrete scene object.

## Current registrations

- `GameSettingsServiceAdapter`: shared settings facade.
- `GameSaveServiceAdapter`: shared save facade.

The adapters preserve the existing testable facades while giving the project one YokiFrame-owned registration point. `GameFlowBootstrap` is the only owner of the current loading transition and scene activation flow.

# Core Narrative Runtime Fixes - 2026-10-18

This note records the two runtime defects found while taking over the NCS formal-integration run.

## Changes

- `NarrativeCandidateValue.conditionsFrozen` records that an eligible candidate has passed its condition gate when it enters the playback queue.
- Playback still rechecks construction blockers, event locks, once/completion markers, trigger once state, held-value state, date validity, and phase routing. Only the already-passed event condition is frozen.
- `NarrativeDebugSession.SubmitDate` now mirrors the formal host contract: after scheduling a new official day it enters `MorningBeforeMap`, so date rows can be observed by the isolated debug session without changing production DayFlow behavior.

## Evidence

- Targeted EditMode tests: `2/2` passed.
- `Spotlight.Narrative.Core.Tests`: `195/195` passed.
- The date helper was also reproduced directly in the connected Unity Editor; `SubmitDate(2)` produced the expected `date` request after restoring the baseline.

## Scope boundary

This fix does not add story text, invent relationship or economy values, or alter the Revolution preservation boundary. It only repairs queue lifecycle semantics and the existing debug scheduling helper.

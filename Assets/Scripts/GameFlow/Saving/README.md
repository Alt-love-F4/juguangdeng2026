# Game Save Service

`Spotlight.GameFlow.GameSaveService` owns six SaveKit slots:

- Slot `0` is autosave. It can be loaded and listed, but only `TryAutoSave` may write it and it cannot be deleted through this facade.
- Slots `1` through `5` are manual slots. `TrySaveManual` is an explicit replacement operation and may overwrite an occupied or corrupt file; the caller should confirm replacement in UI first.
- `FindFirstEmptyManualSlot` uses physical storage occupancy, so corrupt files are not mistaken for empty slots. A quick-save flow should find this slot and then call `TrySaveManual`; it must not fall back to replacement.

`GameSaveSlotInfo.State` distinguishes `Empty`, valid `Occupied`, `Corrupt`, serializer `Incompatible`, and `Unavailable`. A corrupt or incompatible slot remains occupied and is never silently converted into a missing save. `TryLoad` returns the SaveKit failure message through `error` and does not return partial data.

The default root is `<Application.persistentDataPath>/Spotlight/Saves`. Tests and profiles can inject `MemorySaveStorage` or a `FileSaveStorage` root. Each operation rebinds SaveKit to this service's storage and Unity JSON serializer, so do not concurrently use multiple service instances with different backends through the process-global SaveKit facade.

`GameSaveData` is deliberately plain and JSON-serializable. Its `Participants` list is an extension point for gameplay systems that own stable participant IDs and JSON payloads; the save service does not interpret those payloads.

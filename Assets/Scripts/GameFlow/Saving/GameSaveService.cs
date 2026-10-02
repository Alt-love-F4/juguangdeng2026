using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Six-slot game save facade. Slot 0 is autosave-only; slots 1-5 are manual saves.
    /// The service uses SaveKit's atomic FileSaveStorage and strict load result handling.
    /// </summary>
    public sealed class GameSaveService
    {
        public const int SlotCount = 6;
        public const int AutoSaveSlot = 0;
        public const int FirstManualSlot = 1;
        public const int LastManualSlot = SlotCount - 1;
        public const string ModuleId = "spotlight.game";
        private const int CurrentDataVersion = 1;

        private readonly object mSyncRoot = new object();
        private readonly ISaveStorage mStorage;
        private readonly ISaveSerializer mSerializer;

        /// <summary>Creates a file-backed service under the current platform's persistent data directory.</summary>
        public GameSaveService()
            : this(null, null)
        {
        }

        /// <summary>Creates a file-backed service at a caller-provided root, useful for profiles or tests.</summary>
        public GameSaveService(string rootPath)
            : this(string.IsNullOrWhiteSpace(rootPath) ? null : (ISaveStorage)new FileSaveStorage(rootPath), null)
        {
        }

        /// <summary>Creates a service with an injected SaveKit storage backend.</summary>
        public GameSaveService(ISaveStorage storage)
            : this(storage, null)
        {
        }

        private GameSaveService(ISaveStorage storage, ISaveSerializer serializer)
        {
            mStorage = storage ?? new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Spotlight", "Saves"));
            mSerializer = serializer ?? new JsonSaveSerializer(new UnityJsonSaveCodec(), CurrentDataVersion);
        }

        /// <summary>Returns snapshots for all six configured slots in index order.</summary>
        public IReadOnlyList<GameSaveSlotInfo> List()
        {
            lock (mSyncRoot)
            {
                ConfigureSaveKit();
                var slots = new List<GameSaveSlotInfo>(SlotCount);
                for (var index = 0; index < SlotCount; index++)
                {
                    slots.Add(ReadSlot(index));
                }

                return slots;
            }
        }

        /// <summary>Returns a snapshot for one configured slot.</summary>
        public GameSaveSlotInfo GetSlot(int index)
        {
            if (!IsValidSlot(index))
            {
                return new GameSaveSlotInfo(index, GameSaveSlotState.Unavailable, InvalidSlotMessage(index));
            }

            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    return ReadSlot(index);
                }
                catch (Exception exception)
                {
                    return new GameSaveSlotInfo(index, GameSaveSlotState.Unavailable, exception.Message);
                }
            }
        }

        /// <summary>
        /// Saves a manual slot. This explicit operation may overwrite an occupied or corrupt manual slot;
        /// callers should obtain confirmation from their UI before invoking it.
        /// </summary>
        public bool TrySaveManual(int index, GameSaveData data, out string error)
        {
            if (!IsManualSlot(index))
            {
                error = "Manual saves must use slots 1 through 5.";
                return false;
            }

            return TrySave(index, data, false, out error);
        }

        /// <summary>Saves the autosave slot. A corrupt existing autosave is preserved and blocks replacement.</summary>
        public bool TryAutoSave(GameSaveData data, out string error)
        {
            return TrySave(AutoSaveSlot, data, true, out error);
        }

        /// <summary>
        /// Saves to the first empty manual slot without replacing an existing file.
        /// Returns the selected slot index, or -1 when no save was committed.
        /// </summary>
        public bool TryQuickSave(GameSaveData data, out int index, out string error)
        {
            index = -1;
            error = null;
            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    index = FindFirstEmptyManualSlot();
                    if (index < FirstManualSlot)
                    {
                        error = "All manual save slots are occupied.";
                        return false;
                    }

                    if (!TrySave(index, data, false, out error))
                    {
                        index = -1;
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    index = -1;
                    error = "Could not quick save: " + exception.Message;
                    return false;
                }
            }
        }

        /// <summary>Loads any configured slot, including autosave.</summary>
        public bool TryLoad(int index, out GameSaveData data, out string error)
        {
            data = null;
            error = null;
            if (!IsValidSlot(index))
            {
                error = InvalidSlotMessage(index);
                return false;
            }

            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    var result = SaveKit.TryLoad(SaveTarget.Slot(index));
                    if (!result.Succeeded)
                    {
                        error = FormatLoadError(result);
                        return false;
                    }

                    data = result.Data.GetModule<GameSaveData>(ModuleId);
                    if (data == null)
                    {
                        error = "Save is missing the game data module.";
                        data = null;
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    data = null;
                    error = "Save payload is corrupt: " + exception.Message;
                    return false;
                }
            }
        }

        /// <summary>Deletes a manual slot. Autosave slot 0 is intentionally not deletable through this API.</summary>
        public bool TryDeleteManual(int index, out string error)
        {
            error = null;
            if (!IsManualSlot(index))
            {
                error = "Only manual slots 1 through 5 can be deleted.";
                return false;
            }

            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    if (!mStorage.Exists(SaveTarget.Slot(index)))
                    {
                        error = "Manual slot is already empty.";
                        return false;
                    }

                    if (!SaveKit.Delete(SaveTarget.Slot(index)))
                    {
                        error = "SaveKit did not delete the manual slot.";
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    error = "Could not delete manual slot: " + exception.Message;
                    return false;
                }
            }
        }

        /// <summary>Finds the first physically empty manual slot, or -1 when all five are occupied.</summary>
        public int FindFirstEmptyManualSlot()
        {
            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    for (var index = FirstManualSlot; index <= LastManualSlot; index++)
                    {
                        if (!mStorage.Exists(SaveTarget.Slot(index)))
                        {
                            return index;
                        }
                    }
                }
                catch
                {
                    // An unavailable backend cannot safely identify an empty slot.
                }

                return -1;
            }
        }

        private bool TrySave(int index, GameSaveData data, bool protectCorruptAutosave, out string error)
        {
            error = null;
            if (data == null)
            {
                error = "Game save data cannot be null.";
                return false;
            }

            lock (mSyncRoot)
            {
                try
                {
                    ConfigureSaveKit();
                    var target = SaveTarget.Slot(index);
                    if (protectCorruptAutosave && mStorage.Exists(target))
                    {
                        var existing = SaveKit.TryLoad(target);
                        if (!existing.Succeeded)
                        {
                            error = "Autosave was not replaced because the existing file is unreadable: " + FormatLoadError(existing);
                            return false;
                        }
                    }

                    data.DataVersion = CurrentDataVersion;
                    var container = SaveKit.CreateSaveData();
                    container.RegisterModule(data, ModuleId);
                    SaveKit.Save(target, container, data.SceneName);
                    return true;
                }
                catch (Exception exception)
                {
                    error = "Could not save slot " + index + ": " + exception.Message;
                    return false;
                }
            }
        }

        private GameSaveSlotInfo ReadSlot(int index)
        {
            var target = SaveTarget.Slot(index);
            if (!mStorage.Exists(target))
            {
                return new GameSaveSlotInfo(index, GameSaveSlotState.Empty);
            }

            var result = SaveKit.TryLoad(target);
            if (!result.Succeeded)
            {
                var state = result.Status == SaveLoadStatus.SerializerMismatch
                    ? GameSaveSlotState.Incompatible
                    : GameSaveSlotState.Corrupt;
                return new GameSaveSlotInfo(index, state, FormatLoadError(result), GetSavedAt(result.Meta));
            }

            try
            {
                var data = result.Data.GetModule<GameSaveData>(ModuleId);
                if (data == null)
                {
                    return new GameSaveSlotInfo(index, GameSaveSlotState.Corrupt,
                        "Save is missing the game data module.", GetSavedAt(result.Meta));
                }

                return new GameSaveSlotInfo(index, GameSaveSlotState.Occupied, null, GetSavedAt(result.Meta),
                    data.SceneName, data.ElapsedPlaytimeSeconds);
            }
            catch (Exception exception)
            {
                return new GameSaveSlotInfo(index, GameSaveSlotState.Corrupt,
                    "Save payload is corrupt: " + exception.Message, GetSavedAt(result.Meta));
            }
        }

        private void ConfigureSaveKit()
        {
            SaveKit.SetStorage(mStorage);
            SaveKit.SetSerializer(mSerializer);
        }

        private static DateTimeOffset? GetSavedAt(SaveMeta meta)
        {
            return meta.LastSavedTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(meta.LastSavedTimestamp)
                : (DateTimeOffset?)null;
        }

        private static string FormatLoadError(SaveLoadResult result)
        {
            return "SaveKit load failed (" + result.Status + "): " + (result.Error ?? "unknown error");
        }

        private static bool IsValidSlot(int index)
        {
            return index >= AutoSaveSlot && index <= LastManualSlot;
        }

        private static bool IsManualSlot(int index)
        {
            return index >= FirstManualSlot && index <= LastManualSlot;
        }

        private static string InvalidSlotMessage(int index)
        {
            return "Slot index must be between 0 and 5; received " + index + ".";
        }
    }
}

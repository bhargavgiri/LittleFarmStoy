using System;
using System.Collections.Generic;
using LittleFarmStory.Animals;
using LittleFarmStory.CameraSystem;
using LittleFarmStory.Economy;
using LittleFarmStory.Farming;
using LittleFarmStory.Inventory;
using LittleFarmStory.Player;
using UnityEngine;

namespace LittleFarmStory.Persistence
{
    /// <summary>
    /// Captures the farm into a <see cref="SaveData"/> and puts it back again.
    ///
    /// It owns no gameplay state. Every value it reads or writes goes through the capture and
    /// restore methods the owning system already exposes - the wallet's balance, the inventory's
    /// store, each field's plots, each animal's needs and production. Adding a savable system
    /// means giving that system a capture/restore pair and listing it here, not moving its state
    /// into this class.
    ///
    /// LOAD TIMING. Every system resets itself to authored defaults in its own Awake, and a
    /// field does not even own its plots until FarmGrid.Awake has built them. Restoring runs in
    /// Start behind a late execution order, so it overwrites those defaults rather than racing
    /// them. Nothing else needed changing to make that work: the restore paths all raise the
    /// same events gameplay does, so the HUD follows along on its own.
    ///
    /// SAVE TIMING. Android kills an app without warning, so the pause callback is the one that
    /// actually matters on device; quit covers the editor and a clean exit, and the periodic
    /// autosave covers a process killed outright.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class SaveManager : MonoBehaviour
    {
        [Header("Player state")]
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerController playerController;
        [Tooltip("Optional. Snapped to the player after a load so the camera does not glide " +
                 "across the farm to catch up.")]
        [SerializeField] private FarmCameraController cameraController;

        [Header("World state")]
        [SerializeField] private FarmGrid[] fields;
        [SerializeField] private AnimalHabitat[] habitats;

        [Header("Behaviour")]
        [Tooltip("Load the save file, when one exists, as the scene starts.")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Tooltip("Seconds between autosaves. 0 disables periodic saving; pause and quit still save.")]
        [Min(0f)] [SerializeField] private float autosaveInterval = 60f;

        [Tooltip("Development only. Logs every save and load with what it contained.")]
        [SerializeField] private bool logSaves;

        private float autosaveTimer;

        /// <summary>False until Start has finished, so a very early quit cannot save defaults over a real file.</summary>
        private bool ready;

        public bool HasSave => SaveSystem.Exists();

        /// <summary>Raised after a save is written, and after a load is applied.</summary>
        public event Action Saved;

        public event Action Loaded;

        // ============================================================ lifecycle

        private void Awake()
        {
            ResolveMissingReferences();
        }

        private void Start()
        {
            if (autoLoadOnStart && SaveSystem.Exists())
            {
                Load();
            }

            ready = true;
        }

        private void Update()
        {
            if (!ready || autosaveInterval <= 0f)
            {
                return;
            }

            autosaveTimer += Time.unscaledDeltaTime;

            if (autosaveTimer < autosaveInterval)
            {
                return;
            }

            autosaveTimer = 0f;
            Save();
        }

        private void OnApplicationPause(bool paused)
        {
            // The only callback Android reliably delivers before the process can be killed.
            if (paused && ready)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (ready)
            {
                Save();
            }
        }

        // ============================================================ save

        /// <summary>Captures the farm and writes it. Returns false when the write failed.</summary>
        public bool Save()
        {
            SaveData data = Capture();
            bool written = SaveSystem.Write(data);

            if (!written)
            {
                return false;
            }

            autosaveTimer = 0f;

            if (logSaves)
            {
                Debug.Log("[SAVE] wrote " + data.Coins + " coins, " + data.Inventory.Count +
                          " item rows, " + CountPlots(data) + " plots, " + data.Animals.Count +
                          " animals to " + SaveSystem.SavePath, this);
            }

            Saved?.Invoke();
            return true;
        }

        private SaveData Capture()
        {
            SaveData data = new SaveData
            {
                Version = SaveData.CurrentVersion,
                SavedUtcTicks = DateTime.UtcNow.Ticks,
                Coins = wallet != null ? wallet.GetBalance() : 0
            };

            if (inventory != null)
            {
                foreach (KeyValuePair<string, int> item in inventory.All)
                {
                    data.Inventory.Add(new InventoryEntry { ItemId = item.Key, Amount = item.Value });
                }
            }

            if (playerController != null)
            {
                Vector3 position = playerController.transform.position;

                data.Player = new PlayerSnapshot
                {
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    VisualYaw = playerController.VisualYaw
                };
            }

            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    FarmGrid field = fields[i];

                    if (field == null)
                    {
                        continue;
                    }

                    data.Fields.Add(new FieldSnapshot
                    {
                        FieldId = field.FieldId,
                        Plots = new List<FarmPlotSnapshot>(field.CaptureSnapshots())
                    });
                }
            }

            if (habitats != null)
            {
                for (int i = 0; i < habitats.Length; i++)
                {
                    AnimalHabitat habitat = habitats[i];

                    if (habitat == null)
                    {
                        continue;
                    }

                    IReadOnlyList<AnimalController> residents = habitat.Animals;

                    for (int a = 0; a < residents.Count; a++)
                    {
                        if (residents[a] != null)
                        {
                            data.Animals.Add(residents[a].CaptureSnapshot());
                        }
                    }
                }
            }

            return data;
        }

        // ============================================================ load

        /// <summary>Reads the save file and applies it. Returns false when there was nothing usable to load.</summary>
        public bool Load()
        {
            if (!SaveSystem.TryRead(out SaveData data))
            {
                return false;
            }

            Apply(data);

            if (logSaves)
            {
                Debug.Log("[SAVE] loaded " + data.Coins + " coins, " + data.Inventory.Count +
                          " item rows, " + CountPlots(data) + " plots, " + data.Animals.Count +
                          " animals from " + SaveSystem.SavePath, this);
            }

            Loaded?.Invoke();
            return true;
        }

        private void Apply(SaveData data)
        {
            if (wallet != null)
            {
                wallet.RestoreBalance(data.Coins);
            }

            if (inventory != null)
            {
                List<KeyValuePair<string, int>> items =
                    new List<KeyValuePair<string, int>>(data.Inventory.Count);

                for (int i = 0; i < data.Inventory.Count; i++)
                {
                    InventoryEntry entry = data.Inventory[i];
                    items.Add(new KeyValuePair<string, int>(entry.ItemId, entry.Amount));
                }

                inventory.RestoreAll(items);
            }

            ApplyPlayer(data.Player);
            ApplyFields(data.Fields);
            ApplyAnimals(data.Animals);
        }

        private void ApplyPlayer(PlayerSnapshot snapshot)
        {
            if (playerController == null)
            {
                return;
            }

            playerController.RestoreTransform(
                new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
                snapshot.VisualYaw);

            // Without this the camera starts wherever the player spawned and smooth-damps across
            // the farm to the restored position.
            if (cameraController != null)
            {
                cameraController.SnapToTarget();
            }
        }

        private void ApplyFields(List<FieldSnapshot> saved)
        {
            if (saved == null || fields == null)
            {
                return;
            }

            for (int i = 0; i < saved.Count; i++)
            {
                FieldSnapshot snapshot = saved[i];

                if (snapshot == null)
                {
                    continue;
                }

                FarmGrid field = FindField(snapshot.FieldId);

                if (field == null)
                {
                    Debug.LogWarning("SaveManager: the save names field '" + snapshot.FieldId +
                                     "', which this scene does not have; its plots were not restored.", this);
                    continue;
                }

                field.RestoreSnapshots(snapshot.Plots);
            }
        }

        private void ApplyAnimals(List<AnimalSnapshot> saved)
        {
            if (saved == null || habitats == null)
            {
                return;
            }

            Dictionary<string, AnimalController> byInstanceId = BuildAnimalLookup();

            for (int i = 0; i < saved.Count; i++)
            {
                AnimalSnapshot snapshot = saved[i];

                if (string.IsNullOrEmpty(snapshot.InstanceId) ||
                    !byInstanceId.TryGetValue(snapshot.InstanceId, out AnimalController animal))
                {
                    Debug.LogWarning("SaveManager: the save names animal '" + snapshot.InstanceId +
                                     "', which this scene does not have; it was not restored.", this);
                    continue;
                }

                animal.RestoreSnapshot(snapshot);
            }
        }

        private Dictionary<string, AnimalController> BuildAnimalLookup()
        {
            Dictionary<string, AnimalController> lookup = new Dictionary<string, AnimalController>();

            for (int i = 0; i < habitats.Length; i++)
            {
                AnimalHabitat habitat = habitats[i];

                if (habitat == null)
                {
                    continue;
                }

                IReadOnlyList<AnimalController> residents = habitat.Animals;

                for (int a = 0; a < residents.Count; a++)
                {
                    AnimalController animal = residents[a];

                    if (animal == null || string.IsNullOrEmpty(animal.InstanceId))
                    {
                        continue;
                    }

                    if (!lookup.ContainsKey(animal.InstanceId))
                    {
                        lookup.Add(animal.InstanceId, animal);
                        continue;
                    }

                    Debug.LogError("SaveManager: two animals share the instance id '" +
                                   animal.InstanceId + "'. Saved state for it cannot be " +
                                   "restored to the right animal.", animal);
                }
            }

            return lookup;
        }

        private FarmGrid FindField(string fieldId)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != null && fields[i].FieldId == fieldId)
                {
                    return fields[i];
                }
            }

            return null;
        }

        // ============================================================ maintenance

        /// <summary>Deletes the save file. The scene in memory is left exactly as it is.</summary>
        public bool DeleteSave()
        {
            return SaveSystem.Delete();
        }

        // ============================================================ wiring

        /// <summary>
        /// The scene builder wires everything, but a field or a pen added by hand afterwards
        /// would otherwise be saved by nobody. Resolved once, at Awake, never per frame.
        /// </summary>
        private void ResolveMissingReferences()
        {
            if (fields == null || fields.Length == 0)
            {
                fields = FindObjectsByType<FarmGrid>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Debug.LogWarning("SaveManager had no fields wired; found " + fields.Length +
                                 " in the scene instead.", this);
            }

            if (habitats == null || habitats.Length == 0)
            {
                habitats = FindObjectsByType<AnimalHabitat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Debug.LogWarning("SaveManager had no habitats wired; found " + habitats.Length +
                                 " in the scene instead.", this);
            }

            if (wallet == null)
            {
                Debug.LogError("SaveManager has no CurrencyWallet; coins will not be saved.", this);
            }

            if (inventory == null)
            {
                Debug.LogError("SaveManager has no PlayerInventory; items will not be saved.", this);
            }

            if (playerController == null)
            {
                Debug.LogError("SaveManager has no PlayerController; the farmer's position will " +
                               "not be saved.", this);
            }
        }

        private static int CountPlots(SaveData data)
        {
            int total = 0;

            for (int i = 0; i < data.Fields.Count; i++)
            {
                if (data.Fields[i] != null && data.Fields[i].Plots != null)
                {
                    total += data.Fields[i].Plots.Count;
                }
            }

            return total;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the prototype builder tool.</summary>
        public void EditorConfigure(
            CurrencyWallet currencyWallet, PlayerInventory playerInventory,
            PlayerController player, FarmCameraController camera,
            FarmGrid[] farmFields, AnimalHabitat[] animalHabitats, bool diagnostics)
        {
            wallet = currencyWallet;
            inventory = playerInventory;
            playerController = player;
            cameraController = camera;
            fields = farmFields;
            habitats = animalHabitats;
            logSaves = diagnostics;
        }
#endif
    }
}

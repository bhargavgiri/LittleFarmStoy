using System;
using System.Collections.Generic;
using LittleFarmStory.Animals;
using LittleFarmStory.Farming;

namespace LittleFarmStory.Persistence
{
    /// <summary>One item row. JsonUtility cannot serialize a Dictionary, so the store travels as a list.</summary>
    [Serializable]
    public struct InventoryEntry
    {
        public string ItemId;
        public int Amount;
    }

    /// <summary>
    /// Where the farmer stood. Position is the root transform; yaw is the visual child, which is
    /// what actually carries facing - see <c>PlayerController.FaceMovementDirection</c>.
    /// </summary>
    [Serializable]
    public struct PlayerSnapshot
    {
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float VisualYaw;
    }

    /// <summary>
    /// One field's plots. Plots are nested under their field rather than carrying a field id,
    /// because a plot's coordinate is only unique within its own grid.
    /// </summary>
    [Serializable]
    public class FieldSnapshot
    {
        public string FieldId;
        public List<FarmPlotSnapshot> Plots = new List<FarmPlotSnapshot>();
    }

    /// <summary>
    /// The whole save file. Composed of the snapshot types each system already owns, so this
    /// class adds no new state of its own - it is a container and a version stamp.
    ///
    /// Everything here is a primitive, a string, an enum or a list of those: the subset
    /// JsonUtility actually round-trips.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Bumped whenever the shape below changes in a way old files cannot satisfy.</summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>When the file was written. Recorded for diagnostics; no offline progression reads it.</summary>
        public long SavedUtcTicks;

        public int Coins;
        public List<InventoryEntry> Inventory = new List<InventoryEntry>();
        public PlayerSnapshot Player;
        public List<FieldSnapshot> Fields = new List<FieldSnapshot>();
        public List<AnimalSnapshot> Animals = new List<AnimalSnapshot>();
    }
}

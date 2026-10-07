using System;
using System.IO;
using UnityEngine;

namespace LittleFarmStory.Persistence
{
    /// <summary>
    /// Reading and writing the save file, and nothing else. It knows about JSON and the disk;
    /// it knows nothing about coins, plots or animals - <see cref="SaveManager"/> owns that.
    ///
    /// Writes go to a temporary file first and only then replace the real one. A process killed
    /// mid-write (which on Android is routine) therefore destroys the temporary file rather than
    /// the last good save.
    ///
    /// Every method here touches the filesystem, so every one of them can genuinely fail for
    /// reasons the game cannot control: no space, no permission, a file another process holds.
    /// They report failure rather than throwing into gameplay code.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "littlefarmstory.save.json";
        private const string TempSuffix = ".tmp";

        /// <summary>Full path of the save file. Public so a diagnostic can print where to look.</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        private static string TempPath => SavePath + TempSuffix;

        public static bool Exists()
        {
            try
            {
                return File.Exists(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: could not check for a save file: " + e.Message);
                return false;
            }
        }

        /// <summary>Serialises and writes atomically. Returns false - having changed nothing - on failure.</summary>
        public static bool Write(SaveData data)
        {
            if (data == null)
            {
                Debug.LogError("SaveSystem: refused to write a null SaveData.");
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(data, true);

                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(TempPath, json);

                // Replace only once the new contents are safely on disk.
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                File.Move(TempPath, SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: failed to write " + SavePath + ": " + e.Message);
                TryDeleteTemp();
                return false;
            }
        }

        /// <summary>
        /// Reads and deserialises. Returns false when there is no file, when it cannot be read,
        /// or when its contents are not a save this build understands - in every one of those
        /// cases <paramref name="data"/> is null and the caller should simply start a new game.
        /// </summary>
        public static bool TryRead(out SaveData data)
        {
            data = null;

            try
            {
                if (!File.Exists(SavePath))
                {
                    return false;
                }

                string json = File.ReadAllText(SavePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogWarning("SaveSystem: the save file at " + SavePath + " is empty; ignoring it.");
                    return false;
                }

                SaveData parsed = JsonUtility.FromJson<SaveData>(json);

                if (parsed == null)
                {
                    Debug.LogWarning("SaveSystem: the save file at " + SavePath + " could not be parsed; ignoring it.");
                    return false;
                }

                if (parsed.Version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning("SaveSystem: the save file is version " + parsed.Version +
                                     " but this build understands up to " + SaveData.CurrentVersion +
                                     "; ignoring it rather than loading it wrongly.");
                    return false;
                }

                data = parsed;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: failed to read " + SavePath + ": " + e.Message);
                return false;
            }
        }

        public static bool Delete()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                TryDeleteTemp();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: failed to delete " + SavePath + ": " + e.Message);
                return false;
            }
        }

        private static void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(TempPath))
                {
                    File.Delete(TempPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: could not clean up " + TempPath + ": " + e.Message);
            }
        }
    }
}

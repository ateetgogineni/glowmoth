using System;
using System.IO;
using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Local progress: best stars per level plus audio settings, stored as a small JSON file in
    /// Application.persistentDataPath. Written only when something changes (never per frame).
    /// </summary>
    public static class SaveSystem
    {
        [Serializable]
        sealed class SaveData
        {
            public int version = 1;
            public int[] stars = new int[0];
            public bool sound = true;
            public bool music = true;
        }

        static SaveData data;

        static string FilePath => Path.Combine(Application.persistentDataPath, "glowmoth_save.json");

        public static void Load()
        {
            data = null;
            try
            {
                if (File.Exists(FilePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Glowmoth] Could not read save file, starting fresh: " + e.Message);
            }

            if (data == null) data = new SaveData();
            if (data.stars == null || data.stars.Length != LevelDatabase.Count)
            {
                var resized = new int[LevelDatabase.Count];
                if (data.stars != null) Array.Copy(data.stars, resized, Mathf.Min(data.stars.Length, resized.Length));
                data.stars = resized;
            }
        }

        public static void Save()
        {
            try
            {
                // Write to a temp file first so a crash mid-write cannot corrupt progress.
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Glowmoth] Could not write save file: " + e.Message);
            }
        }

        public static int GetStars(int level)
        {
            return level >= 0 && level < data.stars.Length ? data.stars[level] : 0;
        }

        /// <summary>Records a result. Returns true if it beat the previous best.</summary>
        public static bool SubmitStars(int level, int stars)
        {
            if (level < 0 || level >= data.stars.Length || stars <= data.stars[level]) return false;
            data.stars[level] = stars;
            Save();
            return true;
        }

        public static bool IsUnlocked(int level)
        {
            return level == 0 || (level > 0 && level < data.stars.Length && data.stars[level - 1] > 0);
        }

        public static int TotalStars()
        {
            int total = 0;
            for (int i = 0; i < data.stars.Length; i++) total += data.stars[i];
            return total;
        }

        public static bool Sound
        {
            get => data.sound;
            set { data.sound = value; Save(); }
        }

        public static bool Music
        {
            get => data.music;
            set { data.music = value; Save(); }
        }
    }
}

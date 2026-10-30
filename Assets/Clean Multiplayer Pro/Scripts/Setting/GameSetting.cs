using System;
using UnityEngine;

namespace AvocadoShark
{
    [CreateAssetMenu(fileName = "GameSetting", menuName = "ScriptableObject/Game Setting")]
    public class GameSetting : ScriptableObject
    {
        [SerializeField] private SettingData settingData;
        private const string Key = "GameSettingData";
        private bool _loaded;

        // Load once and cache - this property is read from per-frame paths (camera look
        // sensitivity), and PlayerPrefs + JSON parsing per access is wasteful.
        public SettingData SettingData
        {
            get
            {
                if (!_loaded)
                {
                    LoadSettings();
                    _loaded = true;
                }
                return settingData;
            }
        }

        private SettingData LoadSettings()
        {
            if (PlayerPrefs.HasKey(Key))
                settingData = JsonUtility.FromJson<SettingData>(PlayerPrefs.GetString(Key));
            return settingData;
        }

        public void SaveSettings()
        {
            var json = JsonUtility.ToJson(settingData);
            PlayerPrefs.SetString(Key,json);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public class SettingData
    {
        public bool sound;
        [Range(0.25f, 5f)] public float lookSensitivity;
    }
}
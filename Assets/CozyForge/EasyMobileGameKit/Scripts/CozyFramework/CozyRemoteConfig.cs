using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace CozyFramework
{
    public struct userAttributes { }
    public struct appAttributes { }

    

    public class CozyRemoteConfig : MonoBehaviour
    {
        // Static instance to access the config easily
        public static CozyRemoteConfig Instance;

        // Dictionaries to hold different types of remote config values
        public Dictionary<string, bool> boolConfigs = new Dictionary<string, bool>();
        public Dictionary<string, int> intConfigs = new Dictionary<string, int>();
        public Dictionary<string, float> floatConfigs = new Dictionary<string, float>();
        public Dictionary<string, string> stringConfigs = new Dictionary<string, string>();

        
        private List<CozyRemoteConfig.ConfigKey> configKeys = new List<CozyRemoteConfig.ConfigKey>
        {
        };
        
        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        private void Start()
        {
            CozyEvents.AuthSuccess += FetchRemoteConfig;
        }

        private void OnDisable()
        {
            CozyEvents.AuthSuccess -= FetchRemoteConfig;
        }

        private async void FetchRemoteConfig()
        {
            RemoteConfigService.Instance.FetchCompleted += OnRemoteConfigFetched;
            await RemoteConfigService.Instance.FetchConfigsAsync(new userAttributes(), new appAttributes());
        }

        private void OnRemoteConfigFetched(ConfigResponse configResponse)
        {
            RemoteConfigService.Instance.FetchCompleted -= OnRemoteConfigFetched;
            CozyEvents.OnRemoteConfigsFetched(RemoteConfigService.Instance.appConfig);
            SaveAllConfigValues();
        }

        private void SaveAllConfigValues()
        {
            var config = RemoteConfigService.Instance.appConfig;

            foreach (var configKey in configKeys)
            {
                string key = configKey.Key;
                try
                {
                    switch (configKey.Type)
                    {
                        case ConfigType.Bool:
                            if (config.HasKey(key))
                            {
                                boolConfigs[key] = config.GetBool(key);
                            }
                            break;

                        case ConfigType.Int:
                            if (config.HasKey(key))
                            {
                                intConfigs[key] = config.GetInt(key);
                            }
                            break;

                        case ConfigType.Float:
                            if (config.HasKey(key))
                            {
                                floatConfigs[key] = config.GetFloat(key);
                            }
                            break;

                        case ConfigType.String:
                            if (config.HasKey(key))
                            {
                                stringConfigs[key] = config.GetString(key);
                            }
                            break;

                        case ConfigType.Long:
                            if (config.HasKey(key))
                            {
                                intConfigs[key] = (int)config.GetLong(key);
                            }
                            break;

                        case ConfigType.Json:
                            if (config.HasKey(key))
                            {
                                string jsonString = config.GetJson(key);
                                if (!string.IsNullOrEmpty(jsonString))
                                {
                                    
                                }
                            }
                            break;

                        default:
                            Debug.LogWarning($"Unsupported config type for key: {key}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error fetching key '{key}': {ex.Message}");
                }
            }
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return boolConfigs.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            return intConfigs.TryGetValue(key, out int value) ? value : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            return floatConfigs.TryGetValue(key, out float value) ? value : defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            return stringConfigs.TryGetValue(key, out string value) ? value : defaultValue;
        }

        // Class to hold config key and type
        public class ConfigKey
        {
            public string Key { get; private set; }
            public ConfigType Type { get; private set; }

            public ConfigKey(string key, ConfigType type)
            {
                Key = key;
                Type = type;
            }
        }

        // Enum to define config types
        public enum ConfigType
        {
            Bool,
            Int,
            Float,
            Long,
            String,
            Json
        }
    }
}

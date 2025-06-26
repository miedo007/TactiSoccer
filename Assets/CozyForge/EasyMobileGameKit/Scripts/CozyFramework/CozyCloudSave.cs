using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

namespace CozyFramework
{
    public class CozyCloudSave : MonoBehaviour
    {
        public static CozyCloudSave Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
        
        public static async Task SaveData(Dictionary<string, object> data)
        {
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogError("Error saving data: " + e);
            }
        }
        
        public static async Task SaveData<T>(string key, T value)
        {
            var data = new Dictionary<string, object> { { key, value } };
            await SaveData(data);
        }
        
        public static async Task<T> LoadData<T>(string key)
        {
            var keysToLoad = new HashSet<string> { key };
            var data = await LoadData(keysToLoad);

            if (data != null && data.TryGetValue(key, out var item))
            {
                try
                {
                    string jsonString = item.Value.GetAsString();
                    
                    if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
                    {
                        return (T)Convert.ChangeType(jsonString, typeof(T));
                    }
                    else
                    {
                        return JsonUtility.FromJson<T>(jsonString);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to convert value to type {typeof(T)}: {e.Message}");
                    return default;
                }
            }
            else
            {
                return default;
            }
        }
        
        
        public static async Task<Dictionary<string, Item>> LoadData(HashSet<string> keysToLoad)
        {
            try
            {
                var loadedData = await CloudSaveService.Instance.Data.Player.LoadAsync(keysToLoad);
                return loadedData;
            }
            catch (Exception e)
            {
                Debug.LogError("Error loading data: " + e);
                return null;
            }
        }
    }
}

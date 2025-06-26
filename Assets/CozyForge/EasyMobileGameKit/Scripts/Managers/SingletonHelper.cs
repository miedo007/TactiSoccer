using UnityEngine;

namespace CozyFramework
{
    public static class SingletonHelper
    {
        public static void InitializeSingleton<T>(ref T instance, T self) where T : MonoBehaviour
        {
            if (instance == null)
            {
                instance = self;
            }
            else
            {
                if (self != instance)
                {
                    Object.Destroy(self.gameObject);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using UnityEngine;

namespace CozyFramework
{
    public class CozyAnalyticsEvents : MonoBehaviour
    {
        
        public static CozyAnalyticsEvents Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        public void SendAnalyticEvent(string eventName, Dictionary<string, object> parameters)
        {
            AnalyticsService.Instance.RecordEvent(new GenericAnalyticsEvent(eventName, parameters));
        }
        
        public class GenericAnalyticsEvent : Unity.Services.Analytics.Event
        {
            public GenericAnalyticsEvent(string eventName, Dictionary<string, object> parameters) : base(eventName)
            {
                foreach (var kvp in parameters)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    switch (value)
                    {
                        case bool boolVal:
                            SetParameter(key, boolVal);
                            break;
                        case double doubleVal:
                            SetParameter(key, doubleVal);
                            break;
                        case float floatVal:
                            SetParameter(key, floatVal);
                            break;
                        case int intVal:
                            SetParameter(key, intVal);
                            break;
                        case long longVal:
                            SetParameter(key, longVal);
                            break;
                        case string strVal:
                            SetParameter(key, strVal);
                            break;
                        default:
                            if (value is IConvertible)
                            {
                                SetParameter(key, value.ToString());
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning($"Unsupported parameter type for key '{key}': {value.GetType()}");
                            }

                            break;
                    }
                }
            }
        }
    }
}

using UnityEngine;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public class TrackingRequester : MonoBehaviour
{
    void Start()
    {
#if UNITY_IOS
        Device.RequestAdvertisingIdentifierAsync((string idfa, bool trackingAllowed, string error) => {
            Debug.Log($"IDFA: {idfa}, trackingAllowed: {trackingAllowed}");
        });
#endif
    }
}

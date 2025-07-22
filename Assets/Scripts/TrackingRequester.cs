using UnityEngine;
#if UNITY_IOS
using Unity.Advertisement.IosSupport; // from com.unity.ads.ios-support
#endif

public class TrackingRequester : MonoBehaviour
{
    void Awake()
    {
#if UNITY_IOS
        // only request if the status is still “Not Determined”
        if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus()
            == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            ATTrackingStatusBinding.RequestAuthorizationTracking();
        }
#endif
    }
}

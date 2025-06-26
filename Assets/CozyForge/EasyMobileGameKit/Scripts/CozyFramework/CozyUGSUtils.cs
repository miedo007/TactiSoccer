using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CozyFramework
{
    public class CozyUGSUtils : MonoBehaviour
    {
        public static async Task<T> RetryEconomyFunction<T>(Func<Task<T>> functionToRetry, int retryAfterSeconds)
        {
            if (retryAfterSeconds > 60)
            {
                Debug.Log("Economy returned a rate limit exception with an extended Retry After time " +
                          $"of {retryAfterSeconds} seconds. Suggest manually retrying at a later time.");
                return default;
            }

            Debug.Log($"Economy returned a rate limit exception. Retrying after {retryAfterSeconds} seconds");

            try
            {
                using (var cancellationTokenHelper = new CancellationTokenHelper())
                {
                    var cancellationToken = cancellationTokenHelper.cancellationToken;

                    await Task.Delay(retryAfterSeconds * 1000, cancellationToken);

                    // Call the function that we passed in to this method after the retry after time period has passed.
                    var result = await functionToRetry();

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return default;
                    }

                    Debug.Log("Economy retry successfully completed");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return default;
        }
    }
}

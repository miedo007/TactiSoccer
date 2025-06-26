using System;
using System.Threading;
using UnityEditor;

namespace CozyFramework
{
    public class CancellationTokenHelper : IDisposable
    {
        CancellationTokenSource m_CancellationTokenSource;
        bool m_Disposed;

        public CancellationToken cancellationToken => m_CancellationTokenSource.Token;

        public CancellationTokenHelper()
        {
            m_CancellationTokenSource = new CancellationTokenSource();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                m_CancellationTokenSource?.Cancel();
            }
        }
#endif
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool triggeredByUserCode)
        {
            if (m_Disposed)
            {
                return;
            }

            if (triggeredByUserCode)
            {
                m_CancellationTokenSource.Dispose();
                m_CancellationTokenSource = null;
            }

#if UNITY_EDITOR

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

            m_Disposed = true;
        }

        ~CancellationTokenHelper()
        {
            Dispose(false);
        }
    }
}

using UnityEngine;
using System;
using System.Threading;

public class AndroidImmersiveMode : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR

    private AndroidJavaObject activity;
    private AndroidJavaObject decorView;
    private AndroidJavaProxy visibilityListener;

    private int applyingMode;
    private bool initialized;

    private const int HIDE_NAVIGATION = 0x00000002;
    private const int FULLSCREEN = 0x00000004;
    private const int LAYOUT_STABLE = 0x00000100;
    private const int LAYOUT_HIDE_NAVIGATION = 0x00000200;
    private const int LAYOUT_FULLSCREEN = 0x00000400;
    private const int IMMERSIVE_STICKY = 0x00001000;

    private const int IMMERSIVE_FLAGS =
        HIDE_NAVIGATION |
        FULLSCREEN |
        LAYOUT_STABLE |
        LAYOUT_HIDE_NAVIGATION |
        LAYOUT_FULLSCREEN |
        IMMERSIVE_STICKY;

#endif

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        InitializeAndroid();
    }

    private void InitializeAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (initialized)
            return;

        try
        {
            using (AndroidJavaClass unityPlayer =
                   new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            }

            AndroidJavaObject window =
                activity.Call<AndroidJavaObject>("getWindow");

            decorView =
                window.Call<AndroidJavaObject>("getDecorView");

            visibilityListener = new SystemUiVisibilityListener(this);

            decorView.Call(
                "setOnSystemUiVisibilityChangeListener",
                visibilityListener
            );

            initialized = true;
            ApplyImmersiveMode();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AndroidImmersiveMode: " + e.Message);
        }
#endif
    }

    public void ApplyImmersiveMode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!initialized || activity == null)
            return;

        // Evita solicitudes repetidas simultáneas.
        if (Interlocked.Exchange(ref applyingMode, 1) == 1)
            return;

        try
        {
            activity.Call(
                "runOnUiThread",
                new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        if (decorView != null)
                        {
                            decorView.Call(
                                "setSystemUiVisibility",
                                IMMERSIVE_FLAGS
                            );
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(
                            "AndroidImmersiveMode: " + e.Message);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref applyingMode, 0);
                    }
                })
            );
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref applyingMode, 0);

            Debug.LogWarning(
                "AndroidImmersiveMode: " + e.Message);
        }
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplyImmersiveMode();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            ApplyImmersiveMode();
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (decorView != null)
        {
            try
            {
                decorView.Call(
                    "setOnSystemUiVisibilityChangeListener",
                    new object[] { null }
                );
            }
            catch (Exception) { }

            decorView.Dispose();
            decorView = null;
        }

        if (activity != null)
        {
            activity.Dispose();
            activity = null;
        }

        visibilityListener = null;
        initialized = false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR

    private class SystemUiVisibilityListener : AndroidJavaProxy
    {
        private readonly AndroidImmersiveMode owner;

        public SystemUiVisibilityListener(
            AndroidImmersiveMode owner
        ) : base(
            "android.view.View$OnSystemUiVisibilityChangeListener")
        {
            this.owner = owner;
        }

        public void onSystemUiVisibilityChange(int visibility)
        {
            bool navigationVisible =
                (visibility & HIDE_NAVIGATION) == 0;

            bool fullscreenDisabled =
                (visibility & FULLSCREEN) == 0;

            if (navigationVisible || fullscreenDisabled)
            {
                owner.ApplyImmersiveMode();
            }
        }
    }

#endif
}

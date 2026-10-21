
using UnityEngine;
using System;

public class AndroidImmersiveMode : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR

    private AndroidJavaObject activity;
    private AndroidJavaObject decorView;
    private AndroidJavaProxy visibilityListener;

    private bool applyingMode;

    private const int SYSTEM_UI_FLAG_HIDE_NAVIGATION = 0x00000002;
    private const int SYSTEM_UI_FLAG_FULLSCREEN = 0x00000004;
    private const int SYSTEM_UI_FLAG_LAYOUT_STABLE = 0x00000100;
    private const int SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION = 0x00000200;
    private const int SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN = 0x00000400;
    private const int SYSTEM_UI_FLAG_IMMERSIVE_STICKY = 0x00001000;

    private const int IMMERSIVE_FLAGS =
        SYSTEM_UI_FLAG_HIDE_NAVIGATION |
        SYSTEM_UI_FLAG_FULLSCREEN |
        SYSTEM_UI_FLAG_LAYOUT_STABLE |
        SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION |
        SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
        SYSTEM_UI_FLAG_IMMERSIVE_STICKY;

#endif

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        InitializeAndroid();
    }

    private void InitializeAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
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

            visibilityListener = new SystemUiVisibilityListener(
                this,
                "android.view.View$OnSystemUiVisibilityChangeListener"
            );

            decorView.Call(
                "setOnSystemUiVisibilityChangeListener",
                visibilityListener
            );

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
        if (decorView == null || applyingMode)
            return;

        try
        {
            applyingMode = true;

            decorView.Call(
                "setSystemUiVisibility",
                IMMERSIVE_FLAGS
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "No se pudo restaurar el modo inmersivo: "
                + e.Message);
        }
        finally
        {
            applyingMode = false;
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
            catch { }

            decorView.Dispose();
            decorView = null;
        }

        if (activity != null)
        {
            activity.Dispose();
            activity = null;
        }

        visibilityListener = null;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR

    private class SystemUiVisibilityListener : AndroidJavaProxy
    {
        private readonly AndroidImmersiveMode owner;

        public SystemUiVisibilityListener(
            AndroidImmersiveMode owner,
            string interfaceName
        ) : base(interfaceName)
        {
            this.owner = owner;
        }

        public void onSystemUiVisibilityChange(int visibility)
        {
            bool navigationVisible =
                (visibility & SYSTEM_UI_FLAG_HIDE_NAVIGATION) == 0;

            bool fullscreenDisabled =
                (visibility & SYSTEM_UI_FLAG_FULLSCREEN) == 0;

            if (navigationVisible || fullscreenDisabled)
            {
                owner.ApplyImmersiveMode();
            }
        }
    }

#endif
}

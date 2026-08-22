using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// The project-level additive scene unload entry point. It reads the existing
/// MySceneManager scene-entry setting named autoSaveBeforeLeaving before a
/// normal additive unload, then writes the unified game save before Unity is
/// allowed to destroy the scene objects.
/// </summary>
public static class AdditiveSceneAutoSave
{
    private const string _shouldAutoSaveMethodName = "ShouldAutoSaveBeforeLeaving";

    private static readonly BindingFlags _instanceMemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Unloads one additive scene. Set <paramref name="isNormalLeaving"/> to
    /// false for failed-load cleanup and other rollback paths that must never
    /// create an automatic save checkpoint.
    /// </summary>
    public static IEnumerator UnloadAdditiveSceneRoutine(
        string sceneKey,
        bool isNormalLeaving,
        Action<bool> onCompleted = null)
    {
        if (string.IsNullOrWhiteSpace(sceneKey) || MySceneManager.Instance == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (!MySceneManager.Instance.IsAdditivelyLoaded(sceneKey))
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (isNormalLeaving
            && ShouldAutoSaveBeforeLeaving(sceneKey)
            && !TrySaveBeforeLeaving(sceneKey))
        {
            // Do not unload after a requested checkpoint failed. The player
            // remains in the existing scene instead of losing unsaved state.
            onCompleted?.Invoke(false);
            yield break;
        }

        AsyncOperation unloadOperation = MySceneManager.Instance.UnloadAdditiveSceneAsync(sceneKey);
        if (unloadOperation == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        yield return unloadOperation;
        onCompleted?.Invoke(true);
    }

    /// <summary>
    /// Reads the existing SceneEntry setting through MySceneManager's own
    /// private lookup method. MySceneManager currently lives in the compiled
    /// shared toolkit, so this keeps the configuration authoritative there
    /// rather than duplicating a second per-scene save setting in this project.
    /// </summary>
    public static bool ShouldAutoSaveBeforeLeaving(string sceneKey)
    {
        object sceneManager = MySceneManager.Instance;
        if (sceneManager == null || string.IsNullOrWhiteSpace(sceneKey))
            return false;

        MethodInfo method = sceneManager.GetType().GetMethod(
            _shouldAutoSaveMethodName,
            _instanceMemberFlags,
            null,
            new[] { typeof(string) },
            null);

        if (method == null)
        {
            Debug.LogWarning($"[{nameof(AdditiveSceneAutoSave)}] Could not find {nameof(MySceneManager)}.{_shouldAutoSaveMethodName}; additive auto-save is skipped for '{sceneKey}'.");
            return false;
        }

        try
        {
            object result = method.Invoke(sceneManager, new object[] { sceneKey });
            return result is bool autoSaveBeforeLeaving && autoSaveBeforeLeaving;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(exception.InnerException ?? exception);
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool TrySaveBeforeLeaving(string sceneKey)
    {
        GameSaveManager saveManager = GameSaveManager.Instance;
        if (saveManager == null)
        {
            Debug.LogError($"[{nameof(AdditiveSceneAutoSave)}] '{sceneKey}' requests auto-save before leaving, but {nameof(GameSaveManager)} is unavailable.");
            return false;
        }

        try
        {
            saveManager.Save();
            Debug.Log($"[{nameof(AdditiveSceneAutoSave)}] Auto-saved before unloading additive scene '{sceneKey}'.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }
}

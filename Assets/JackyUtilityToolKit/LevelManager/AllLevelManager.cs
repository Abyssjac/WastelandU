using UnityEngine;
using UnityEngine.SceneManagement;

namespace JackyUtility
{
    /// <summary>
    /// Singleton manager responsible for level/scene transitions.
    /// Registers debug console commands on startup:
    ///   levelmgr-ld &lt;sceneName&gt;  ¡ª load a scene by name
    ///   levelmgr-reload           ¡ª reload the currently active scene
    /// </summary>
    public class AllLevelManager : MonoBehaviour
    {
        public static AllLevelManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterDebugCommands();
        }

        // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

        /// <summary>Load a scene by name.</summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[AllLevelManager] LoadScene: sceneName is null or empty.");
                return;
            }

            Debug.Log($"[AllLevelManager] Loading scene: '{sceneName}'");
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>Reload the currently active scene.</summary>
        public void ReloadCurrentScene()
        {
            string current = SceneManager.GetActiveScene().name;
            Debug.Log($"[AllLevelManager] Reloading scene: '{current}'");
            SceneManager.LoadScene(current);
        }

        // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug Commands ©¤©¤©¤©¤©¤©¤©¤©¤©¤

        private void RegisterDebugCommands()
        {
            if (DebugConsoleManager.Instance == null) return;

            DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
                "levelmgr-ld",
                "Load a scene by name. Usage: levelmgr-ld <sceneName>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        Debug.LogWarning("[AllLevelManager] levelmgr-ld requires a scene name argument.");
                        return;
                    }

                    LoadScene(args[0]);
                }
            ));

            DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
                "levelmgr-reload",
                "Reload the current scene. Usage: levelmgr-reload",
                args => ReloadCurrentScene()
            ));
        }
    }
}

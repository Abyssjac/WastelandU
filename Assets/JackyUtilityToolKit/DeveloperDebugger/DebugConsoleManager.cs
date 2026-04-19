using System;
using System.Collections.Generic;
using UnityEngine;

namespace JackyUtility
{
    public class DebugConsoleManager : MonoBehaviour
    {
        public static DebugConsoleManager Instance { get; private set; }

        private Dictionary<string, DebugCommand> commandMap = new();
        public event Action<bool> OnConsoleToggled;

        // ---- IDebuggable registry ----
        private Dictionary<string, IDebuggable> debugTargetMap = new Dictionary<string, IDebuggable>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterBuiltInCommands();
        }


        // ===================== Command Registry =====================
        public void RegisterCommand(DebugCommand command)
        {
            if (commandMap.ContainsKey(command.CommandId))
            {
                Debug.LogWarning($"Command already exists: {command.CommandId}");
                return;
            }

            commandMap.Add(command.CommandId, command);
        }

        public void ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            string[] split = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
                return;

            string commandId = split[0];
            string[] args = new string[split.Length - 1];
            Array.Copy(split, 1, args, 0, args.Length);

            if (commandMap.TryGetValue(commandId, out var command))
            {
                try
                {
                    command.Execute?.Invoke(args);
                    Debug.Log($"<color=yellow>Debug Terminal Executed command:</color> {commandId}; {input}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"<color=yellow>Debug Terminal Command execution failed:</color> {commandId}\n{e}");
                }
            }
            else
            {
                Debug.LogWarning($"<color=yellow>Debug Terminal Unknown command:</color> {commandId}");
            }
        }

        // ===================== IDebuggable Registry =====================

        public void RegisterDebugTarget(IDebuggable target)
        {
            if (target == null) return;

            string id = target.DebugId.ToLowerInvariant();
            if (debugTargetMap.ContainsKey(id))
            {
                Debug.LogWarning($"[DebugConsoleManager] Debug target already registered: {id}");
                return;
            }

            debugTargetMap.Add(id, target);
            Debug.Log($"[DebugConsoleManager] Debug target registered: {id}");
        }

        public void UnregisterDebugTarget(IDebuggable target)
        {
            if (target == null) return;

            string id = target.DebugId.ToLowerInvariant();
            if (debugTargetMap.Remove(id))
            {
                Debug.Log($"[DebugConsoleManager] Debug target unregistered: {id}");
            }
        }

        // ===================== Built-in Commands =====================

        private void RegisterBuiltInCommands()
        {
            RegisterCommand(new DebugCommand(
                "ls",
                "List all debug commands",
                args =>
                {
                    foreach (var pair in commandMap)
                    {
                        Debug.Log($"{pair.Key} - {pair.Value.CommandDescription}");
                    }
                }
            ));

            // ---- Unified debug toggle command ----
            RegisterCommand(new DebugCommand(
                "debug",
                "Toggle debug gizmos. Usage: debug <targetId> <true/false>  |  debug ls (list all targets)",
                args =>
                {
                    if (args.Length == 0)
                    {
                        Debug.LogWarning("Usage: debug <targetId> <true/false>  |  debug ls");
                        return;
                    }

                    // Sub-command: list all registered debug targets
                    if (args[0].Equals("ls", StringComparison.OrdinalIgnoreCase))
                    {
                        if (debugTargetMap.Count == 0)
                        {
                            Debug.Log("[debug] No debug targets registered.");
                            return;
                        }

                        foreach (var pair in debugTargetMap)
                        {
                            Debug.Log($"  {pair.Key} ¡ª enabled: {pair.Value.DebugEnabled}");
                        }
                        return;
                    }

                    if (args.Length < 2)
                    {
                        Debug.LogWarning("Usage: debug <targetId> <true/false>");
                        return;
                    }

                    string targetId = args[0].ToLowerInvariant();
                    if (!debugTargetMap.TryGetValue(targetId, out var target))
                    {
                        Debug.LogWarning($"[debug] Unknown target: {targetId}. Use 'debug ls' to list all.");
                        return;
                    }

                    if (bool.TryParse(args[1], out bool value))
                    {
                        target.DebugEnabled = value;
                        Debug.Log($"[debug] {targetId} debug set to {value}");
                    }
                    else
                    {
                        Debug.LogWarning($"[debug] Invalid bool value: {args[1]}. Use true/false.");
                    }
                }
            ));

            //OnConsoleToggled.Invoke(false);
            InvokeOnConsoleToggled(false);
        }

        public void InvokeOnConsoleToggled(bool isOpen)
        {
            OnConsoleToggled?.Invoke(isOpen);
        }
    }
    [Serializable]
    public class DebugCommand
    {
        public string CommandId;
        [TextArea] public string CommandDescription;
        public Action<string[]> Execute;

        public DebugCommand(string commandId, string description, Action<string[]> execute)
        {
            CommandId = commandId;
            CommandDescription = description;
            Execute = execute;
        }
    }

    /// <summary>
    /// Implement this interface on any MonoBehaviour that has a debug toggle
    /// controllable via the debug terminal.
    /// </summary>
    public interface IDebuggable
    {
        /// <summary>
        /// Short identifier used in the terminal command, e.g. "buildpos", "emissionturret".
        /// Must be unique across all IDebuggable instances.
        /// </summary>
        string DebugId { get; }

        bool DebugEnabled { get; set; }
    }
}
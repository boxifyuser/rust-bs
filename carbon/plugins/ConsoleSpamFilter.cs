using System;
using HarmonyLib;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Console Spam Filter", "Server", "1.1.2")]
    [Description("Silencia spam do console (interest zone / navmesh)")]
    public class ConsoleSpamFilter : RustPlugin
    {
        private const string HarmonyId = "com.bichosolto.consolespamfilter";
        private Harmony _harmony;

        private static readonly string[] BlockContains =
        {
            "interest zone",
            "returning default interest",
            "returning default interest zone",
            "not on navmesh after a warp",
            "Failed to create agent because it is not close enough to the NavMesh",
            "No navmesh areas matching agent type"
        };

        private void Init()
        {
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(ConsoleSpamFilter).Assembly);
                Puts("Filtro ativo: interest zone.");
            }
            catch (Exception ex)
            {
                PrintError($"Falha Harmony: {ex.Message}");
            }
        }

        private void Unload()
        {
            try { _harmony?.UnpatchAll(HarmonyId); }
            catch { /* ignored */ }
        }

        internal static bool ShouldBlock(object message)
        {
            if (message == null)
                return false;

            var s = message.ToString();
            if (string.IsNullOrEmpty(s))
                return false;

            for (var i = 0; i < BlockContains.Length; i++)
            {
                if (s.IndexOf(BlockContains[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.Log), new[] { typeof(object) })]
        private static class Patch_Log
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.Log), new[] { typeof(object), typeof(UnityEngine.Object) })]
        private static class Patch_LogContext
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object) })]
        private static class Patch_LogWarning
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object), typeof(UnityEngine.Object) })]
        private static class Patch_LogWarningContext
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.LogError), new[] { typeof(object) })]
        private static class Patch_LogError
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }

        [HarmonyPatch(typeof(Debug), nameof(Debug.LogError), new[] { typeof(object), typeof(UnityEngine.Object) })]
        private static class Patch_LogErrorContext
        {
            private static bool Prefix(object message) => !ShouldBlock(message);
        }
    }
}

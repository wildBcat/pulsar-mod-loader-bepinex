using System;
using HarmonyLib;
using PulsarModLoader.Chat.Commands;
using UnityEngine;

namespace PulsarModLoader.Utilities
{
    [HarmonyPatch(typeof(PLNetworkManager), "Start")]
    class ExceptionWarningPatch
    {
        private static void Prefix()
        {
            Application.logMessageReceived += OnUnityLog;
        }

        public static void LogException(Exception ex)
        {
            string id = Guid.NewGuid().ToString().Substring(0, 8); // Or whatever generates the ID
            Logger.Info($"Exception ID: {id}");
            Logger.Info($"Message: {ex.Message}");
            Logger.Info($"Stack Trace: {ex.StackTrace}");
        }

        // Modify the OnUnityLog method to use LogException
        private static void OnUnityLog(string line, string stackTrace, LogType type)
        {
            if (type.Equals(LogType.Exception))
            {
                // Generate the exception ID
                string id = String
                    .Format("{0:X}", DateTime.UtcNow.GetHashCode())
                    .Substring(0, 7)
                    .ToUpper();

                // Create the message with color formatting for Unity's log
                string msg =
                    $"<color='#{ColorUtility.ToHtmlStringRGB(Color.red)}'>Exception!</color> {id}";

                // Send notification if in debug mode and local player exists
                if (
                    PMLConfig.DebugMode
                    && PLNetworkManager.Instance != null
                    && PLNetworkManager.Instance.LocalPlayer != null
                )
                {
                    Messaging.Notification(msg);
                }

                // Log the exception using the new method
                // Assuming `ex` is available, if not, you'll need to capture it properly (from the 'line' or 'stackTrace')
                LogException(new Exception(line)); // Replace with actual exception details if possible

                // If you want to continue logging some additional details manually:
                Logger.Info($"Exception ID: {id}");
                Logger.Info($"Message: {line}");
                Logger.Info($"Stack Trace: {stackTrace}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using PulsarModLoader.Utilities;

namespace PulsarModLoader.Patches
{
    [HarmonyPatch(typeof(NetworkingPeer), "ExecuteRpc")]
    class AllowPMLRPCPatch
    {
        static bool PatchMethod(bool ShouldContinue, string MethodName)
        {
            // Define allowed RPCs
            string[] allowedRpcs = new[]
            {
                "ReceiveMessage",
                "ClientRecieveModList", // Note: Typo? Should be "ClientReceiveModList"?
                "ServerRecieveModList", // Note: Typo? Should be "ServerReceiveModList"?
                "ClientRequestModList",
            };

            // Check if this is an allowed RPC
            if (allowedRpcs.Contains(MethodName))
            {
                Logger.Info($"Allowing RPC: {MethodName}");
                return true;
            }

            // Default to true for vanilla RPCs, no logging unless denied (none here)
            return true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instrList = instructions.ToList();
            Logger.Info("Attempting to patch ExecuteRpc...");

            // Find where MethodName is set (stloc.2)
            int matchIndex = -1;
            for (int i = 0; i < instrList.Count; i++)
            {
                if (instrList[i].opcode == OpCodes.Stloc_2) // MethodName assignment
                {
                    matchIndex = i;
                    Logger.Info($"Found stloc.2 at IL_{i:D4}");
                    break;
                }
            }

            if (matchIndex >= 0)
            {
                Logger.Info("Injecting patch...");
                var injectedSequence = new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldloc_S, (byte)10), // Load ShouldContinue (local 10)
                    new CodeInstruction(OpCodes.Ldloc_2), // Load MethodName
                    new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(AllowPMLRPCPatch), "PatchMethod")
                    ),
                    new CodeInstruction(
                        OpCodes.Stloc_S,
                        (byte)10
                    ) // Store result back to ShouldContinue
                    ,
                };
                instrList.InsertRange(matchIndex + 1, injectedSequence);
                Logger.Info("Patch applied successfully");
            }
            else
            {
                Logger.Info("Failed to find stloc.2 for MethodName");
            }

            return instrList;
        }
    }
}

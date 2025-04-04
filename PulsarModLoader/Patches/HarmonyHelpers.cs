﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using PulsarModLoader.Utilities;

namespace PulsarModLoader.Patches
{
    /// <summary>
    /// Provides helper functions for Harmony Transpilers.
    /// </summary>
    public static class HarmonyHelpers
    {
        /// <summary>
        /// Modifies instructions targetSequence with patchSequence based on input PatchMode and CheckMode.
        /// </summary>
        /// <param name="instructions">Input pre-modified Transpiler Instructions here</param>
        /// <param name="targetSequence">Targeted Instructions collection</param>
        /// <param name="patchSequence">Inserted Instructions collection</param>
        /// <param name="patchMode"></param>
        /// <param name="checkMode"></param>
        /// <param name="showDebugOutput"></param>
        /// <returns>Modified instructions based on input values</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <summary>
        /// Modifies instructions targetSequence with patchSequence based on input PatchMode and CheckMode.
        /// </summary>
        /// <param name="instructions">Input pre-modified Transpiler Instructions here</param>
        /// <param name="targetSequence">Targeted Instructions collection</param>
        /// <param name="patchSequence">Inserted Instructions collection</param>
        /// <param name="patchMode"></param>
        /// <param name="checkMode"></param>
        /// <param name="showDebugOutput"></param>
        /// <returns>Modified instructions based on input values</returns>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<CodeInstruction> PatchBySequence(
            IEnumerable<CodeInstruction> instructions,
            IEnumerable<CodeInstruction> targetSequence,
            IEnumerable<CodeInstruction> patchSequence,
            PatchMode patchMode = PatchMode.AFTER,
            CheckMode checkMode = CheckMode.ALWAYS,
            bool showDebugOutput = false
        )
        {
            List<CodeInstruction> Instructions = instructions.ToList(); // Create new list to be modified and returned.
            List<CodeInstruction> TargetSequenceList = targetSequence.ToList();
            int targetSize = TargetSequenceList.Count;

            for (int i = 0; i < Instructions.Count; i++) // Check every Instruction in the given list
            {
                bool targetSequenceStillFits = i + targetSize <= Instructions.Count; // Calculate if target sequence fits in Instructions.

                if (targetSequenceStillFits) // Stop if not enough lines capable of fitting target sequence
                {
                    bool foundTargetSequence = true;

                    for (int x = 0; x < targetSize && foundTargetSequence; x++) // Compare each element
                    {
                        var instr = Instructions[i + x];
                        var target = TargetSequenceList[x];

                        // HarmonyX tolerance: allow branch opcode inversion
                        if (
                            (instr.opcode == OpCodes.Brtrue || instr.opcode == OpCodes.Brtrue_S)
                                && (
                                    target.opcode == OpCodes.Brfalse
                                    || target.opcode == OpCodes.Brfalse_S
                                )
                            || (
                                instr.opcode == OpCodes.Brfalse || instr.opcode == OpCodes.Brfalse_S
                            )
                                && (
                                    target.opcode == OpCodes.Brtrue
                                    || target.opcode == OpCodes.Brtrue_S
                                )
                        )
                        {
                            continue; // Skip opcode check for inverted branches
                        }

                        foundTargetSequence = instr.opcode.Equals(target.opcode);
                        if (checkMode != CheckMode.NEVER) // If specified checking params are set appropriately, check operand
                        {
                            foundTargetSequence =
                                foundTargetSequence
                                && (
                                    (
                                        (instr.operand == null || checkMode == CheckMode.NONNULL)
                                        && target.operand == null
                                    )
                                    || instr.operand?.Equals(target.operand) == true
                                );
                        }

                        if (showDebugOutput && foundTargetSequence)
                        {
                            Logger.Info($"Found {target.opcode} at {i + x}");
                        }
                    }

                    if (foundTargetSequence) // If the TargetSequence was found in the Instructions, apply patch
                    {
                        if (patchMode == PatchMode.BEFORE || patchMode == PatchMode.AFTER)
                        {
                            int indexToInsertAt = patchMode == PatchMode.AFTER ? i + targetSize : i;
                            Instructions.InsertRange(
                                indexToInsertAt,
                                patchSequence.Select(c => c.FullClone())
                            );
                        }
                        else if (patchMode == PatchMode.REPLACE)
                        {
                            Instructions.RemoveRange(i, targetSize);
                            Instructions.InsertRange(i, patchSequence.Select(c => c.FullClone()));
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"Argument PatchMode patchMode == {patchMode}; invalid value!"
                            );
                        }

                        break;
                    }
                }
                else // If target sequence didn't fit (couldn't find target sequence)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(
                        $"Failed to patch by sequence: couldn't find target sequence. This might be okay in certain cases."
                    );
                    sb.AppendLine($"Stack Trace:");
                    string[] stackTrace = new System.Diagnostics.StackTrace()
                        .ToString()
                        .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int lineNumber = 0; lineNumber < 2; lineNumber++)
                    {
                        sb.AppendLine(stackTrace[lineNumber]);
                    }
                    Logger.Info(sb.ToString());
                    break;
                }
            }

            return Instructions.AsEnumerable();
        }

        /// <summary>
        /// Returns the index of the last instruction in targetSquence, or -1 if not found
        /// </summary>
        /// <param name="instructions"></param>
        /// <param name="targetSequence"></param>
        /// <param name="checkMode"></param>
        /// <param name="showDebugOutput"></param>
        /// <returns></returns>
        public static int FindSequence(
            IEnumerable<CodeInstruction> instructions,
            IEnumerable<CodeInstruction> targetSequence,
            CheckMode checkMode = CheckMode.ALWAYS,
            bool showDebugOutput = false
        )
        {
            List<CodeInstruction> Instructions = instructions.ToList();

            CodeInstruction targetStart = targetSequence.ElementAt(0);
            int targetSize = targetSequence.Count();

            for (int i = 0; i < Instructions.Count; i++)
            {
                bool targetSequenceStillFits = i + targetSize <= Instructions.Count;

                if (targetSequenceStillFits)
                {
                    bool foundTargetSequence = true;

                    for (int x = 0; x < targetSize && foundTargetSequence; x++)
                    {
                        foundTargetSequence = Instructions[i + x]
                            .opcode.Equals(targetSequence.ElementAt(x).opcode);
                        if (checkMode != CheckMode.NEVER) //check that target sequence matches.
                        {
                            foundTargetSequence =
                                foundTargetSequence
                                && (
                                    (
                                        Instructions[i + x].operand == null
                                        || checkMode == CheckMode.NONNULL
                                    )
                                        && targetSequence.ElementAt(x).operand == null
                                    || Instructions[i + x]
                                        .operand.Equals(targetSequence.ElementAt(x).operand)
                                );
                        }

                        if (showDebugOutput && foundTargetSequence)
                        {
                            Logger.Info($"Found {targetSequence.ElementAt(x).opcode} at {i + x}");
                        }
                    }

                    if (foundTargetSequence)
                    {
                        return i + targetSize;
                    }
                }
                else
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine(
                        $"Couldn't find target sequence.  This might be okay in certain cases."
                    );

                    // Cut down the stack trace because it's 20 lines of unhelpful reflection internals.
                    // Show enough to figure out which mod + transpiler method is causing this:
                    sb.AppendLine($"Stack Trace:");
                    string[] stackTrace = new System.Diagnostics.StackTrace()
                        .ToString()
                        .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int lineNumber = 0; lineNumber < 2; lineNumber++)
                    {
                        sb.AppendLine(stackTrace[lineNumber]);
                    }

                    Logger.Info(sb.ToString());
                    break;
                }
            }

            return -1;
        }

        public enum CheckMode
        {
            /// <summary>
            /// Target opperands ALWAYS need to match
            /// </summary>
            ALWAYS,

            /// <summary>
            /// Target opperands can be NULL to match. This is good if you don't know what to put in for 1 of multiple instruction operands.
            /// </summary>
            /*
            Match for NONNULL, ALWAYS, and NEVER
            instructions:
                stfld PLMouselook::minimumY
                br 600
                ldarg.0

            target:
                stfld PLMouselook::minimumY
                br 600
                ldarg.0

            Match for NONNULL and NEVER
            instructions:
                stfld PLMouselook::minimumY
                br 600
                ldarg.0zzz

            target:
                stfld
                br 600
                ldarg.0

            Match for NEVER
            instructions:
                stfld PLMouselook::minimumY
                br 600
                ldarg.0

            target:
                stfld Not-The-Same-instruction
                br 600
                ldarg.0
            */
            NONNULL,

            /// <summary>
            /// Target opperands NEVER need to match
            /// </summary>
            NEVER,
        }

        public enum PatchMode
        {
            /// <summary>
            /// impliment new code BEFORE target code
            /// </summary>
            BEFORE,

            /// <summary>
            /// impliment new code AFTER target code
            /// </summary>
            AFTER,

            /// <summary>
            /// REPLACE target code with new code
            /// </summary>
            REPLACE,
        }

        /// <summary>
        /// Logs the string form of an IEnumerable sequence to ease debugging.
        /// </summary>
        /// <param name="label">Text to display before the sequence.</param>
        /// <param name="sequence">Sequence to display, one element per line.</param>
        public static void LogSequence(string label, IEnumerable sequence)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(label);
            foreach (object c in sequence)
            {
                sb.AppendLine($"\t{c.ToString()}");
            }

            Logger.Info(sb.ToString());
        }

        /// <summary>
        /// Deep-copies the instruction, including labels and exception blocks.
        /// </summary>
        /// <param name="instruction">The instruction to fully clone.</param>
        /// <returns>Fully cloned instruction.</returns>
        public static CodeInstruction FullClone(this CodeInstruction instruction)
        {
            CodeInstruction clone = instruction.Clone();
            clone.labels = instruction.labels.ConvertAll(l => l); // TODO: Clone labels?
            clone.blocks = instruction.blocks.ConvertAll(b => b.Clone());

            return clone;
        }

        /// <summary>
        /// Deep-copies the exception block.
        /// </summary>
        /// <param name="block">The exception block to clone.</param>
        /// <returns>The cloned exception block.</returns>
        public static ExceptionBlock Clone(this ExceptionBlock block)
        {
            return new ExceptionBlock(block.blockType, block.catchType);
        }
    }
}

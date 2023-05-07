#nullable enable
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using static IPA.Logging.Logger;

namespace IPA.Logging
{
    internal class StdoutInterceptor : TextWriter
    {
        private const ConsoleColor defaultColor = ConsoleColor.Gray;

        private static StdoutInterceptor? stdoutInterceptor;
        private static StdoutInterceptor? stderrInterceptor;

        private static Harmony? harmony;
        private static bool usingInterceptor;

        private static int harmonyLoggingInited;
        private readonly object bufferLock = new();
        private ConsoleColor currentColor = defaultColor;

        private bool isStdErr;

        private string lineBuffer = "";
        public override Encoding Encoding => Encoding.Default;

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void Write(string value)
        {
            lock (bufferLock)
            {
                // avoid threading issues
                lineBuffer += value;

                string[]? parts = lineBuffer.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i + 1 == parts.Length) // last element
                    {
                        lineBuffer = parts[i];
                    }
                    else
                    {
                        string? str = parts[i];
                        if (string.IsNullOrEmpty(str))
                        {
                            continue;
                        }

                        if (!isStdErr && WinConsole.IsInitialized)
                        {
                            str = ConsoleColorToForegroundSet(currentColor) + str;
                        }

                        if (isStdErr)
                        {
                            stdout.Error(str);
                        }
                        else
                        {
                            stdout.Info(str);
                        }
                    }
                }
            }
        }

        internal static string ConsoleColorToForegroundSet(ConsoleColor col)
        {
            if (!WinConsole.UseVTEscapes)
            {
                return "";
            }

            string code = "0"; // reset

            switch (col)
            {
                case ConsoleColor.Black:
                    code = "30";
                    break;
                case ConsoleColor.DarkBlue:
                    code = "34";
                    break;
                case ConsoleColor.DarkGreen:
                    code = "32";
                    break;
                case ConsoleColor.DarkCyan:
                    code = "36";
                    break;
                case ConsoleColor.DarkRed:
                    code = "31";
                    break;
                case ConsoleColor.DarkMagenta:
                    code = "35";
                    break;
                case ConsoleColor.DarkYellow:
                    code = "33";
                    break;
                case ConsoleColor.Gray:
                    code = "37";
                    break;
                case ConsoleColor.DarkGray:
                    code = "90"; // literally bright black
                    break;
                case ConsoleColor.Blue:
                    code = "94";
                    break;
                case ConsoleColor.Green:
                    code = "92";
                    break;
                case ConsoleColor.Cyan:
                    code = "96";
                    break;
                case ConsoleColor.Red:
                    code = "91";
                    break;
                case ConsoleColor.Magenta:
                    code = "95";
                    break;
                case ConsoleColor.Yellow:
                    code = "93";
                    break;
                case ConsoleColor.White:
                    code = "97";
                    break;
            }

            return "\x1b[" + code + "m";
        }

        public static void Intercept()
        {
            if (!usingInterceptor)
            {
                usingInterceptor = true;

                EnsureHarmonyLogging();

                HarmonyGlobalSettings.DisallowLegacyGlobalUnpatchAll = true;
                harmony ??= new Harmony("BSIPA Console Redirector Patcher");
                stdoutInterceptor ??= new StdoutInterceptor();
                stderrInterceptor ??= new StdoutInterceptor { isStdErr = true };

                RedirectConsole();
                ConsoleHarmonyPatches.Patch(harmony);
            }
        }

        public static void RedirectConsole()
        {
            if (usingInterceptor)
            {
                Console.SetOut(stdoutInterceptor);
                Console.SetError(stderrInterceptor);
            }
        }

        // I'm not completely sure this is the best place for this, but whatever
        internal static void EnsureHarmonyLogging()
        {
            if (Interlocked.Exchange(ref harmonyLoggingInited, 1) != 0)
            {
                return;
            }

            HarmonyLib.Tools.Logger.ChannelFilter =
                HarmonyLib.Tools.Logger.LogChannel.All & ~HarmonyLib.Tools.Logger.LogChannel.IL;
            HarmonyLib.Tools.Logger.MessageReceived += (s, e) =>
            {
                string? msg = e.Message;
                Level lvl = e.LogChannel switch
                {
                    HarmonyLib.Tools.Logger.LogChannel.None => Level.Notice,
                    HarmonyLib.Tools.Logger.LogChannel.Info => Level.Trace, // HarmonyX logs a *lot* of Info messages
                    HarmonyLib.Tools.Logger.LogChannel.IL => Level.Trace,
                    HarmonyLib.Tools.Logger.LogChannel.Warn => Level.Warning,
                    HarmonyLib.Tools.Logger.LogChannel.Error => Level.Error,
                    HarmonyLib.Tools.Logger.LogChannel.Debug => Level.Debug,
                    HarmonyLib.Tools.Logger.LogChannel.All => Level.Critical,
                    _ => Level.Critical
                };
                Logger.Harmony.Log(lvl, msg);
            };
        }

        private static class ConsoleHarmonyPatches
        {
            public static void Patch(Harmony harmony)
            {
                Type? console = typeof(Console);
                MethodInfo? resetColor = console.GetMethod("ResetColor");
                PropertyInfo? foregroundProperty = console.GetProperty("ForegroundColor");
                MethodInfo? setFg = foregroundProperty?.GetSetMethod();
                MethodInfo? getFg = foregroundProperty?.GetGetMethod();

                try
                {
                    if (resetColor != null)
                    {
                        _ = harmony.Patch(resetColor,
                            transpiler: new HarmonyMethod(typeof(ConsoleHarmonyPatches), nameof(PatchResetColor)));
                    }

                    if (foregroundProperty != null)
                    {
                        _ = harmony.Patch(setFg,
                            transpiler: new HarmonyMethod(typeof(ConsoleHarmonyPatches),
                                nameof(PatchSetForegroundColor)));
                        _ = harmony.Patch(getFg,
                            transpiler: new HarmonyMethod(typeof(ConsoleHarmonyPatches),
                                nameof(PatchGetForegroundColor)));
                    }
                }
                catch (Exception e)
                {
                    // Harmony might be fucked because of wierdness in Guid.NewGuid, don't let that kill us
                    Default.Error("Error installing harmony patches to intercept Console color properties:");
                    Default.Error(e);
                }
            }

            public static ConsoleColor GetColor()
            {
                return stdoutInterceptor!.currentColor;
            }

            public static void SetColor(ConsoleColor col)
            {
                stdoutInterceptor!.currentColor = col;
            }

            public static void ResetColor()
            {
                stdoutInterceptor!.currentColor = defaultColor;
            }

            public static IEnumerable<CodeInstruction> PatchGetForegroundColor(IEnumerable<CodeInstruction> _)
            {
                MethodInfo? getColorM = typeof(ConsoleHarmonyPatches).GetMethod("GetColor");
                return new[]
                {
                    new CodeInstruction(OpCodes.Tailcall), new CodeInstruction(OpCodes.Call, getColorM),
                    new CodeInstruction(OpCodes.Ret)
                };
            }

            public static IEnumerable<CodeInstruction> PatchSetForegroundColor(IEnumerable<CodeInstruction> _)
            {
                MethodInfo? setColorM = typeof(ConsoleHarmonyPatches).GetMethod("SetColor");
                return new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Tailcall),
                    new CodeInstruction(OpCodes.Call, setColorM), new CodeInstruction(OpCodes.Ret)
                };
            }

            public static IEnumerable<CodeInstruction> PatchResetColor(IEnumerable<CodeInstruction> _)
            {
                MethodInfo? resetColor = typeof(ConsoleHarmonyPatches).GetMethod("ResetColor");
                return new[]
                {
                    new CodeInstruction(OpCodes.Tailcall), new CodeInstruction(OpCodes.Call, resetColor),
                    new CodeInstruction(OpCodes.Ret)
                };
            }
        }
    }
}
#nullable enable
using IPA.Config;
using IPA.Utilities.Async;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Logger = IPA.Logging.Logger;
#if NET3
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Utilities
{
    /// <summary>
    ///     Provides some basic utility methods and properties of Beat Saber
    /// </summary>
    public static class UnityGame
    {
        /// <summary>
        ///     The different types of releases of the game.
        /// </summary>
        public enum Release
        {
            /// <summary>
            ///     Indicates a Steam release.
            /// </summary>
            Steam,

            /// <summary>
            ///     Indicates a non-Steam release.
            /// </summary>
            Other
        }

        private static AlmostVersion? _gameVersion;

        private static Thread? mainThread;
        private static Release? _releaseCache;

        private static string? _installRoot;

        /// <summary>
        ///     Provides the current game version.
        /// </summary>
        /// <value>the SemVer version of the game</value>
        public static AlmostVersion GameVersion => _gameVersion ??= new AlmostVersion(ApplicationVersionProxy);

        private static string ApplicationVersionProxy
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                try
                {
                    return Application.version;
                }
                catch (MissingMemberException ex)
                {
                    Logger.Default.Error("Tried to grab 'Application.version' too early, it's probably broken now.");
                    if (SelfConfig.Debug_.ShowHandledErrorStackTraces_)
                    {
                        Logger.Default.Error(ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Default.Error($"Error getting Application.version: {ex.Message}");
                    if (SelfConfig.Debug_.ShowHandledErrorStackTraces_)
                    {
                        Logger.Default.Error(ex);
                    }
                }

                return string.Empty;
            }
        }

        internal static bool IsGameVersionBoundary { get; private set; }
        internal static AlmostVersion? OldVersion { get; private set; }

        /// <summary>
        ///     Checks if the currently running code is running on the Unity main thread.
        /// </summary>
        /// <value><see langword="true" /> if the curent thread is the Unity main thread, <see langword="false" /> otherwise</value>
        public static bool OnMainThread => Environment.CurrentManagedThreadId == mainThread?.ManagedThreadId;

        /// <summary>
        ///     Gets the <see cref="Release" /> type of this installation of Beat Saber
        /// </summary>
        /// <remarks>
        ///     This only gives a
        /// </remarks>
        /// <value>the type of release this is</value>
        public static Release ReleaseType => _releaseCache ??= CheckIsSteam() ? Release.Steam : Release.Other;

        /// <summary>
        ///     Gets the path to the game's install directory.
        /// </summary>
        /// <value>the path of the game install directory</value>
        public static string InstallPath
        {
            get
            {
                if (_installRoot == null)
                {
                    _installRoot = Path.GetFullPath(
                        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", ".."));
                }

                return _installRoot;
            }
        }

        /// <summary>
        ///     The path to the `Libs` folder. Use only if necessary.
        /// </summary>
        /// <value>the path to the library directory</value>
        public static string LibraryPath => Path.Combine(InstallPath, "Libs");

        /// <summary>
        ///     The path to the `Libs\Native` folder. Use only if necessary.
        /// </summary>
        /// <value>the path to the native library directory</value>
        public static string NativeLibraryPath => Path.Combine(LibraryPath, "Native");

        /// <summary>
        ///     The directory to load plugins from.
        /// </summary>
        /// <value>the path to the plugin directory</value>
        public static string PluginsPath => Path.Combine(InstallPath, "Plugins");

        /// <summary>
        ///     The path to the `UserData` folder.
        /// </summary>
        /// <value>the path to the user data directory</value>
        public static string UserDataPath => Path.Combine(InstallPath, "UserData");

        internal static void SetEarlyGameVersion(AlmostVersion ver)
        {
            _gameVersion = ver;
            Logger.Default.Debug($"GameVersion set early to {ver}");
        }

        internal static void EnsureRuntimeGameVersion()
        {
            try
            {
                AlmostVersion? rtVer = new(ApplicationVersionProxy);
                if (!rtVer.Equals(_gameVersion)) // this actually uses stricter equality than == for AlmostVersion
                {
                    Logger.Default.Warn(
                        $"Early version {_gameVersion} parsed from game files doesn't match runtime version {rtVer}!");
                    _gameVersion = rtVer;
                }
            }
            catch (MissingMethodException e)
            {
                Logger.Default.Error("Application.version was not found! Cannot check early parsed version");
                if (SelfConfig.Debug_.ShowHandledErrorStackTraces_)
                {
                    Logger.Default.Error(e);
                }

                StackTrace? st = new();
                Logger.Default.Notice($"{st}");
            }
        }

        internal static void CheckGameVersionBoundary()
        {
            AlmostVersion? gameVer = GameVersion;
            string? lastVerS = SelfConfig.LastGameVersion_;
            OldVersion = lastVerS != null ? new AlmostVersion(lastVerS, gameVer) : null;

            IsGameVersionBoundary = OldVersion is not null && gameVer != OldVersion;

            SelfConfig.Instance.LastGameVersion = gameVer.ToString();
        }

        /// <summary>
        ///     Asynchronously switches the current execution context to the Unity main thread.
        /// </summary>
        /// <returns>An awaitable which causes any following code to execute on the main thread.</returns>
        public static SwitchToUnityMainThreadAwaitable SwitchToMainThreadAsync()
        {
            return default;
        }

        internal static void SetMainThread()
        {
            mainThread = Thread.CurrentThread;
        }

        private static bool CheckIsSteam()
        {
            DirectoryInfo? installDirInfo = new(InstallPath);
            return installDirInfo.Parent?.Name == "common"
                   && installDirInfo.Parent?.Parent?.Name == "steamapps";
        }
    }

    /// <summary>
    ///     An awaitable which, when awaited, switches the current context to the Unity main thread.
    /// </summary>
    /// <seealso cref="UnityGame.SwitchToMainThreadAsync" />
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types",
        Justification = "This type should never be compared.")]
    public struct SwitchToUnityMainThreadAwaitable
    {
        /// <summary>
        ///     Gets the awaiter for this awaitable.
        /// </summary>
        /// <returns>The awaiter for this awaitable.</returns>
        public SwitchToUnityMainThreadAwaiter GetAwaiter()
        {
            return default;
        }
    }

    /// <summary>
    ///     An awaiter which, when awaited, switches the current context to the Unity main thread.
    /// </summary>
    /// <seealso cref="UnityGame.SwitchToMainThreadAsync" />
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types",
        Justification = "This type should never be compared.")]
    public struct SwitchToUnityMainThreadAwaiter : INotifyCompletion, ICriticalNotifyCompletion
    {
        private static readonly ContextCallback InvokeAction = static o => ((Action)o!)();

        /// <summary>
        ///     Gets whether or not this awaiter is completed.
        /// </summary>
        public bool IsCompleted => UnityGame.OnMainThread;

        /// <summary>
        ///     Gets the result of this awaiter.
        /// </summary>
        public void GetResult() { }

        /// <summary>
        ///     Registers a continuation to be called when this awaiter finishes.
        /// </summary>
        /// <param name="continuation">The continuation.</param>
        public void OnCompleted(Action continuation)
        {
            ExecutionContext? ec = ExecutionContext.Capture();
            UnityMainThreadTaskScheduler.Default.QueueAction(() =>
                ExecutionContext.Run(ec, InvokeAction, continuation));
        }

        /// <summary>
        ///     Registers a continuation to be called when this awaiter finishes, without capturing the execution context.
        /// </summary>
        /// <param name="continuation">The continuation.</param>
        public void UnsafeOnCompleted(Action continuation)
        {
            UnityMainThreadTaskScheduler.Default.QueueAction(continuation);
        }
    }
}
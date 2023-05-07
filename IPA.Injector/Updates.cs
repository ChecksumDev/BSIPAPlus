#nullable enable
using IPA.AntiMalware;
using IPA.Config;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static IPA.Logging.Logger;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Injector
{
    internal static class Updates
    {
        private const string DeleteFileName = "$$delete";

        public static void InstallPendingUpdates()
        {
            InstallPendingSelfUpdates();
            InstallPendingModUpdates();
        }

        private static void InstallPendingSelfUpdates()
        {
            string? path = Path.Combine(UnityGame.InstallPath, "IPA.exe");
            if (!File.Exists(path))
            {
                return;
            }

            Version? ipaVersion = new(FileVersionInfo.GetVersionInfo(path).FileVersion);
            Version? selfVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (ipaVersion > selfVersion)
            {
                ScanResult scanResult = AntiMalwareEngine.Engine.ScanFile(new FileInfo(path));
                if (scanResult == ScanResult.Detected)
                {
                    Updater.Error("Scan of BSIPA installer found malware; not updating");
                    return;
                }

                if (!SelfConfig.AntiMalware_.RunPartialThreatCode_ &&
                    scanResult is not ScanResult.KnownSafe and not ScanResult.NotDetected)
                {
                    Updater.Error(
                        "Scan of BSIPA installer returned partial threat; not updating. To allow this, enable AntiMalware.RunPartialThreatCode in the config.");
                    return;
                }

                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = $"\"-nw={Process.GetCurrentProcess().Id}," +
                                $"s={string.Join(" ", Environment.GetCommandLineArgs().Skip(1).StrJP()).Replace("\\", "\\\\").Replace(",", "\\,")}\"",
                    UseShellExecute = false
                });

                Updater.Info("Updating BSIPA...");
                Environment.Exit(0);
            }
        }

        private static void InstallPendingModUpdates()
        {
            string? pendingDir = Path.Combine(UnityGame.InstallPath, "IPA", "Pending");
            if (!Directory.Exists(pendingDir))
            {
                return;
            }

            // there are pending updates, install
            Updater.Info("Installing pending updates");

            string[]? toDelete = Array.Empty<string>();
            string? delFn = Path.Combine(pendingDir, DeleteFileName);
            if (File.Exists(delFn))
            {
                toDelete = File.ReadAllLines(delFn);
                File.Delete(delFn);
            }

            foreach (string? file in toDelete)
            {
                try
                {
                    File.Delete(Path.Combine(UnityGame.InstallPath, file));
                }
                catch (Exception e)
                {
                    Updater.Error("While trying to install pending updates: Error deleting file marked for deletion");
                    Updater.Error(e);
                }
            }

            #region Self Protection

            string path;
            if (Directory.Exists(path = Path.Combine(pendingDir, "IPA")))
            {
                Stack<string>? dirs = new(20);

                dirs.Push(path);

                while (dirs.Count > 0)
                {
                    string? currentDir = dirs.Pop();
                    string[] subDirs;
                    string[] files;
                    try
                    {
                        subDirs = Directory.GetDirectories(currentDir);
                        files = Directory.GetFiles(currentDir);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Updater.Error(e);
                        continue;
                    }
                    catch (DirectoryNotFoundException e)
                    {
                        Updater.Error(e);
                        continue;
                    }

                    foreach (string? file in files)
                    {
                        try
                        {
                            if (!Utils.GetRelativePath(file, path).Split(Path.PathSeparator).Contains("Pending"))
                            {
                                File.Delete(file);
                            }
                        }
                        catch (FileNotFoundException e)
                        {
                            Updater.Error(e);
                        }
                    }

                    foreach (string? str in subDirs)
                    {
                        dirs.Push(str);
                    }
                }
            }

            if (File.Exists(path = Path.Combine(pendingDir, "IPA.exe")))
            {
                File.Delete(path);
                if (File.Exists(path = Path.Combine(pendingDir, "Mono.Cecil.dll")))
                {
                    File.Delete(path);
                }
            }

            #endregion

            try
            {
                Utils.CopyAll(new DirectoryInfo(pendingDir), new DirectoryInfo(UnityGame.InstallPath),
                    onCopyException: (e, f) =>
                    {
                        Updater.Error(
                            $"Error copying file {Utils.GetRelativePath(f.FullName, pendingDir)} from Pending:");
                        Updater.Error(e);
                        return true;
                    });
            }
            catch (Exception e)
            {
                Updater.Error("While trying to install pending updates: Error copying files in");
                Updater.Error(e);
            }

            try
            {
                Directory.Delete(pendingDir, true);
            }
            catch (Exception e)
            {
                Updater.Error("Something went wrong performing an operation that should never fail!");
                Updater.Error(e);
            }
        }
    }
}
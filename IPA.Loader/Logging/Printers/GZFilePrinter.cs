﻿#nullable enable
using Ionic.Zlib;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
#if NET3
using Net3_Proxy;
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Logging.Printers
{
    /// <summary>
    ///     A <see cref="LogPrinter" /> abstract class that provides the utilities to write to a GZip file.
    /// </summary>
    public abstract class GZFilePrinter : LogPrinter, IDisposable
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

#if NET4
        private const RegexOptions reOptions = RegexOptions.Compiled;
#elif NET3 // Needed because Compiled doesn't exist in Unity's .NET 3 runtime
        private const RegexOptions reOptions = RegexOptions.None;
#endif

        internal static Regex removeControlCodes = new("\x1b\\[\\d+m", reOptions);

        private FileInfo? fileInfo;

        /// <summary>
        ///     The <see cref="StreamWriter" /> that writes to the GZip file.
        /// </summary>
        /// <value>the writer to the underlying filestream</value>
        protected StreamWriter? FileWriter;

        private FileStream? fstream;

        /// <summary>
        ///     Gets the <see cref="FileInfo" /> for the file to write to.
        /// </summary>
        /// <returns>the file to write to</returns>
        protected abstract FileInfo GetFileInfo();

        private const string latestFormat = "_latest{0}";

        [MemberNotNull(nameof(fileInfo))]
        private void InitLog()
        {
            try
            {
                if (fileInfo == null)
                {
                    // first init
                    fileInfo = GetFileInfo();
                    string? ext = fileInfo.Extension;

                    FileInfo? symlink =
                        new(Path.Combine(fileInfo.DirectoryName ?? throw new InvalidOperationException(),
                            string.Format(latestFormat, ext)));
                    if (symlink.Exists)
                    {
                        symlink.Delete();
                    }

                    foreach (FileInfo? file in
                             fileInfo.Directory.EnumerateFiles("*.log", SearchOption.TopDirectoryOnly))
                    {
                        if (file.Equals(fileInfo))
                        {
                            continue;
                        }

                        if (file.Extension == ".gz")
                        {
                            continue;
                        }

                        CompressOldLog(file);
                    }

                    fileInfo.Create().Close();

                    try
                    {
                        if (!CreateHardLink(symlink.FullName, fileInfo.FullName, IntPtr.Zero))
                        {
                            int error = Marshal.GetLastWin32Error();
                            Logger.Default.Error($"Hardlink creation failed ({error})");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Default.Error("Error creating latest hardlink!");
                        Logger.Default.Error(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Default.Error("Error initializing log!");
                Logger.Default.Error(e);
                throw;
            }
        }

        private static async void CompressOldLog(FileInfo file)
        {
            try
            {
                Logger.Default.Debug($"Compressing log file {file}");

                FileInfo? newFile = new(file.FullName + ".gz");

                using (FileStream? istream = file.OpenRead())
                using (FileStream? ostream = newFile.Create())
                using (GZipStream? gz = new(ostream, CompressionMode.Compress, CompressionLevel.BestCompression, false))
                {
                    await istream.CopyToAsync(gz).ConfigureAwait(false);
                }

                file.Delete();
            }
            catch (Exception e)
            {
                Logger.Default.Error("Error compressing old log file:");
                Logger.Default.Error(e);
            }
        }

        /// <summary>
        ///     Called at the start of any print session.
        /// </summary>
        public sealed override void StartPrint()
        {
            InitLog();

            fstream = fileInfo.Open(FileMode.Append, FileAccess.Write);
            FileWriter = new StreamWriter(fstream, new UTF8Encoding(false));
        }

        /// <summary>
        ///     Called at the end of any print session.
        /// </summary>
        public sealed override void EndPrint()
        {
            FileWriter?.Flush();
            fstream?.Flush();
            FileWriter?.Dispose();
            fstream?.Dispose();
            FileWriter = null;
            fstream = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Disposes the file printer.
        /// </summary>
        /// <param name="disposing">does nothing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                FileWriter?.Flush();
                fstream?.Flush();
                FileWriter?.Close();
                fstream?.Close();
                FileWriter?.Dispose();
                fstream?.Dispose();
            }
        }
    }
}
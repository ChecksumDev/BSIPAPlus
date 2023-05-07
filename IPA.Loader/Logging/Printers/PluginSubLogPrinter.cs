using IPA.Utilities;
using System;
using System.IO;
#if NET3
using Path = Net3_Proxy.Path;
#endif

namespace IPA.Logging.Printers
{
    /// <summary>
    ///     Prints log messages to the file specified by the name.
    /// </summary>
    public class PluginSubLogPrinter : GZFilePrinter
    {
        private readonly string mainName;

        private readonly string name;

        /// <summary>
        ///     Creates a new printer with the given name.
        /// </summary>
        /// <param name="mainname">the name of the main logger</param>
        /// <param name="name">the name of the logger</param>
        public PluginSubLogPrinter(string mainname, string name)
        {
            this.name = name;
            mainName = mainname;
        }

        /// <summary>
        ///     Provides a filter for this specific printer.
        /// </summary>
        /// <value>the filter for this printer</value>
        public override Logger.LogLevel Filter { get; set; } = Logger.LogLevel.All;

        /// <summary>
        ///     Gets the <see cref="FileInfo" /> for the target file.
        /// </summary>
        /// <returns>the file to write to</returns>
        protected override FileInfo GetFileInfo()
        {
            DirectoryInfo logsDir = new(Path.Combine("Logs", mainName, name));
            logsDir.Create();
            FileInfo finfo = new(Path.Combine(logsDir.FullName, $"{Utils.CurrentTime():yyyy.MM.dd.HH.mm.ss}.log"));
            return finfo;
        }

        /// <summary>
        ///     Prints an entry to the associated file.
        /// </summary>
        /// <param name="level">the <see cref="Logger.Level" /> of the message</param>
        /// <param name="time">the <see cref="DateTime" /> the message was recorded at</param>
        /// <param name="logName">the name of the log that sent the message</param>
        /// <param name="message">the message to print</param>
        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            foreach (string line in removeControlCodes.Replace(message, "").Split(new[] { "\n", Environment.NewLine },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                FileWriter.WriteLine("[{2} @ {1:HH:mm:ss}] {0}", line, time, level.ToString().ToUpper());
            }
        }
    }
}
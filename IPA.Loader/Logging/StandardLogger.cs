﻿#nullable enable

using IPA.Config;
using IPA.Logging.Printers;
using IPA.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace IPA.Logging
{
    /// <summary>
    ///     The default (and standard) <see cref="Logger" /> implementation.
    /// </summary>
    /// <remarks>
    ///     <see cref="StandardLogger" /> uses a multi-threaded approach to logging. All actual I/O is done on another thread,
    ///     where all messaged are guaranteed to be logged in the order they appeared. It is up to the printers to format them.
    ///     This logger supports child loggers. Use <see cref="LoggerExtensions.GetChildLogger" /> to safely get a child.
    ///     The modification of printers on a parent are reflected down the chain.
    /// </remarks>
    public class StandardLogger : Logger
    {
        private const int LogCloseTimeout = 250;
        private static readonly List<LogPrinter> defaultPrinters = new();

        private static bool addedConsolePrinters;
        private static bool finalizedDefaultPrinters;
        private static bool showSourceClass;
        private static bool showTrace;
        private static volatile bool syncLogging;

        private static bool addedFilePrinter;

        [ThreadStatic] private static ManualResetEventSlim? threadSync;

        [ThreadStatic] private static bool? isOnLoggerThread;

        private static readonly ManualResetEventSlim logWaitEvent = new(true);
        private static readonly BlockingCollection<LogMessage> logQueue = new();
        private static Thread? logThread;

        private static StandardLogger? loggerLogger;

        private readonly Dictionary<string, StandardLogger> children = new();

        private readonly string logName;
        private readonly StandardLogger? parent;

        private readonly List<LogPrinter> printers = new();

        static StandardLogger()
        {
            ConsoleColorSupport();
        }

        private StandardLogger(StandardLogger parent, string subName)
        {
            logName = $"{parent.logName}/{subName}";
            this.parent = parent;
            printers = new List<LogPrinter>();
            if (SelfConfig.Debug_.CreateModLogs_ && !SelfConfig.Debug_.CondenseModLogs_)
            {
                printers.Add(new PluginSubLogPrinter(parent.logName, subName));
            }

            if (logThread == null || !logThread.IsAlive)
            {
                logThread = new Thread(LogThread);
                logThread.Start();
            }
        }

        internal StandardLogger(string name)
        {
            ConsoleColorSupport();
            if (!finalizedDefaultPrinters)
            {
                if (!addedConsolePrinters)
                {
                    AddDefaultPrinter(new ColorlessConsolePrinter());
                }

                finalizedDefaultPrinters = true;
            }

            logName = name;
            if (SelfConfig.Debug_.CreateModLogs_)
            {
                printers.Add(new PluginLogFilePrinter(name));
            }

            if (logThread == null || !logThread.IsAlive)
            {
                logThread = new Thread(LogThread);
                logThread.Start();
            }
        }

        /// <summary>
        ///     The <see cref="TextWriter" /> for writing directly to the console window, or stdout if no window open.
        /// </summary>
        /// <value>a <see cref="TextWriter" /> for the current primary text output</value>
        public static TextWriter ConsoleWriter { get; internal set; } = Console.Out;

        /// <summary>
        ///     All levels defined by this filter will be sent to loggers. All others will be ignored.
        /// </summary>
        /// <value>the global filter level</value>
        public static LogLevel PrintFilter { get; internal set; } = LogLevel.All;

        /// <summary>
        ///     Whether or not the calling thread is the logger thread.
        /// </summary>
        /// <value><see langword="true" /> if the current thread is the logger thread, <see langword="false" /> otherwise</value>
        public static bool IsOnLoggerThread =>
            isOnLoggerThread ??= Thread.CurrentThread.ManagedThreadId == logThread?.ManagedThreadId;

        internal static void ConsoleColorSupport()
        {
            if (!addedConsolePrinters && !finalizedDefaultPrinters && WinConsole.IsInitialized)
            {
                defaultPrinters.AddRange(new[]
                {
                    new ColoredConsolePrinter { Filter = LogLevel.TraceOnly, Color = ConsoleColor.DarkMagenta },
                    new ColoredConsolePrinter { Filter = LogLevel.DebugOnly, Color = ConsoleColor.Green },
                    new ColoredConsolePrinter { Filter = LogLevel.InfoOnly, Color = ConsoleColor.White },
                    new ColoredConsolePrinter { Filter = LogLevel.NoticeOnly, Color = ConsoleColor.Cyan },
                    new ColoredConsolePrinter { Filter = LogLevel.WarningOnly, Color = ConsoleColor.Yellow },
                    new ColoredConsolePrinter { Filter = LogLevel.ErrorOnly, Color = ConsoleColor.Red },
                    new ColoredConsolePrinter { Filter = LogLevel.CriticalOnly, Color = ConsoleColor.Magenta }
                });

                addedConsolePrinters = true;
            }
        }

        /// <summary>
        ///     Adds to the default printer pool that all printers inherit from. Printers added this way will be passed every
        ///     message from every logger.
        /// </summary>
        /// <param name="printer">the printer to add</param>
        internal static void AddDefaultPrinter(LogPrinter printer)
        {
            defaultPrinters.Add(printer);
        }

        /// <summary>
        ///     Configures internal debug settings based on the config passed in.
        /// </summary>
        internal static void Configure()
        {
            showSourceClass = SelfConfig.Debug_.ShowCallSource_;
            PrintFilter = SelfConfig.Debug_.ShowDebug_ ? LogLevel.All : LogLevel.InfoUp;
            showTrace = SelfConfig.Debug_.ShowTrace_;
            syncLogging = SelfConfig.Debug_.SyncLogging_;
            if (SelfConfig.CommandLineValues.WriteLogs && !addedFilePrinter)
            {
                addedFilePrinter = true;
                AddDefaultPrinter(new GlobalLogFilePrinter());
            }
        }

        /// <summary>
        ///     Gets a child printer with the given name, either constructing a new one or using one that was already made.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>a child <see cref="StandardLogger" /> with the given sub-name</returns>
        internal StandardLogger GetChild(string name)
        {
            if (!children.TryGetValue(name, out StandardLogger? child))
            {
                child = new StandardLogger(this, name);
                children.Add(name, child);
            }

            return child;
        }

        /// <summary>
        ///     Adds a log printer to the logger.
        /// </summary>
        /// <param name="printer">the printer to add</param>
        public void AddPrinter(LogPrinter printer)
        {
            printers.Add(printer);
        }

        /// <summary>
        ///     Logs a specific message at a given level.
        /// </summary>
        /// <param name="level">the message level</param>
        /// <param name="message">the message to log</param>
        public override void Log(Level level, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // FIXME: trace doesn't seem to ever actually appear
            if (!showTrace && level == Level.Trace)
            {
                return;
            }

            // make sure that the queue isn't being cleared
            logWaitEvent.Wait();
            try
            {
                bool sync = syncLogging && !IsOnLoggerThread;
                if (sync)
                {
                    threadSync ??= new ManualResetEventSlim();
                    threadSync.Reset();
                }

                logQueue.Add(new LogMessage
                {
                    Level = level,
                    Message = message,
                    Logger = this,
                    Time = Utils.CurrentTime(),
                    Sync = threadSync
                });

                if (sync)
                {
                    threadSync!.Wait();
                }
            }
            catch (InvalidOperationException)
            {
                // the queue has been closed, so we leave it
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     An override to <see cref="M:IPA.Logging.Logger.Debug(System.String)" /> which shows the method that called it.
        /// </summary>
        /// <param name="message">the message to log</param>
        public override void Debug(string message)
        {
            if (showSourceClass)
            {
                // add source to message
                StackFrame? stackFrame = new StackTrace(true).GetFrame(1);
                int lineNo = stackFrame.GetFileLineNumber();

                if (lineNo == 0)
                {
                    // no debug info
                    MethodBase? method = stackFrame.GetMethod();
                    string? paramString = string.Join(", ",
                        method.GetParameters().Select(p => p.ParameterType.FullName).StrJP());

                    message = $"{{{method.DeclaringType?.FullName}::{method.Name}({paramString})}} {message}";
                }
                else
                {
                    message = $"{{{stackFrame.GetFileName()}:{lineNo}}} {message}";
                }
            }

            base.Debug(message);
        }

        /// <summary>
        ///     The log printer thread for <see cref="StandardLogger" />.
        /// </summary>
        private static void LogThread()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                StopLogThread();
            };

            loggerLogger = new StandardLogger("Log Subsystem");
            loggerLogger.printers.Clear(); // don't need a log file for this one

            TimeSpan timeout = TimeSpan.FromMilliseconds(LogCloseTimeout);

            try
            {
                HashSet<LogPrinter>? started = new();
                while (logQueue.TryTake(out LogMessage msg, Timeout.Infinite))
                {
                    StdoutInterceptor.Intercept(); // only runs once, after the first message is queued
                    do
                    {
                        StandardLogger? logger = msg.Logger;
                        IEnumerable<LogPrinter> printers = logger.printers;
                        do
                        {
                            // aggregate all printers in the inheritance chain
                            logger = logger.parent;
                            if (logger != null)
                            {
                                printers = printers.Concat(logger.printers);
                            }
                        } while (logger != null);

                        foreach (LogPrinter? printer in printers.Concat(defaultPrinters))
                        {
                            try
                            {
                                // print to them all
                                if (((byte)msg.Level & (byte)printer.Filter) != 0)
                                {
                                    if (!started.Contains(printer))
                                    {
                                        // start printer if not started
                                        printer.StartPrint();
                                        _ = started.Add(printer);
                                    }

                                    // update last use time and print
                                    printer.LastUse = Utils.CurrentTime();
                                    printer.Print(msg.Level, msg.Time, msg.Logger.logName, msg.Message);
                                }
                            }
                            catch (Exception e)
                            {
                                // do something sane in the face of an error
                                Console.WriteLine($"printer errored: {e}");
                            }
                        }

                        msg.Sync?.Set();

                        SelfConfig.Debug_? debugConfig = SelfConfig.Instance?.Debug;

                        if (debugConfig != null && debugConfig.HideMessagesForPerformance
                                                && logQueue.Count > debugConfig.HideLogThreshold)
                        {
                            // spam filtering (if queue has more than HideLogThreshold elements)
                            logWaitEvent.Reset(); // pause incoming log requests

                            // clear loggers for this instance, to print the message to all affected logs
                            loggerLogger.printers.Clear();
                            HashSet<LogPrinter>? prints = new();
                            // clear the queue
                            while (logQueue.TryTake(out LogMessage message))
                            {
                                // aggregate loggers in the process
                                StandardLogger? messageLogger = message.Logger;
                                foreach (LogPrinter? print in messageLogger.printers)
                                {
                                    _ = prints.Add(print);
                                }

                                do
                                {
                                    messageLogger = messageLogger.parent;
                                    if (messageLogger != null)
                                    {
                                        foreach (LogPrinter? print in messageLogger.printers)
                                        {
                                            _ = prints.Add(print);
                                        }
                                    }
                                } while (messageLogger != null);

                                message.Sync?.Set();
                            }

                            // print using logging subsystem to all logger printers
                            loggerLogger.printers.AddRange(prints);
                            logQueue.Add(new LogMessage
                            {
                                // manually adding to the queue instead of using Warn() because calls to the logger are suspended here
                                Level = Level.Warning,
                                Logger = loggerLogger,
                                Message = $"{loggerLogger.logName.ToUpper()}: Messages omitted to improve performance",
                                Time = Utils.CurrentTime()
                            });

                            // resume log calls
                            logWaitEvent.Set();
                        }

                        DateTime now = Utils.CurrentTime();
                        List<LogPrinter>? copy = new(started);
                        foreach (LogPrinter? printer in copy)
                        {
                            // close printer after 500ms from its last use
                            if (now - printer.LastUse > timeout)
                            {
                                try
                                {
                                    printer.EndPrint();
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"printer errored: {e}");
                                }

                                _ = started.Remove(printer);
                            }
                        }
                    }
                    // wait for messages for 500ms before ending the prints
                    while (logQueue.TryTake(out msg, timeout));

                    if (logQueue.Count == 0)
                    {
                        // when the queue has been empty for 500ms, end all prints
                        foreach (LogPrinter? printer in started)
                        {
                            try
                            {
                                printer.EndPrint();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"printer errored: {e}");
                            }
                        }

                        started.Clear();
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>
        ///     Stops and joins the log printer thread.
        /// </summary>
        internal static void StopLogThread()
        {
            logQueue.CompleteAdding();
            logThread!.Join();
        }

        private struct LogMessage
        {
            public Level Level;
            public StandardLogger Logger;
            public string Message;
            public DateTime Time;
            public ManualResetEventSlim? Sync;
        }
    }

    /// <summary>
    ///     A class providing extensions for various loggers.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        ///     Gets a child logger, if supported. Currently the only defined and supported logger is <see cref="StandardLogger" />
        ///     , and most plugins will only ever receive this anyway.
        /// </summary>
        /// <param name="logger">the parent <see cref="Logger" /></param>
        /// <param name="name">the name of the child</param>
        /// <returns>the child logger</returns>
        public static Logger GetChildLogger(this Logger logger, string name)
        {
            return logger switch
            {
                StandardLogger l => l.GetChild(name),
                _ => throw new InvalidOperationException()
            };
        }
    }
}
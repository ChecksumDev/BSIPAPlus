#nullable enable
using IPA.Config;
using System;
using System.Runtime.InteropServices;

namespace IPA.Logging.Printers
{
    /// <summary>
    ///     Prints a pretty message to the console.
    /// </summary>
    public class ColoredConsolePrinter : LogPrinter
    {
        private static bool _haveReadDefaultColors;
        private static short _defaultColors;
        private readonly bool darkenMessages;

        private readonly bool darkenSetManually;


        public ColoredConsolePrinter() : this(SelfConfig.Debug_.DarkenMessages_)
        {
            darkenSetManually = false;
        }

        public ColoredConsolePrinter(bool darkenMessages)
        {
            darkenSetManually = true;
            this.darkenMessages = darkenMessages;
        }

        /// <summary>
        ///     A filter for this specific printer.
        /// </summary>
        /// <value>the filter to apply to this printer</value>
        public override Logger.LogLevel Filter { get; set; } = Logger.LogLevel.All;

        /// <summary>
        ///     The color to print messages as.
        /// </summary>
        /// <value>the color to print this message as</value>
        // Initializer calls this function because Unity's .NET 3.5 doesn't have the color properties on Console
        public ConsoleColor Color { get; set; } = GetConsoleColor(WinConsole.OutHandle);

        private static ConsoleColor GetDarkenedColor(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Gray => ConsoleColor.DarkGray,
                ConsoleColor.Blue => ConsoleColor.DarkBlue,
                ConsoleColor.Green => ConsoleColor.DarkGreen,
                ConsoleColor.Cyan => ConsoleColor.DarkCyan,
                ConsoleColor.Red => ConsoleColor.DarkRed,
                ConsoleColor.Magenta => ConsoleColor.DarkMagenta,
                ConsoleColor.Yellow => ConsoleColor.DarkYellow,
                ConsoleColor.White => ConsoleColor.Gray,
                _ => color
            };
        }

        /// <summary>
        ///     Prints an entry to the console window.
        /// </summary>
        /// <param name="level">the <see cref="Logger.Level" /> of the message</param>
        /// <param name="time">the <see cref="DateTime" /> the message was recorded at</param>
        /// <param name="logName">the name of the log that sent the message</param>
        /// <param name="message">the message to print</param>
        public override void Print(Logger.Level level, DateTime time, string logName, string message)
        {
            if (((byte)level & (byte)StandardLogger.PrintFilter) == 0)
            {
                return;
            }

            EnsureDefaultsPopulated(WinConsole.OutHandle);
            SetColor(Color, WinConsole.OutHandle);

            string? prefixStr = "";
            string? suffixStr = "";
            if ((darkenSetManually && darkenMessages) || (!darkenSetManually && SelfConfig.Debug_.DarkenMessages_))
            {
                ConsoleColor darkened = GetDarkenedColor(Color);
                if (darkened != Color)
                {
                    prefixStr = StdoutInterceptor.ConsoleColorToForegroundSet(darkened);
                    suffixStr = StdoutInterceptor.ConsoleColorToForegroundSet(Color);
                }
            }

            foreach (string? line in message.Split(new[] { "\n", Environment.NewLine },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                WinConsole.ConOut.WriteLine(Logger.LogFormat, prefixStr + line + suffixStr, logName, time,
                    level.ToString().ToUpperInvariant());
            }

            ResetColor(WinConsole.OutHandle);
        }

        private static void EnsureDefaultsPopulated(IntPtr handle, bool force = false)
        {
            if (!_haveReadDefaultColors | force)
            {
                _ = GetConsoleScreenBufferInfo(handle, out ConsoleScreenBufferInfo info);
                _defaultColors = (short)(info.Attribute & ~15);
                _haveReadDefaultColors = true;
            }
        }

        private static void ResetColor(IntPtr handle)
        {
            _ = GetConsoleScreenBufferInfo(handle, out ConsoleScreenBufferInfo info);
            short otherAttrs = (short)(info.Attribute & ~15);
            _ = SetConsoleTextAttribute(handle, (short)(otherAttrs | _defaultColors));
        }

        private static void SetColor(ConsoleColor col, IntPtr handle)
        {
            _ = GetConsoleScreenBufferInfo(handle, out ConsoleScreenBufferInfo info);
            short attr = GetAttrForeground(info.Attribute, col);
            _ = SetConsoleTextAttribute(handle, attr);
        }

        private static short GetAttrForeground(int attr, ConsoleColor color)
        {
            attr &= ~15;
            return (short)(attr | (int)color);
        }

        private static ConsoleColor GetConsoleColor(IntPtr handle)
        {
            _ = GetConsoleScreenBufferInfo(handle, out ConsoleScreenBufferInfo info);
            return (ConsoleColor)(info.Attribute & 15);
        }


        [DllImport("kernel32.dll", EntryPoint = "GetConsoleScreenBufferInfo", SetLastError = true,
            CharSet = CharSet.Unicode)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr handle, out ConsoleScreenBufferInfo info);

        [DllImport("kernel32.dll", EntryPoint = "SetConsoleTextAttribute", SetLastError = true,
            CharSet = CharSet.Unicode)]
        private static extern bool SetConsoleTextAttribute(IntPtr handle, short attribute);


        // ReSharper disable NotAccessedField.Local
#pragma warning disable 649
        private struct Coordinate
        {
            public short X;
            public short Y;
        }

        private struct SmallRect
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        private struct ConsoleScreenBufferInfo
        {
            public Coordinate Size;
            public Coordinate CursorPosition;
            public short Attribute;
            public SmallRect Window;
            public Coordinate MaxWindowSize;
        }
#pragma warning restore 649
        // ReSharper restore NotAccessedField.Local
    }
}
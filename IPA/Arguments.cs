using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA
{
    public class Arguments
    {
        public static readonly Arguments CmdLine = new(Environment.GetCommandLineArgs());
        private readonly List<ArgumentFlag> flagObjects = new();
        private readonly Dictionary<char, string?> flags = new();
        private readonly Dictionary<string, string?> longFlags = new();

        private readonly List<string> positional = new();

        private string[]? toParse;

        private Arguments(string[] args)
        {
            toParse = args.Skip(1).ToArray();
        }

        public IReadOnlyList<string> PositionalArgs => positional;

        public Arguments Flags(params ArgumentFlag[] toAdd)
        {
            foreach (ArgumentFlag? f in toAdd)
            {
                AddFlag(f);
            }

            return this;
        }

        public void AddFlag(ArgumentFlag toAdd)
        {
            if (toParse == null)
            {
                throw new InvalidOperationException();
            }

            flagObjects.Add(toAdd);
        }

        public void Process()
        {
            if (toParse == null)
            {
                throw new InvalidOperationException();
            }

            foreach (string? arg in toParse)
            {
                if (arg.StartsWith("--"))
                {
                    // parse as a long flag
                    string? name = arg.Substring(2); // cut off first two chars
                    string? value = null;

                    if (name.Contains('='))
                    {
                        string[]? spl = name.Split('=');
                        name = spl[0];
                        value = string.Join("=", spl, 1, spl.Length - 1);
                    }

                    longFlags.Add(name, value);
                }
                else if (arg.StartsWith("-"))
                {
                    // parse as flags
                    string? argument = arg.Substring(1); // cut off first char

                    StringBuilder? subBuildState = new();
                    bool parsingValue = false;
                    bool escaped = false;
                    char mainChar = ' ';
                    foreach (char chr in argument)
                    {
                        if (!parsingValue)
                        {
                            if (chr == '=')
                            {
                                parsingValue = true;
                            }
                            else
                            {
                                mainChar = chr;
                                flags.Add(chr, null);
                            }
                        }
                        else
                        {
                            if (!escaped)
                            {
                                if (chr == ',')
                                {
                                    parsingValue = false;
                                    flags[mainChar] = subBuildState.ToString();
                                    subBuildState = new StringBuilder();
                                    continue;
                                }

                                if (chr == '\\')
                                {
                                    escaped = true;
                                    continue;
                                }
                            }

                            _ = subBuildState.Append(chr);
                        }
                    }

                    if (parsingValue)
                    {
                        flags[mainChar] = subBuildState.ToString();
                    }
                }
                else
                {
                    // parse as positional
                    positional.Add(arg);
                }
            }

            toParse = null;

            foreach (ArgumentFlag? flag in flagObjects)
            {
                foreach (char charFlag in flag.ShortFlags)
                {
                    if (!(flag.exists_ = HasFlag(charFlag)))
                    {
                        continue;
                    }

                    flag.value_ = GetFlagValue(charFlag);
                    goto FoundValue; // continue to next flagObjects item
                }

                foreach (string? longFlag in flag.LongFlags)
                {
                    if (!(flag.exists_ = HasLongFlag(longFlag)))
                    {
                        continue;
                    }

                    flag.value_ = GetLongFlagValue(longFlag);
                    goto FoundValue; // continue to next flagObjects item
                }

                FoundValue: ;
            }
        }

        public bool HasLongFlag(string flag)
        {
            return longFlags.ContainsKey(flag);
        }

        public bool HasFlag(char flag)
        {
            return flags.ContainsKey(flag);
        }

        public string? GetLongFlagValue(string flag)
        {
            return longFlags[flag];
        }

        public string? GetFlagValue(char flag)
        {
            return flags[flag];
        }

        public void PrintHelp()
        {
            const string indent = "    ";
            string? filename = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            const string format = @"usage:
{2}{0} [FLAGS] [ARGUMENTS]

flags:
{1}";
            StringBuilder? flagsBuilder = new();
            foreach (ArgumentFlag? flag in flagObjects)
            {
                _ = flagsBuilder
                    .AppendFormat("{2}{0}{3}{1}",
                        string.Join(", ",
                            flag.ShortFlags.Select(s => $"-{s}").Concat(flag.LongFlags.Select(s => $"--{s}"))),
                        Environment.NewLine, indent, flag.ValueString != null ? "=" + flag.ValueString : "")
                    .AppendFormat("{2}{2}{0}{1}", flag.DocString, Environment.NewLine, indent);
            }

            Console.Write(format, filename, flagsBuilder, indent);
        }
    }

    public class ArgumentFlag
    {
        internal readonly List<string> LongFlags = new();
        internal readonly List<char> ShortFlags = new();
        internal bool exists_;

        internal string? value_;

        public ArgumentFlag(params string[] flags)
        {
            foreach (string? part in flags)
            {
                AddPart(part);
            }
        }

        public bool Exists => exists_;
        public string? Value => value_;

        public bool HasValue => Exists && Value != null;

        public string DocString { get; set; } = "";
        public string? ValueString { get; set; }

        private void AddPart(string flagPart)
        {
            if (flagPart.StartsWith("--"))
            {
                LongFlags.Add(flagPart.Substring(2));
            }
            else if (flagPart.StartsWith("-"))
            {
                ShortFlags.Add(flagPart[1]);
            }
        }

        public static implicit operator bool(ArgumentFlag f)
        {
            return f.Exists;
        }
    }
}
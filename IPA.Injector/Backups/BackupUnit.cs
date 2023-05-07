using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace IPA.Injector.Backups
{
    /// <summary>
    ///     A unit for backup. WIP.
    /// </summary>
    internal class BackupUnit
    {
        private const string ManifestFileName = "$manifest$.txt";

        private readonly DirectoryInfo _backupPath;
        private readonly HashSet<string> _files = new();
        private readonly FileInfo _manifestFile;

        public BackupUnit(string dir) : this(dir, Utils.CurrentTime().ToString("yyyy-MM-dd_h-mm-ss"))
        {
        }

        private BackupUnit(string dir, string name)
        {
            Name = name;
            _backupPath = new DirectoryInfo(Path.Combine(dir, Name));
            _manifestFile = new FileInfo(Path.Combine(_backupPath.FullName, ManifestFileName));
        }

        public string Name { get; }

        public static BackupUnit FromDirectory(DirectoryInfo directory, string dir)
        {
            BackupUnit unit = new(dir, directory.Name);

            // Read Manifest
            if (unit._manifestFile.Exists)
            {
                string manifest = File.ReadAllText(unit._manifestFile.FullName);
                foreach (string line in manifest.Split(new[] { Environment.NewLine, "\n", "\r" },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    unit._files.Add(line);
                }
            }
            else
            {
                foreach (FileInfo file in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    if (file.Name == ManifestFileName)
                    {
                        continue;
                    }

                    string relativePath = file.FullName.Substring(directory.FullName.Length + 1);
                    unit._files.Add(relativePath);
                }
            }

            return unit;
        }

        public void Add(string file)
        {
            Add(new FileInfo(file));
        }

        internal void Delete()
        {
            _backupPath.Delete(true);
        }

        /// <summary>
        ///     Adds a file to the list of changed files and backups it.
        /// </summary>
        /// <param name="file"></param>
        public void Add(FileInfo file)
        {
            string relativePath = Utils.GetRelativePath(file.FullName, Environment.CurrentDirectory);
            FileInfo backupPath = new(Path.Combine(_backupPath.FullName, relativePath));

            // Copy over
            backupPath.Directory?.Create();
            if (file.Exists)
            {
                if (File.Exists(backupPath.FullName))
                {
                    File.Delete(backupPath.FullName);
                }

                file.CopyTo(backupPath.FullName);
            }

            // Make empty file
            //backupPath.Create().Close();
            // do not do this because it can cause problems
            if (_files.Contains(relativePath))
            {
                return;
            }

            if (!File.Exists(_manifestFile.FullName))
            {
                _manifestFile.Create().Close();
            }

            StreamWriter stream = _manifestFile.AppendText();
            stream.WriteLine(relativePath);
            stream.Close();

            // Add to list
            _files.Add(relativePath);
        }
    }
}
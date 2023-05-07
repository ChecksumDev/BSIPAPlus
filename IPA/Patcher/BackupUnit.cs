using System;
using System.Collections.Generic;
using System.IO;

namespace IPA.Patcher
{
    /// <summary>
    ///     A unit for backup. WIP.
    /// </summary>
    public class BackupUnit
    {
        private static readonly string _ManifestFileName = "$manifest$.txt";

        private readonly DirectoryInfo _backupPath;
        private readonly PatchContext _context;
        private readonly List<string> _files = new();
        private readonly FileInfo _manifestFile;

        public BackupUnit(PatchContext context) : this(context, DateTime.Now.ToString("yyyy-MM-dd_h-mm-ss"))
        {
        }

        private BackupUnit(PatchContext context, string name)
        {
            Name = name;
            _context = context;
            _backupPath = new DirectoryInfo(Path.Combine(_context.BackupPath, Name));
            _manifestFile = new FileInfo(Path.Combine(_backupPath.FullName, _ManifestFileName));
        }

        private string Name { get; }

        public static BackupUnit FromDirectory(DirectoryInfo directory, PatchContext context)
        {
            BackupUnit? unit = new(context, directory.Name);

            // Read Manifest
            if (unit._manifestFile.Exists)
            {
                string? manifest = File.ReadAllText(unit._manifestFile.FullName);
                foreach (string? line in manifest.Split(new[] { Environment.NewLine },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    unit._files.Add(line);
                }
            }
            else
            {
                foreach (FileInfo? file in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    if (file.Name == _ManifestFileName)
                    {
                        continue;
                    }

                    string? relativePath = file.FullName.Substring(directory.FullName.Length + 1);
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
        /// <param name="file">the file to add</param>
        public void Add(FileInfo file)
        {
            if (!file.FullName.StartsWith(_context.ProjectRoot))
            {
                Console.Error.WriteLine("Invalid file path for backup! {0}", file);
                return;
            }

            string? relativePath = file.FullName.Substring(_context.ProjectRoot.Length + 1);
            FileInfo? backupPath = new(Path.Combine(_backupPath.FullName, relativePath));

            if (_files.Contains(relativePath))
            {
                Console.WriteLine("Skipping backup of {0}", relativePath);
                return;
            }

            // Copy over
            backupPath.Directory?.Create();
            if (file.Exists)
            {
                _ = file.CopyTo(backupPath.FullName);
            }

            // Make empty file
            //backupPath.Create().Close();
            // don't do this bc its dumb
            if (!File.Exists(_manifestFile.FullName))
            {
                _manifestFile.Create().Close();
            }

            StreamWriter? stream = _manifestFile.AppendText();
            stream.WriteLine(relativePath);
            stream.Close();

            // Add to list
            _files.Add(relativePath);
        }

        /// <summary>
        ///     Reverts the changes made in this unit.
        /// </summary>
        public void Restore()
        {
            foreach (string? relativePath in _files)
            {
                Console.WriteLine("Restoring {0}", relativePath);
                // Original version
                FileInfo? backupFile = new(Path.Combine(_backupPath.FullName, relativePath));
                FileInfo? target = new(Path.Combine(_context.ProjectRoot, relativePath));

                if (backupFile.Exists && backupFile.Length > 0)
                {
                    Console.WriteLine("  {0} => {1}", backupFile.FullName, target.FullName);
                    target.Directory?.Create();
                    _ = backupFile.CopyTo(target.FullName, true);
                }
                else
                {
                    Console.WriteLine("  x {0}", target.FullName);
                    if (target.Exists)
                    {
                        target.Delete();
                    }
                }
            }
        }
    }
}
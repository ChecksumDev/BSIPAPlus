using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using OgDir = System.IO.Directory;

namespace Net3_Proxy
{
    public static class Directory
    {
        public static void Move(string f, string t)
        {
            OgDir.Move(f, t);
        }

        public static string[] GetFiles(string d)
        {
            return OgDir.GetFiles(d);
        }

        public static string[] GetFiles(string d, string s)
        {
            return OgDir.GetFiles(d, s);
        }

        public static string[] GetFiles(string d, string s, SearchOption o)
        {
            return OgDir.GetFiles(d, s, o);
        }

        public static string[] GetDirectories(string d)
        {
            return OgDir.GetDirectories(d);
        }

        public static string[] GetDirectories(string d, string s)
        {
            return OgDir.GetDirectories(d, s);
        }

        public static string[] GetDirectories(string d, string s, SearchOption o)
        {
            return OgDir.GetDirectories(d, s, o);
        }

        public static bool Exists(string d)
        {
            return OgDir.Exists(d);
        }

        public static void Delete(string d)
        {
            OgDir.Delete(d);
        }

        public static void Delete(string d, bool r)
        {
            OgDir.Delete(d, r);
        }

        public static DirectoryInfo CreateDirectory(string d)
        {
            return OgDir.CreateDirectory(d);
        }

        public static DirectoryInfo CreateDirectory(string d, DirectorySecurity s)
        {
            return OgDir.CreateDirectory(d, s);
        }

        public static IEnumerable<string> EnumerateFiles(string d)
        {
            return GetFiles(d);
        }

        public static IEnumerable<string> EnumerateFiles(string d, string s)
        {
            return GetFiles(d, s);
        }

        public static IEnumerable<string> EnumerateFiles(string d, string s, SearchOption o)
        {
            return GetFiles(d, s, o);
        }

        public static IEnumerable<string> EnumerateDirectories(string d)
        {
            return GetDirectories(d);
        }

        public static IEnumerable<string> EnumerateDirectories(string d, string s)
        {
            return GetDirectories(d, s);
        }

        public static IEnumerable<string> EnumerateDirectories(string d, string s, SearchOption o)
        {
            return GetDirectories(d, s, o);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace More.Net
{
    public static class NfsPath
    {

        // Returns the share name, and also splits the sub directory list
        public static String SplitShareNameAndSubPath(String fullSharePath, out String subPath)
        {
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }
            if (fullSharePath.StartsWith("/"))
            {
                fullSharePath = fullSharePath.Substring(1);
            }
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }
            if (fullSharePath.EndsWith("/"))
            {
                fullSharePath = fullSharePath.Remove(fullSharePath.Length - 1);
            }
            if (fullSharePath == null || fullSharePath.Length == 0) { subPath = null; return null; }

            Int32 firstSlashIndex = fullSharePath.IndexOf('/');
            if (firstSlashIndex < 0)
            {
                subPath = null;
                return fullSharePath;
            }
            else
            {
                subPath = fullSharePath.Substring(firstSlashIndex + 1);
                return fullSharePath.Remove(firstSlashIndex);
            }
        }


        //
        // Returns the leaf of the path, or the path itself if there is no parent.
        // If there is no parent parent will be set to null
        //
        public static String LeafName(String path)
        {
            if (path == null || path.Length == 0) return null;

            // Remove ending '/'
            if(path[path.Length - 1] == '/')
            {
                path = path.Remove(path.Length - 1);
            }

            if (path == null || path.Length == 0) return null;


            // Find last '/' (skip current last element because it shouldn't be a /'/)
            for(int i = path.Length - 2; i >= 0; i--)
            {
                if(path[i] == '/')
                {
                    return path.Substring(i + 1);
                }
            }

            return path;
        }
    }
}

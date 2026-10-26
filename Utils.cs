using HActLib.YAct;
using ParLibrary;
using ParLibrary.Converter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yarhl;
using Yarhl.FileSystem;
using Yarhl.IO;

namespace TotalPorter
{
    internal static class Utils
    {
        public static void ExtractFromPar(string path, string outputFolder)
        {
            var parameters = new ParArchiveReaderParameters
            {
                Recursive = true
            };

            byte[] buf = File.ReadAllBytes(path);

            DataStream fact = DataStreamFactory.FromStream(new MemoryStream(buf));

            using Node par = NodeFactory.FromSubstream("file", fact, 0, buf.Length);
            par.TransformWith<ParArchiveReader, ParArchiveReaderParameters>(parameters);

            if(!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            ExtractFromPar(par, outputFolder);

            fact.Dispose();
        }

        public static void ExtractFromPar(Node parNode, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            foreach (Node node in Navigator.IterateNodes(parNode))
            {
                string fileInfoPath = node.Path.Substring(parNode.Path.Length).Replace('/', Path.DirectorySeparatorChar);
                string outputPath = Path.Join(outputFolder, fileInfoPath);

                var file = node.GetFormatAs<ParFile>();

                if (file == null)
                {
                    Directory.CreateDirectory(outputPath);
                    continue;
                }

                if (file.IsCompressed)
                {
                    node.TransformWith<ParLibrary.Sllz.Decompressor>();
                }

                if (node.Stream.Length > 0)
                {
                    node.Stream.WriteTo(outputPath);
                }
                else
                {
                    // Create empty file
                    File.Create(outputPath).Dispose();
                }

                File.SetCreationTime(outputPath, file.FileDate);
                File.SetLastWriteTime(outputPath, file.FileDate);
                File.SetAttributes(outputPath, (FileAttributes)file.Attributes);
            }


        }

        public static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public static FileInfo TryFindFile(string directory, string fileName)
        {
            FileInfo start = new FileInfo(Path.Combine(directory, fileName));

            if (start.Exists)
                return start;

            string[] dirs = Directory.GetDirectories(directory);

            foreach (string str in dirs)
            {
                FileInfo file = new FileInfo(Path.Combine(str, fileName));

                if (file.Exists)
                    return file;

                string[] subDirs = Directory.GetDirectories(str, "*", SearchOption.AllDirectories);

                foreach (string subDir in subDirs)
                {
                    file = new FileInfo(Path.Combine(subDir, fileName));

                    if (file.Exists)
                        return file;
                }
            }

            return null;
        }

        public static string ClearUnpackedPath(string path)
        {
            return path.Replace(".par", "").Replace(".unpack", "");
        }
    }
}


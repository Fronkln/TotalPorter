using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using MotionLib;

namespace TotalPorter
{
    internal class MBV
    {
        public static void Port(string inputDir, string targetDir, string inputGameS, string outputGameS, string inputFile, string outputDir)
        {
            string[] assets = GetDependencies(inputFile);

            DirectoryInfo motionFolder = new DirectoryInfo(inputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("motion"));

            string outputMotionPath = Path.Combine(outputDir, "motion");
            string outputGmtPath = Path.Combine(outputMotionPath, "gmt");
            string outputBepPath = Path.Combine(outputMotionPath, "bep");
            string outputPuidDir = "";

            DirectoryInfo[] inputDirs = new DirectoryInfo(inputDir).GetDirectories();
            DirectoryInfo[] targetDirs = new DirectoryInfo(targetDir).GetDirectories();

            DirectoryInfo targetPuidFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("puid"));

            if (targetPuidFolder.Exists)
                outputPuidDir = Path.Combine(outputDir, targetPuidFolder.Name);

            if (!Directory.Exists(outputMotionPath))
                Directory.CreateDirectory(outputMotionPath);

            if (!Directory.Exists(outputGmtPath))
                Directory.CreateDirectory(outputGmtPath);

            if (!Directory.Exists(outputBepPath))
                Directory.CreateDirectory(outputBepPath);

            if (!Directory.Exists(outputPuidDir))
                Directory.CreateDirectory(outputPuidDir);

            foreach(string str in assets)
            {
                string fileName = str + ".gmt";
                FileInfo file = Utils.TryFindFile(motionFolder.FullName, fileName);

                if(file != null)
                {
                    file.CopyTo(Path.Combine(outputGmtPath, fileName));
                }
            }
            
        }

        public static string[] GetDependencies(string mbvJson)
        {
            if (!File.Exists(mbvJson))
                return new string[0];

            List<string> assets = new List<string>();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {
                        JObject parent = (JObject)property.Parent;


                        if (property.Name.StartsWith("Anim Group"))
                        {
                            foreach (JProperty prop in property.Values())
                            {
                                if (prop.Name != "Frames")
                                {
                                    string val = prop.Value.ToString();
                                    if(val != "{}" && !string.IsNullOrEmpty(val))
                                        assets.Add(prop.Value.ToString());
                                }
                            }
                        }
                        else
                        {
                            if (parent.Path == property.Name)
                                assets.Add(property.Value.ToString());
                        }

                        // Recursively iterate if the value is an object or array
                        IterateJson(property.Value);
                    }
                }
                else if (token is JArray array)
                {
                    // Iterate through each element in the JArray
                    foreach (var item in array)
                    {
                        IterateJson(item); // Recursively iterate in case of nested structures
                    }
                }
                else
                {
                }
            }

            var jobject = JObject.Parse(File.ReadAllText(mbvJson));
            IterateJson(jobject);


            return assets.Distinct().ToArray();
        }
    }
}

using HActLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using LibARMP;
using LibARMP.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MotionLib;
using System.Xml.Linq;

namespace TotalPorter
{
    internal class OEToDECFC
    {
        static HashSet<string> convertedSets = new HashSet<string>();
        static Dictionary<string, uint> setMap = new Dictionary<string, uint>();

        static HashSet<FileInfo> gmtsToConvert = new HashSet<FileInfo>();
        static List<string> hactsToConvert = new List<string>();
        static List<string> newHactDirs = new List<string>();
        static HashSet<string> propNames = new HashSet<string>();
        static Dictionary<string, uint> newHActIds = new Dictionary<string, uint>();
        static List<string> copiedChpFiles = new List<string>();


        public static void Convert(string inputGameDir, string inputFile, string targetGameDir, string outputDir, Game inputGame, Game outputGame, bool removeSkillCond, bool removeStyleCond, bool removePlayerCond)
        {
            PortCFC(inputGameDir, inputFile, targetGameDir, outputDir, inputGame, outputGame, removeSkillCond, removeStyleCond, removePlayerCond);

            DirectoryInfo[] inputDirs = new DirectoryInfo(inputGameDir).GetDirectories();
            DirectoryInfo[] targetDirs = new DirectoryInfo(targetGameDir).GetDirectories();

            DirectoryInfo targetMotionFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("motion"));
            DirectoryInfo outputMotionFolder = new DirectoryInfo(Path.Combine(outputDir, "motion"));

            DirectoryInfo targetGmtFolder = targetMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("gmt"));
            DirectoryInfo targetBepFolder = targetMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("bep"));

            DirectoryInfo targetDBDir = targetDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
            DirectoryInfo targetPuidDir = targetDirs.FirstOrDefault(x => x.Name.StartsWith("puid"));

            FileInfo puidGmtFile = new FileInfo("placeholderrrrr");
            FileInfo puidBepFile = new FileInfo("placeholderrrrr");

            DirectoryInfo outputPuidDir = new DirectoryInfo(Path.Combine(outputDir, targetPuidDir.Name));

            if (targetPuidDir.Exists)
            {
                puidGmtFile = new FileInfo(Path.Combine(targetPuidDir.FullName, "motion_gmt.bin"));
                puidBepFile = new FileInfo(Path.Combine(targetPuidDir.FullName, "motion_bep.bin"));
            }

            ARMP targetPuidGmt = ArmpFileReader.ReadARMP(puidGmtFile.FullName);
            ARMP targetPuidBep = ArmpFileReader.ReadARMP(puidBepFile.FullName);

            DirectoryInfo outputGmtFolder = null;
            DirectoryInfo outputBepFolder = null;

            DirectoryInfo motionFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("motion"));

            FileInfo propertyFile = new FileInfo(Path.Combine(motionFolder.FullName, "property.bin"));

            if (!targetGmtFolder.Exists)
                outputGmtFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, "gmt"));
            else
                outputGmtFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, targetGmtFolder.Name));

            if (!targetBepFolder.Exists)
                outputBepFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, "bep"));
            else
                outputBepFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, targetBepFolder.Name));

            DirectoryInfo targetDbFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
            DirectoryInfo outputDbFolder = null;

            if (targetDbFolder.Exists)
            {
                outputDbFolder = new DirectoryInfo(Path.Combine(outputDir, targetDbFolder.Name));
            }
            else
                outputDbFolder = new DirectoryInfo(Path.Combine(outputDir, "db"));

            if (!outputDbFolder.Exists)
                outputDbFolder.Create();

            if (gmtsToConvert.Count > 0)
            {
                if (!outputMotionFolder.Exists)
                    outputMotionFolder.Create();

                if (!outputGmtFolder.Exists)
                    outputGmtFolder.Create();

                if (!outputBepFolder.Exists)
                    outputBepFolder.Create();

                HashSet<FileInfo> newGmtsToConvert = new HashSet<FileInfo>();

                DirectoryInfo tempGmtFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, "gmt_temp"));

                if (!tempGmtFolder.Exists)
                    tempGmtFolder.Create();

                foreach (FileInfo file in gmtsToConvert)
                {
                    string newPath = Path.Combine(tempGmtFolder.FullName, file.Name);
                    file.CopyTo(Path.Combine(newPath), true);

                    newGmtsToConvert.Add(new FileInfo(newPath));
                }

                gmtsToConvert = newGmtsToConvert;

                string gmtConverterArgs = $@"-ig {inputGame.ToString().ToLowerInvariant()} -og yk2 -dir -i ""{tempGmtFolder.FullName}"" -o ""{outputGmtFolder.FullName}"" --nosuffix -mtn";
                Process proc = Process.Start("GMT_Converter.exe", gmtConverterArgs);
                proc.WaitForExit();

                if (tempGmtFolder.Exists)
                    tempGmtFolder.Delete(true);
            }

            DirectoryInfo inputBattleDir = inputDirs.FirstOrDefault(x => x.Name.StartsWith("fighter"));
            DirectoryInfo outputBattleDir = new DirectoryInfo(outputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));
            string outputCfcPath;

            if (targetPuidDir.Exists)
            {
                if (!puidGmtFile.Exists || !puidBepFile.Exists || !propertyFile.Exists)
                {
                    if (!puidGmtFile.Exists)
                        Console.Write("Target GMT Puid does not exist. Cannot add GMTs to PUID.");

                    if (!puidBepFile.Exists)
                        Console.Write("Target BEP Puid does not exist. Cannot add BEPs to PUID.");

                    if (!propertyFile.Exists)
                        Console.Write("Input game Property.bin does not exist. Cannot convert move properties");

                }
                else
                {
                    if (!outputPuidDir.Exists)
                        outputPuidDir.Create();

                    OldEngineFormat prop = OldEngineFormat.Read(propertyFile.FullName);

                    foreach (OEAnimEntry entry in prop.Moves)
                    {
                        if (propNames.Contains(entry.Name))
                        {
                            BEP bep = new BEP();
                            bep.Game = outputGame;


                            foreach (OEAnimProperty property in entry.Properties)
                            {
                                Node node = OEToDEProperty.ConvertPropertyToNode(property, inputGame, outputGame);

                                if (node != null)
                                    bep.Nodes.Add(node);
                            }

                            BEP.Write(bep, Path.Combine(outputBepFolder.FullName, entry.Name + ".bep"), outputGame);

                            ArmpEntry bepEntry = null;

                            if (!targetPuidBep.MainTable.TryGetEntry(entry.Name, out bepEntry))
                                targetPuidBep.MainTable.AddEntry(entry.Name);
                        }
                    }


                    foreach (var file in gmtsToConvert)
                    {
                        string entryName = Path.GetFileNameWithoutExtension(file.Name);
                        ArmpEntry entry = null;

                        if (targetPuidGmt.MainTable.TryGetEntry(entryName, out entry))
                            continue;

                        targetPuidGmt.MainTable.AddEntry(entryName);
                    }

                    //only write if we modified anything
                    if (gmtsToConvert.Count > 0)
                    {
                        ArmpFileWriter.WriteARMPToFile(targetPuidGmt, Path.Combine(outputPuidDir.FullName, "motion_gmt.bin"));
                        ArmpFileWriter.WriteARMPToFile(targetPuidBep, Path.Combine(outputPuidDir.FullName, "motion_bep.bin"));
                    }
                }
            }
            else
            {
                Console.WriteLine("Target game does not have PUID folder. Will not be adding GMTs to PUID");
            }

            if (outputBattleDir != null)
            {
                DirectoryInfo cfcFolder = new DirectoryInfo(outputBattleDir.FullName).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("Fighter Command"));

                string cfcInfPath = Path.Combine(cfcFolder.FullName, "File Information.json");

                JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];

                outputCfcPath = Path.Combine(cfcFolder.FullName, fileInf["Files Directory"].ToString());

                uint highest = 0;

                foreach (var property in sets.Properties())
                {
                    // Check if the property value is an integer
                    if (property.Value.Type == JTokenType.Integer)
                    {
                        uint value = property.Value.Value<uint>();
                        if (value > highest)
                            highest = value;
                    }
                }

                foreach (string str in convertedSets)
                {
                    string newPath = Path.Combine(outputCfcPath, new FileInfo(str).Name);
                    File.Copy(str, newPath, true);
                    CFC.ConvertToDEFormat(newPath, inputGame);
                    CFC.AdjustCFCIDs(newPath, outputGame == Game.LADIW);
                    CFC.RemoveConditions(newPath, removeStyleCond, removeSkillCond, removePlayerCond);

                    string name = Path.GetFileNameWithoutExtension(newPath);

                    bool setExists = sets.ContainsKey(name);

                    if (!setExists)
                    {
                        highest++;
                        sets[name] = highest;
                        setMap[name] = highest;
                    }
                }

                File.WriteAllText(cfcInfPath, JsonConvert.SerializeObject(fileInf, Formatting.Indented));

                FileInfo commandSetBinPUID = TryFindFile(targetPuidDir.FullName, "battle_command_set.bin");
                FileInfo commandSetBinDB = TryFindFile(targetDBDir.FullName, "battle_command_set.bin");

                if (convertedSets.Count > 0 && commandSetBinPUID.Exists && commandSetBinPUID.Exists)
                {
                    ARMP commandSetPUIDArmp = ArmpFileReader.ReadARMP(commandSetBinPUID.FullName);
                    ARMP commandSetBinDBArmp = ArmpFileReader.ReadARMP(commandSetBinDB.FullName);

                    foreach (string str in convertedSets)
                    {
                        commandSetPUIDArmp.MainTable.AddEntry(Path.GetFileNameWithoutExtension(str));
                    }

                    foreach (var kv in setMap)
                    {
                        ArmpEntry mainEntry = commandSetBinDBArmp.MainTable.AddEntry();
                        ArmpEntry subTableEntry = commandSetBinDBArmp.MainTable.SubTable.AddEntry(kv.Key);
                        subTableEntry.SetValueFromColumn("0", kv.Value);
                        subTableEntry.SetValueFromColumn("2", mainEntry.ID);
                    }

                    //only write if we even made any changes to begin with
                    if (setMap.Count > 0)
                    {
                        ArmpFileWriter.WriteARMPToFile(commandSetPUIDArmp, Path.Combine(outputPuidDir.FullName, "battle_command_set.bin"));
                        ArmpFileWriter.WriteARMPToFile(commandSetBinDBArmp, Path.Combine(outputDbFolder.FullName, "battle_command_set.bin"));
                    }
                }
            }
            else
            {
                Console.WriteLine("Output directory does not have battle directory. Will not be adjusting cfcs");
            }


            if (hactsToConvert.Count > 0 && Program.ConvertHActFiles)
            {
                DirectoryInfo inputHactFolder = inputDirs.FirstOrDefault(X => X.Name.StartsWith("hact"));
                DirectoryInfo targetHactFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("hact"));

                if (inputHactFolder != null && targetHactFolder != null)
                {
                    DirectoryInfo outputHactFolder = new DirectoryInfo(Path.Combine(outputDir, targetHactFolder.Name));

                    if (!outputHactFolder.Exists)
                        outputHactFolder.Create();

                    foreach (string hactName in hactsToConvert)
                    {
                        string basePath = Path.Combine(inputHactFolder.FullName, hactName);
                        string parName = basePath += ".par";

                        string newPath = Path.Combine(outputHactFolder.FullName, hactName);

                        if (Directory.Exists(newPath))
                            Directory.Delete(newPath, true);

                        if (File.Exists(parName))
                        {
                            Utils.ExtractFromPar(parName, newPath);
                            Console.WriteLine("Extracted " + hactName);
                        }
                        else if (Directory.Exists(basePath))
                        {
                            Directory.Move(basePath, newPath);
                            Console.WriteLine("Moved " + hactName);
                        }
                        else
                        {
                            Console.WriteLine("Couldn't find " + hactName + " will not convert");
                        }

                        if (Directory.Exists(newPath))
                        {
                            foreach (string dir in Directory.GetDirectories(newPath, "*.par", SearchOption.AllDirectories))
                            {
                                string newDir = Path.Combine(newPath, new DirectoryInfo(dir).Name.Replace(".par", ""));

                                if (Directory.Exists(newDir))
                                    new DirectoryInfo(newDir).Delete(true);

                                Directory.Move(dir, newDir);
                            }


                            List<DirectoryInfo> dirsToDelete = new List<DirectoryInfo>();

                            foreach (string dir in Directory.GetDirectories(newPath, "*", SearchOption.AllDirectories))
                            {
                                DirectoryInfo dirinf = new DirectoryInfo(dir);

                                if (dirinf.Name == dirinf.Parent.Name)
                                {
                                    //gulp nested
                                    Utils.CopyFilesRecursively(dirinf.FullName, dirinf.Parent.FullName);
                                    dirsToDelete.Add(dirinf);
                                }
                            }

                            foreach (DirectoryInfo dir in dirsToDelete)
                                dir.Delete(true);

                            newHactDirs.Add(newPath);
                        }
                    }

                    for (int i = 0; i < newHactDirs.Count; i++)
                    {
                        string hactDir = newHactDirs[i];
                        string dirName = new DirectoryInfo(hactDir).Name;

                        Console.WriteLine("Converting " + dirName + $" ({i + 1} out of {newHactDirs.Count})");

                        string hactBridgeArgs = $@"""{hactDir}"" {inputGame.ToString().ToLowerInvariant()} {outputGame.ToString().ToLowerInvariant()}";

                        if (!Program.ConvertPib)
                            hactBridgeArgs += " -nopibs";

                        var proc = Process.Start("HActBridge/HActBridge.exe", hactBridgeArgs);
                        proc.WaitForExit();

                        string newDir = Path.Combine(outputHactFolder.FullName, dirName + "_" + CMN.GetVersionForGame(outputGame).ToString().ToLowerInvariant() + "_" + outputGame.ToString().ToLowerInvariant());
                        /*
                        if (Directory.Exists(newDir))
                        {
                            Directory.Delete(hactDir, true);
                            Directory.Move(newDir, hactDir);
                        }*/
                    }

                    for (int i = 0; i < newHactDirs.Count; i++)
                    {
                        string hactDir = newHactDirs[i];
                        string dirName = new DirectoryInfo(hactDir).Name;

                        string newDir = Path.Combine(outputHactFolder.FullName, dirName + "_" + CMN.GetVersionForGame(outputGame).ToString().ToLowerInvariant() + "_" + outputGame.ToString().ToLowerInvariant());
                        if (Directory.Exists(newDir))
                        {
                            Directory.Delete(hactDir, true);
                            Directory.Move(newDir, hactDir);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Input/target game does not have a hact folder. Will not convert hact files");
                }
            }

            if (hactsToConvert.Count > 0 && Program.ConvertHAct)
            {
                DirectoryInfo inputHactChpFolder = inputBattleDir.GetDirectories("*", SearchOption.AllDirectories).FirstOrDefault(x => x.Name.StartsWith("HAct"));
                DirectoryInfo outputHactChpFolder = outputBattleDir.GetDirectories("*", SearchOption.AllDirectories).FirstOrDefault(x => x.Name.StartsWith("HAct"));

                string inputChpPath = Path.Combine(inputHactChpFolder.FullName, "File Information.json");
                string outputChpPath = Path.Combine(outputHactChpFolder.FullName, "File Information.json");

                FileInfo talkParamFile = TryFindFile(targetDBDir.FullName, "talk_param.bin");

                bool outputUsesNumbers = outputGame >= Game.JE;

                if (File.Exists(inputChpPath) && File.Exists(outputChpPath) && talkParamFile.Exists)
                {
                    JObject inputFileInf = JObject.Parse(File.ReadAllText(inputChpPath));
                    JObject outputFileInf = JObject.Parse(File.ReadAllText(outputChpPath));
                    string inputJsonsDirName = inputFileInf["Files Directory"].ToString();
                    string outputJsonsDirName = outputFileInf["Files Directory"].ToString();
                    JObject inputSets = (JObject)inputFileInf["File Specific Info"]["Set Order"];
                    JObject outputSets = (JObject)outputFileInf["File Specific Info"]["Set Order"];

                    DirectoryInfo inputJsonsDir = new DirectoryInfo(Path.Combine(inputHactChpFolder.FullName, inputJsonsDirName));
                    DirectoryInfo outputJsonsDir = new DirectoryInfo(Path.Combine(outputHactChpFolder.FullName, outputJsonsDirName));


                    ARMP talkParamArmp = ArmpFileReader.ReadARMP(talkParamFile.FullName);

                    foreach (string str in hactsToConvert)
                    {
                        string name = str;

                        if (outputGame == Game.YK2)
                            name = "l2_" + name;

                        ArmpEntry entry = talkParamArmp.MainTable.AddEntry(name);

                        if (!outputUsesNumbers)
                        {
                            entry.Flags[1] = true;
                            entry.SetValueFromColumn("relative_play", (byte)1);
                            entry.SetValueFromColumn("draw_stop_to_play", (byte)1);
                        }

                        ArmpEntry template = talkParamArmp.MainTable.GetEntry(1);
                        string path = template.GetValueFromColumn("path").ToString().Replace(template.Name, "").Replace("talk_", "hact_").Replace("test/", "") + str;
                        entry.SetValueFromColumn("path", path);

                        newHActIds[str] = entry.ID;
                    }

                    ArmpFileWriter.WriteARMPToFile(talkParamArmp, Path.Combine(outputDbFolder.FullName, "talk_param.bin"));


                    if (inputJsonsDir.Exists)
                    {
                        foreach (string str in hactsToConvert)
                        {
                            FileInfo chpFile = new FileInfo(Path.Combine(inputJsonsDir.FullName, str + ".json"));

                            if (chpFile.Exists)
                            {
                                string outputPath = Path.Combine(outputHactChpFolder.FullName, "Extracted", str + ".json");
                                chpFile.CopyTo(outputPath, true);
                                CFC.RemoveConditions(outputPath, true, true, true);
                                //CFC.ConvertToDEFormat(outputPath, inputGame);
                                CHP.ConvertToDEFormat(outputPath, newHActIds[str], inputGame);

                                copiedChpFiles.Add(outputPath);

                                Console.WriteLine("Copied CHP HAct " + str);

                                if (!outputSets.ContainsKey(str))
                                    outputSets[str] = "";
                            }
                        }

                        File.WriteAllText(outputChpPath, JsonConvert.SerializeObject(outputFileInf, Formatting.Indented));
                    }

                    foreach (string str in copiedChpFiles)
                    {

                    }
                }
            }

        }

        private static void PortCFC(string inputGameDir, string inputFile, string targetGameDir, string outputDir, Game inputGame, Game outputGame, bool removeSkillCond, bool removeStyleCond, bool removePlayerCond)
        {
            if (convertedSets.Contains(inputFile))
                return;

            if (!File.Exists(inputFile))
                return;

            DirectoryInfo cfcFileDir = new FileInfo(inputFile).Directory;

            DirectoryInfo[] inputDirs = new DirectoryInfo(inputGameDir).GetDirectories();

            if (Program.ConvertMoves)
            {
                DirectoryInfo motionFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("motion"));

                string[] animationFiles = CFC.Parse(inputFile).Select(x => x += ".gmt").ToArray();

                foreach (string str in animationFiles.Where(x => gmtsToConvert.FirstOrDefault(y => y.Name == x) == null))
                {
                    FileInfo file = TryFindFile(motionFolder.FullName, str);

                    if (file == null)
                    {
                        Console.WriteLine("Couldn't find animation file " + str + ". Skipping...");
                        continue;
                    }

                    gmtsToConvert.Add(file);
                    propNames.Add(Path.GetFileNameWithoutExtension(file.FullName));
                }
            }

            if (Program.ConvertHAct || Program.ConvertHActFiles)
            {
                string[] hacts = CFC.ParseHActs(inputFile);

                //TODO: Fetch names from chp instead
                hactsToConvert.AddRange(hacts.Where(x => !hactsToConvert.Contains(x)));
            }

            convertedSets.Add(inputFile);

            if (Program.Recurse)
            {
                string[] deps = CFC.ParseDependingCFCs(inputFile);

                foreach (string str in deps)
                {
                    string path = Path.Combine(cfcFileDir.FullName, str);
                    PortCFC(inputGameDir, path, targetGameDir, outputDir, inputGame, outputGame, removeSkillCond, removeStyleCond, removePlayerCond);
                }
            }

            convertedSets.Add(inputFile);
        }

        //tries to find file recursively
        private static FileInfo TryFindFile(string directory, string fileName)
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

                foreach(string subDir in subDirs)
                {
                    file = new FileInfo(Path.Combine(subDir, fileName));

                    if(file.Exists)
                        return file;
                }
            }

            return null;
        }
    }
}

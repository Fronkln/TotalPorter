using HActLib;
using MotionLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PIBLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TotalPorter
{
    internal static class DEToOECFC
    {
        static HashSet<string> convertedSets = new HashSet<string>();

        static HashSet<FileInfo> gmtsToConvert = new HashSet<FileInfo>();
        static List<string> hactsToConvert = new List<string>();
        static List<string> newHactDirs = new List<string>();
        static HashSet<string> propNames = new HashSet<string>();

        private static bool m_bulkConvert = false;

        private static Game inputGamee;

        public static void Convert(string inputGameDir, string inputFile, string targetGameDir, string outputDir, Game inputGame, Game outputGame, bool removeSkillCond)
        {
            inputGamee = inputGame;
            
            PortCFC(inputGameDir, inputFile, targetGameDir, outputDir, inputGame, outputGame, removeSkillCond);

            DirectoryInfo[] inputDirs = new DirectoryInfo(inputGameDir).GetDirectories();
            DirectoryInfo[] targetDirs = new DirectoryInfo(targetGameDir).GetDirectories();

            DirectoryInfo inputMotionFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("motion"));
            DirectoryInfo targetMotionFolder = new DirectoryInfo(Path.Combine(targetGameDir, "motion"));
            DirectoryInfo outputMotionFolder = new DirectoryInfo(Path.Combine(outputDir, targetMotionFolder.Name));
            DirectoryInfo outputMotionAllFolder = new DirectoryInfo(Path.Combine(outputMotionFolder.FullName, "all"));

            DirectoryInfo inputGmtFolder = inputMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("gmt"));
            DirectoryInfo inputBepFolder = inputMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("bep"));

            if (gmtsToConvert.Count > 0 && IniSettings.ConvertGMTs)
            {
                if (!outputMotionAllFolder.Exists)
                    outputMotionAllFolder.Create();

                HashSet<FileInfo> newGmtsToConvert = new HashSet<FileInfo>();

                DirectoryInfo tempGmtFolder = new DirectoryInfo(Path.Combine(outputMotionAllFolder.FullName, "gmt_temp"));

                if (!tempGmtFolder.Exists)
                    tempGmtFolder.Create();

                foreach (FileInfo file in gmtsToConvert)
                {
                    string newPath = Path.Combine(tempGmtFolder.FullName, file.Name);
                    file.CopyTo(Path.Combine(newPath), true);

                    newGmtsToConvert.Add(new FileInfo(newPath));
                }


                gmtsToConvert = newGmtsToConvert;

                if (m_bulkConvert)
                {
                    string gmtConverterArgs = $@"-og {outputGame.ToString().ToLowerInvariant()} -ig yk2 -dir -i ""{tempGmtFolder.FullName}"" -o ""{outputMotionAllFolder.FullName}"" --nosuffix -mtn";
                    Process proc = Process.Start("GMT_Converter.exe", gmtConverterArgs);
                    proc.WaitForExit();
                }
                else
                {
                    foreach(FileInfo fileinfo in newGmtsToConvert)
                    {
                        Console.WriteLine("Converting " + fileinfo.Name);
                        string gmtConverterArgs = $@"-og {outputGame.ToString().ToLowerInvariant()} -ig yk2 -i ""{fileinfo.FullName}"" -o ""{Path.Combine(outputMotionAllFolder.FullName, fileinfo.Name)}"" --nosuffix";
                        Process proc = Process.Start("GMT_Converter.exe", gmtConverterArgs);
                        proc.WaitForExit();
                    }
                }

                if (tempGmtFolder.Exists)
                    tempGmtFolder.Delete(true);
            }

            DirectoryInfo inputBattleDir = new DirectoryInfo(inputGameDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));
            DirectoryInfo outputBattleDir = new DirectoryInfo(outputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("fighter"));
            DirectoryInfo outputPropertyDir = outputMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("property"));

            if (outputBattleDir != null)
                outputBattleDir = new DirectoryInfo(Path.Combine(outputBattleDir.FullName, "command"));

            string outputCfcPath;


            if (outputBattleDir != null)
            {
                DirectoryInfo cfcFolder = new DirectoryInfo(outputBattleDir.FullName).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("Fighter Command"));

                string cfcInfPath = Path.Combine(cfcFolder.FullName, "File Information.json");

                JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];

                outputCfcPath = Path.Combine(cfcFolder.FullName, fileInf["Files Directory"].ToString());

                foreach (string str in convertedSets)
                {
                    string newPath = Path.Combine(outputCfcPath, new FileInfo(str).Name);
                    File.Copy(str, newPath, true);

                    CFC.RemoveConditions(newPath, true, true, true, true);
                    CFC.AdjustCFCIDs(newPath, inputGame, outputGame);
                    CFC.ConvertToOEFormat(newPath, inputGame);

                    string name = Path.GetFileNameWithoutExtension(str);

                    if(!sets.ContainsKey(name))
                        sets[name] = "";
                }

                File.WriteAllText(cfcInfPath, JsonConvert.SerializeObject(fileInf, Formatting.Indented));
                Console.WriteLine("Wrote CFC inf " + cfcInfPath);
            }

            if(IniSettings.ConvertMoves && outputPropertyDir != null)
            {
                DirectoryInfo tableDat = new DirectoryInfo(Path.Combine(outputPropertyDir.FullName, "Table Data"));
                if (tableDat.Exists)
                {
                    string gmtDatPath = Path.Combine(tableDat.FullName, "GMT Block Data.json");
                    string strDatPath = Path.Combine(tableDat.FullName, "String Data.json");
                    string moveDatPath = Path.Combine(tableDat.FullName, "Move Block Data.json");

                    JObject gmtDat = JObject.Parse(File.ReadAllText(gmtDatPath));
                    JObject strDat = JObject.Parse(File.ReadAllText(strDatPath));
                    JObject moveDat = JObject.Parse(File.ReadAllText(moveDatPath));

                    int start = gmtDat.Count;
                    int start2 = strDat.Count + 1;

                    foreach (FileInfo str in gmtsToConvert)
                    {
                        string name = str.Name.Replace(".gmt", "");

                        if (gmtDat.ContainsKey(name))
                            continue;

                        gmtDat[start.ToString()] = name;
                        strDat[start2.ToString()] = name;
                        start++;
                        start2++;

                        if(true == true)
                        {
                            JObject moveBlockDat = new JObject();
                            JObject moveProps = new JObject();
                            moveProps["Property Unk"] = 0;

                            moveBlockDat["Unk 0"] = 16;
                            moveBlockDat["MEP Table ID"] = "";
                            moveBlockDat["Unk 1"] = 196608;
                            moveBlockDat["Unk 2"] = 16777472;
                            moveBlockDat["Unk 3"] = 0.0f;
                            moveBlockDat["Unk 4"] = 0.0f;
                            moveBlockDat["GMT Table ID"] = name;
                            moveBlockDat["Unk 6"] = 0.0f;
                            moveBlockDat["Unk 7"] = 0.0f;
                            moveBlockDat["Unk 8"] = 131072;
                            moveBlockDat["Unk 9"] = 83886080;
                            moveBlockDat["Unk 10"] = 0f;
                            moveBlockDat["Unk 11"] = 0f;

                            string bepFile = Path.Combine(inputBepFolder.FullName, name + ".bep");

                            if(File.Exists(bepFile))
                            {
                                BEP bep = BEP.Read(bepFile, inputGame);
                                JObject propertiesTable = new JObject();

                                NodeElement[] elements = bep.Nodes.Where(x => x is NodeElement).Cast<NodeElement>().ToArray();

                                for(int i = 0; i < elements.Length; i++)
                                {
                                    NodeElement element = elements[i];
                                    string nodeName = HActLib.Internal.Reflection.GetElementNameByID(element.ElementKind, inputGame);

                                    var createdProp = ElementToProp(element, nodeName);

                                    if (createdProp != null)
                                    {
                                        var type = createdProp["Property Data"]["Property ID Type"];
                                        propertiesTable["Property " + i + "| Type ID " + type.ToString() + " = " + nodeName] = createdProp;
                                    }
                                }

                                moveProps["Properties Table"] = propertiesTable;
                            }

                            moveDat[name] = new JObject();

                            moveDat[name]["Move Data"] = moveBlockDat;
                            moveDat[name]["Move Properties"] = moveProps;
                        }
   
                    }

                    File.WriteAllText(gmtDatPath, JsonConvert.SerializeObject(gmtDat, Formatting.Indented));
                    File.WriteAllText(strDatPath, JsonConvert.SerializeObject(strDat, Formatting.Indented));
                    File.WriteAllText(moveDatPath, JsonConvert.SerializeObject(moveDat, Formatting.Indented));

                    Console.WriteLine("Wrote GMT, String and Move Block");
                }
            }
        }

        private static JObject ElementToProp(NodeElement element, string name)
        {
            JObject createdProp = null;
            int propType = 0;
            bool timeOverriden = false;

            switch(name)
            {
                case "e_auth_element_battle_attack":
                    DETimingInfoAttack attackNode = element as DETimingInfoAttack;

                    createdProp = new JObject();
                    createdProp["Property Data"] = new JObject();

                    createdProp["Hitbox"] = new JObject();

                    int hitbox1 = 0;

                    BattleColliball deHitbox = (BattleColliball)attackNode.Data.Parts;

                    if (deHitbox.HasFlag(BattleColliball.ude1_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftArm);

                    if (deHitbox.HasFlag(BattleColliball.ude2_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftElbow);

                    if (deHitbox.HasFlag(BattleColliball.ude3_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftHand);

                    if (deHitbox.HasFlag(BattleColliball.ude1_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightArm);

                    if (deHitbox.HasFlag(BattleColliball.ude2_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightElbow);

                    if (deHitbox.HasFlag(BattleColliball.ude3_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightHand);

                    if (deHitbox.HasFlag(BattleColliball.asi1_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftThigh);

                    if (deHitbox.HasFlag(BattleColliball.asi2_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftKnee);

                    if (deHitbox.HasFlag(BattleColliball.asi3_l))
                        hitbox1 |= ((int)HitboxLocation1Flag.LeftFoot);

                    if (deHitbox.HasFlag(BattleColliball.asi1_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightThigh);

                    if (deHitbox.HasFlag(BattleColliball.asi2_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightKnee);

                    if (deHitbox.HasFlag(BattleColliball.asi3_r))
                        hitbox1 |= ((int)HitboxLocation1Flag.RightFoot);

                    if (deHitbox.HasFlag(BattleColliball.face))
                        hitbox1 |= ((int)HitboxLocation1Flag.Head);

                    int power = attackNode.Data.Power - 1;

                    if (power <= 0)
                        power = 1;
                    if (power > 4)
                        power = 4;

                    createdProp["Hitbox"]["HitboxLocation1"] = hitbox1;
                    createdProp["Hitbox"]["HitboxLocation2"] = 0;
                    createdProp["Hitbox"]["Hit Effect"] = 12;
                    createdProp["Hitbox"]["Hit Strength"] = power;
                    createdProp["Hitbox"]["Hit Location"] = 0;
                    createdProp["Hitbox"]["Flags"] = 255;
                    createdProp["Hitbox"]["Damage"] = attackNode.Data.Damage;
                    createdProp["Hitbox"]["Heat"] = 5;
                    createdProp["Hitbox"]["Hit Value"] = 2147483648;

                    propType = 5;
                    break;
                
                case "e_auth_element_se":
                    DEElementSE deSe = element as DEElementSE;

                    //fighter sound assumed
                    if(DEElementSE.IsSpecialCuesheet(deSe.CueSheet, inputGamee) && deSe.SoundIndex < 255)
                    {
                        createdProp = new JObject();
                        createdProp["Property Data"] = new JObject();
                        createdProp["Property Data"]["StartFrame"] = System.Convert.ToInt32(element.Start);
                        createdProp["Property Data"]["EndFrame"] = System.Convert.ToInt32(element.Start);
                        timeOverriden = true;

                        createdProp["Audio"] = new JObject();
                        createdProp["Audio"]["Voice Que"] = "0014";
                        createdProp["Audio"]["Sound Container ID"] = deSe.SoundIndex - 1;
                        createdProp["Audio"]["Unk 0"] = -1;
                        createdProp["Audio"]["Unk 1"] = -1;
                        createdProp["Audio"]["Unk 2"] = -1;
                        createdProp["Audio"]["Unk 3"] = -1;
                        createdProp["Audio"]["Unk 4"] = -1;
                        createdProp["Audio"]["Unk 5"] = -1;
                        createdProp["Audio"]["Unk 6"] = -1;
                        createdProp["Audio"]["Unk 7"] = -1;

                        propType = 1;

                    }
                    break;
                

                case "e_auth_element_battle_followup_window":
                    createdProp = new JObject();
                    createdProp["Property Data"] = new JObject();
                    propType = 3;
                    break;
                case "e_auth_element_battle_control_window":
                    createdProp = new JObject();
                    createdProp["Property Data"] = new JObject();
                    propType = 4;
                    break;
            }

            if(createdProp != null)
            {
                if (!timeOverriden)
                {
                    createdProp["Property Data"]["StartFrame"] = System.Convert.ToInt32(element.Start);
                    createdProp["Property Data"]["EndFrame"] = System.Convert.ToInt32(element.End);
                }
                createdProp["Property Data"]["Modifier"] = 0;
                createdProp["Property Data"]["Unk 1"] = 0;
                createdProp["Property Data"]["Unk 2"] = 0;
                createdProp["Property Data"]["Property ID Type"] = propType;
                createdProp["Property Data"]["Unk Value"] = 0;
            }

            return createdProp;
        }


        private static void PortCFC(string inputGameDir, string inputFile, string targetGameDir, string outputDir, Game inputGame, Game outputGame, bool removeSkillCond)
        {
            if (convertedSets.Contains(inputFile))
                return;

            if (!File.Exists(inputFile))
                return;

            DirectoryInfo cfcFileDir = new FileInfo(inputFile).Directory;

            DirectoryInfo[] inputDirs = new DirectoryInfo(inputGameDir).GetDirectories();

            if (IniSettings.ConvertMoves)
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

            convertedSets.Add(inputFile);

            if (IniSettings.Recurse)
            {
                string[] deps = CFC.ParseDependingCFCs(inputFile);

                foreach (string str in deps)
                {
                    string path = Path.Combine(cfcFileDir.FullName, str);
                    PortCFC(inputGameDir, path, targetGameDir, outputDir, inputGame, outputGame, removeSkillCond);
                }
            }

            convertedSets.Add(inputFile);
        }

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

                foreach (string subDir in subDirs)
                {
                    file = new FileInfo(Path.Combine(subDir, fileName));

                    if (file.Exists)
                        return file;
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HActLib;
using LibARMP;
using LibARMP.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ParLibrary.Converter;
using PIBLib;
using Yarhl.FileSystem;

namespace TotalPorter
{
    internal class Program
    {
        static HashSet<string> convertedSets = new HashSet<string>();
        static Dictionary<string, int> convertedSetsMap = new Dictionary<string, int>();
        static List<string> convertedAnims = new List<string>();
        static HashSet<string> convertedPibs = new HashSet<string>();
        static HashSet<string> convertedHActs = new HashSet<string>();
        static Dictionary<uint, uint> convertedSounds = new Dictionary<uint, uint>();
        static Dictionary<string, uint> pibVals = new Dictionary<string, uint>();
        static List<string> hactsToConvert = new List<string>();

        static void Main(string[] args)
        {
            if (args.Length <= 0)
                return;

            IniSettings.CreateIfNotPresent();
            IniSettings.Read();

            string mode;

            if (args.Length > 1)
                mode = args[0];
            else
                mode = IniSettings.Mode;

            switch (mode)
            {
                case "mbv":
                    string inputDirMBV = args[1];
                    string targetDirMBV = args[2];
                    string inputGameSMBV = args[3];
                    string outputGameSMBV = args[4];
                    string inputFileMBV = args[5];
                    string outputDirMBV = args[6];

                    MBV.Port(inputDirMBV, targetDirMBV, inputGameSMBV, outputGameSMBV, inputFileMBV, outputDirMBV);
                    break;

                case "cfc":
                    string inputDir;
                    string targetDir;
                    string inputGameS;
                    string outputGameS;
                    string inputFile;
                    string outputDir;

                    if (args.Length > 1)
                    {
                        inputDir = args[1];
                        targetDir = args[2];
                        inputGameS = args[3];
                        outputGameS = args[4];
                        inputFile = args[5];
                        outputDir = args[6];
                    }
                    else
                    {
                        inputDir = IniSettings.InputDir;
                        targetDir = IniSettings.TargetDir;
                        inputGameS = IniSettings.InputGame;
                        outputGameS = IniSettings.OutputGame;
                        inputFile = args[0];
                        outputDir = IniSettings.OutputDir;
                    }


                    if (string.IsNullOrEmpty(inputDir))
                    {
                        Console.WriteLine("Input directory is empty.");
                        Console.ReadKey();

                    }

                    if (string.IsNullOrEmpty(targetDir))
                    {
                        Console.WriteLine("Target directory is empty.");
                        Console.ReadKey();
                    }

                    if (string.IsNullOrEmpty(outputDir))
                    {
                        Console.WriteLine("Output directory is empty.");
                        Console.ReadKey();
                    }


                    bool removeSkillCond = args.Contains("-noskillcondition");
                    bool removeStyleCond = args.Contains("-nostylecondition");
                    bool removePlayerCond = args.Contains("-noplayercondition");

                    Game inputGame = CMN.GetGameFromString(inputGameS);
                    Game outputGame = CMN.GetGameFromString(outputGameS);

                    if(CMN.IsDEGame(inputGame) && CMN.IsOEGame(outputGame))
                    {
                        DEToOECFC.Convert(inputDir, inputFile, targetDir, outputDir, inputGame, outputGame, removeSkillCond);
                        break;
                    }

                    if(CMN.IsOEGame(inputGame) && CMN.IsDEGame(outputGame))
                    {
                        OEToDECFC.Convert(inputDir, inputFile, targetDir, outputDir, inputGame, outputGame, removeSkillCond, removeStyleCond, removePlayerCond);
                        break;
                    }

                    //DE to DE code below

                    DirectoryInfo[] inputDirs = new DirectoryInfo(inputDir).GetDirectories();
                    DirectoryInfo[] targetDirs = new DirectoryInfo(targetDir).GetDirectories();

                    DirectoryInfo dbFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
                    DirectoryInfo puidFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("puid"));

                    DirectoryInfo originalDbFolder = dbFolder;

                    if(originalDbFolder == null)
                    {
                        Console.WriteLine("Input DB folder does not exist.");
                        Console.ReadKey();
                    }

                    DirectoryInfo targetDbFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
                    DirectoryInfo targetPuidFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("puid"));

                    DirectoryInfo outputPuidDir = new DirectoryInfo(Path.Combine(outputDir, "puid"));

                    if (targetPuidFolder.Exists)
                        outputPuidDir = new DirectoryInfo(Utils.ClearUnpackedPath(Path.Combine(outputDir, targetPuidFolder.Name)));

                    if (!outputPuidDir.Exists)
                        outputPuidDir.Create();

                    PortCFC(inputDir, targetDir, inputGameS, outputGameS, inputFile, outputDir, true, args.Contains("-noskillcondition"), args.Contains("-nostylecondition"), args.Contains("-noplayercondition"));


                    DirectoryInfo outputDbFolder = null;

                    if (targetDbFolder.Exists)
                    {
                        outputDbFolder = new DirectoryInfo(Utils.ClearUnpackedPath(Path.Combine(outputDir, targetDbFolder.Name)));
                    }
                    else
                        outputDbFolder = new DirectoryInfo(Path.Combine(outputDir, "db"));

                    if (IniSettings.ConvertMoves)
                    {
                        Console.WriteLine("Adding commandsets to DB and PUID");

                        if (targetPuidFolder.Exists && outputPuidDir.Exists)
                        {
                            FileInfo targetCommandsetPUID = new FileInfo(Path.Combine(targetPuidFolder.FullName, "battle_command_set.bin"));

                            if (targetCommandsetPUID.Exists)
                            {
                                ARMP targetCommandSetPuidFile = ArmpFileReader.ReadARMP(targetCommandsetPUID.FullName);

                                foreach (var kv in convertedSets)
                                {
                                    string name = Path.GetFileNameWithoutExtension(kv);

                                    if(!targetCommandSetPuidFile.GetMainTable().TryGetEntry(name, out var table))
                                    {
                                        targetCommandSetPuidFile.GetMainTable().AddEntry(name);
                                        Console.WriteLine("Added " + name + " to PUID");
                                    }
                                }

                                ArmpFileWriter.WriteARMPToFile(targetCommandSetPuidFile, Path.Combine(outputPuidDir.FullName, "battle_command_set.bin"));
                            }
                            else
                            {
                                Console.WriteLine("Target game PUID does not have battle_command_set.bin. Will not add commandsets to PUID.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Target game/output game does not have PUID directories. Will not add commandsets to PUID.");
                        }

                        if (originalDbFolder.Exists)
                        {
                            if (targetDbFolder.Exists)
                            {
                                if (outputDbFolder.Exists)
                                {
                                    FileInfo inputCommandset = originalDbFolder.GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "battle_command_set.bin");
                                    FileInfo targetCommandset = targetDbFolder.GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "battle_command_set.bin");

                                    if (inputCommandset != null && targetCommandset != null)
                                    {
                                        ARMP inputCommandsetFile = ArmpFileReader.ReadARMP(inputCommandset.FullName);
                                        ARMP targetCommandsetFile = ArmpFileReader.ReadARMP(targetCommandset.FullName);


                                        foreach (var kv in convertedSetsMap)
                                        {
                                            /*
                                            ArmpEntry inputSubtableEntry = null;

                                            if(!inputCommandsetFile.GetMainTable().Indexer.TryGetEntry(kv.Value, out inputSubtableEntry))
                                            {
                                                Console.WriteLine("Couldn't find " + kv.Value + " on the DB subtable. Skipping");
                                                continue;
                                            }
                                            */

                                            ArmpEntry targetSubtableEntry = null;

                                            if (!targetCommandsetFile.GetMainTable().Indexer.TryGetEntry(kv.Key, out targetSubtableEntry))
                                            {
                                                targetSubtableEntry = targetCommandsetFile.GetMainTable().Indexer.AddEntry(kv.Key);
                                                targetSubtableEntry.SetValueFromColumn("0", (int)kv.Value);

                                                ArmpEntry targetMainTableEntry = targetCommandsetFile.GetMainTable().AddEntry();
                                                targetSubtableEntry.SetValueFromColumn("2", targetMainTableEntry.ID);
                                            }
                                        }

                                        ArmpFileWriter.WriteARMPToFile(targetCommandsetFile, Path.Combine(outputDbFolder.FullName, "battle_command_set.bin"));
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Input/target game does not have battle_command_set.bin on DB folder. Will not add commandset to DB");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Output game does not have DB directories. Will not add commandsets to DB.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Input game does not have DB directories. Will not add commandsets to DB.");
                        }
                    }


                    DirectoryInfo inputBattleDir = inputDirs.FirstOrDefault(x => x.Name.StartsWith("fighter"));
                    DirectoryInfo targetBattleDir = new DirectoryInfo(targetDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));
                    DirectoryInfo targetCfcFolder = new DirectoryInfo(targetBattleDir.FullName).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("Fighter Command") || x.Name == "cfc");
                    DirectoryInfo outputBattleDir = new DirectoryInfo(Path.Combine(outputDir, "battle"));
                    DirectoryInfo outputCfcDir = new DirectoryInfo(Path.Combine(outputBattleDir.FullName, targetCfcFolder.Name));


                    if (targetCfcFolder == null)
                    {
                        Console.WriteLine("Unpacked Fighter command not found on target dir.");
                        return;
                    }

                    string outputCfcPath = null;

                    if (!outputBattleDir.Exists)
                        outputBattleDir.Create();

                    if (!outputCfcDir.Exists)
                        outputCfcDir.Create();

                    string cfcInfPath = Path.Combine(targetCfcFolder.FullName, "File Information.json");
                    string outCfcInfPath = Path.Combine(outputCfcDir.FullName, "File Information.json");

                    JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                    JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];

                    outputCfcPath = Path.Combine(outputCfcDir.FullName, fileInf["Files Directory"].ToString());

                    if (IniSettings.ConvertHAct)
                    {
                        if (inputGame > Game.Y6)
                        {
                            if (dbFolder != null)
                            {
                                DirectoryInfo inputHactsDir = inputDirs.FirstOrDefault(x => x.Name.StartsWith("hact"));

                                if (inputHactsDir.Exists)
                                {
                                    FileInfo talkFile = new DirectoryInfo(dbFolder.FullName).GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "talk_param.bin");
                                    FileInfo talkFile2 = new DirectoryInfo(targetDbFolder.FullName).GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "talk_param.bin");

                                    if (talkFile.Exists && talkFile2.Exists)
                                    {
                                        ARMP talkParam = ArmpFileReader.ReadARMP(talkFile.FullName);
                                        ARMP talk2Param = ArmpFileReader.ReadARMP(talkFile2.FullName);

                                        DirectoryInfo outputHActDir = new DirectoryInfo(targetDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("hact"));

                                        if (outputHActDir == null)
                                            outputHActDir = new DirectoryInfo(Path.Combine(outputDir, "hact"));
                                        else
                                            outputHActDir = new DirectoryInfo(Path.Combine(outputDir, outputHActDir.Name));

                                        var map = ConvertHActs(hactsToConvert.Distinct().ToArray(), outputBattleDir, talkParam, talk2Param, inputHactsDir, outputDir, outputHActDir, inputGame, outputGame, removeSkillCond, removeStyleCond, removePlayerCond);

                                        if(map is Dictionary<uint, uint>)
                                        {
                                            foreach(string convertedSet in convertedSets)
                                            {
                                                CFC.AdjustHActIDsToNumbers(convertedSet, (Dictionary<uint, uint>)map);
                                            }
                                        }
                                        else if (map is Dictionary<uint, string>)
                                        {
                                            foreach (string convertedSet in convertedSets)
                                            {
                                                CFC.AdjustHActIDsToStrings(convertedSet, (Dictionary<uint, string>)map);
                                            }
                                        }

                                        //TODO IMPROVE: this is not good
                                        ArmpFileWriter.WriteARMPToFile(talk2Param, Path.Combine(outputDbFolder.FullName, "talk_param.bin"));
                                    }
                                }
                                else
                                    Console.WriteLine("Input game hact folder not found, will not be converting hacts");
                            }
                            else
                                Console.WriteLine("Input DB Folder not found, will not be adjusting talk_param");
                        }
                    }



                    ARMP inputParticlePuid = null;
                    ARMP targetParticlePuid = null;

                    DirectoryInfo inputParticleFolder = new DirectoryInfo(inputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("particle"));

                    if (puidFolder.Exists)
                    {
                        if (File.Exists(Path.Combine(puidFolder.FullName, "particle.bin")))
                            inputParticlePuid = ArmpFileReader.ReadARMP(Path.Combine(puidFolder.FullName, "particle.bin"));

                    }

                    if (targetPuidFolder.Exists)
                    {
                        if (File.Exists(Path.Combine(targetPuidFolder.FullName, "particle.bin")))
                            targetParticlePuid = ArmpFileReader.ReadARMP(Path.Combine(targetPuidFolder.FullName, "particle.bin"));
                    }


                    if (inputGame > Game.Y6 && IniSettings.ConvertPib)
                    {
                        if (inputParticlePuid != null && inputParticleFolder != null)
                            ConvertPibs(inputParticleFolder, outputDir, outputPuidDir.FullName, targetParticlePuid, inputGame, outputGame);
                        else
                        {
                            Console.WriteLine("Input Particle PUID or foldern not found, will not be converting particles");
                        }
                    }

                    if (IniSettings.ConvertSound)
                    {
                        DirectoryInfo inputSoundFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("sound"));
                        DirectoryInfo targetSoundFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("sound"));

                        FileInfo inputSoundDB = dbFolder.GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "sound_cuesheet_info.bin");
                        FileInfo targetSoundDB = targetDbFolder.GetFiles("*.bin", SearchOption.AllDirectories).FirstOrDefault(x => x.Name == "sound_cuesheet_info.bin");

                        if (inputSoundFolder.Exists && targetSoundFolder.Exists && inputSoundDB != null && targetSoundDB != null)
                        {
                            ARMP inputSoundArmp = ArmpFileReader.ReadARMP(inputSoundDB.FullName);
                            ARMP targetSoundArmp = ArmpFileReader.ReadARMP(targetSoundDB.FullName);
                            ConvertSounds(inputSoundFolder, Path.Combine(outputDir, targetSoundFolder.Name), outputDbFolder.FullName, inputSoundArmp, targetSoundArmp, inputGame, outputGame);
                        }
                    }

                    break;
            }

            Console.WriteLine("Over!");
        }

        struct CFCAsset
        {
            public string AssetName;
            public FileInfo File;
        }

        static PibVersion GetPibVersionForGame(Game game)
        {
            switch (game)
            {
                default:
                    return PibVersion.Y0;
                case Game.Y5:
                    return PibVersion.Y5;
                case Game.Ishin:
                    return PibVersion.Ishin;
                case Game.Y6:
                    return PibVersion.Y6;
                case Game.YK2:
                    return PibVersion.YK2;
                case Game.JE:
                    return PibVersion.JE;
                case Game.YLAD:
                    return PibVersion.YLAD;
                case Game.LJ:
                    return PibVersion.LJ;
                case Game.LAD7Gaiden:
                    return PibVersion.Gaiden;
                case Game.LADIW:
                    return PibVersion.LJ;
                case Game.YK3:
                    return PibVersion.YK3;
            }
        }


        static void PortCFC(string inputDir, string targetDir, string inputGameS, string outputGameS, string inputFile, string outputDir, bool isMainCFC, bool removeSkillCond, bool removeStyleCond, bool removePlayerCond)
        {
            if (!File.Exists(inputFile))
                return;

            if (convertedSets.Contains(new FileInfo(inputFile).Name))
                return;

            convertedSets.Add(new FileInfo(inputFile).Name);

            Console.WriteLine("---Porting---" + new FileInfo(inputFile).Name);

            string[] cfcAssets = CFC.Parse(inputFile).ToArray();

            DirectoryInfo motionFolder = new DirectoryInfo(inputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("motion"));
            CFCAsset[] cfcFiles = GetGMTFiles(motionFolder, cfcAssets);

            Game inputGame = CMN.GetGameFromString(inputGameS);
            Game outputGame = CMN.GetGameFromString(outputGameS);

            GameVersion inputVersion = CMN.GetVersionForGame(inputGame);
            GameVersion outputVersion = CMN.GetVersionForGame(outputGame);

            if (CMN.IsDE(inputVersion) && CMN.IsDE(outputVersion))
            {
                string outputMotionPath = Path.Combine(outputDir, "motion");
                string outputGmtPath = Path.Combine(outputMotionPath, "gmt");
                string outputBepPath = Path.Combine(outputMotionPath, "bep");
                string outputPuidDir = Path.Combine(outputDir, "puid");

                DirectoryInfo[] inputDirs = new DirectoryInfo(inputDir).GetDirectories();
                DirectoryInfo[] targetDirs = new DirectoryInfo(targetDir).GetDirectories();

                DirectoryInfo dbFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
                DirectoryInfo puidFolder = inputDirs.FirstOrDefault(x => x.Name.StartsWith("puid"));

                DirectoryInfo originalDbFolder = dbFolder;

                DirectoryInfo targetDbFolder = targetDirs.FirstOrDefault(x => x.Name.StartsWith("db"));
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

                DirectoryInfo outputDbFolder = null;

                if (targetDbFolder.Exists)
                {
                    outputDbFolder = new DirectoryInfo(Path.Combine(outputDir, targetDbFolder.Name));

                    if (!outputDbFolder.Exists)
                        outputDbFolder.Create();
                }
                else
                    outputDbFolder = new DirectoryInfo(Path.Combine(outputDir, "db"));

                if (!isMainCFC)
                {
                    puidFolder = new DirectoryInfo(outputPuidDir);
                    dbFolder = outputDbFolder;
                }


                ARMP motionGmtPuid = null;
                ARMP motionBepPuid = null;
                ARMP inputParticlePuid = null;
                ARMP targetParticlePuid = null;

                DirectoryInfo inputMotionFolder = new DirectoryInfo(inputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("motion"));
                DirectoryInfo inputBepFolder = inputMotionFolder.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("bep"));
                DirectoryInfo inputParticleFolder = new DirectoryInfo(inputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("particle"));

                if (puidFolder.Exists)
                {
                    if (isMainCFC)
                    {
                        motionGmtPuid = ArmpFileReader.ReadARMP(Path.Combine(targetPuidFolder.FullName, "motion_gmt.bin"));
                        motionBepPuid = ArmpFileReader.ReadARMP(Path.Combine(targetPuidFolder.FullName, "motion_bep.bin"));
                    }
                    else
                    {
                        motionGmtPuid = ArmpFileReader.ReadARMP(Path.Combine(outputPuidDir, "motion_gmt.bin"));
                        motionBepPuid = ArmpFileReader.ReadARMP(Path.Combine(outputPuidDir, "motion_bep.bin"));
                    }

                    if (File.Exists(Path.Combine(puidFolder.FullName, "particle.bin")))
                        inputParticlePuid = ArmpFileReader.ReadARMP(Path.Combine(puidFolder.FullName, "particle.bin"));

                }

                if (targetPuidFolder.Exists)
                {
                    if (File.Exists(Path.Combine(targetPuidFolder.FullName, "particle.bin")))
                        targetParticlePuid = ArmpFileReader.ReadARMP(Path.Combine(targetPuidFolder.FullName, "particle.bin"));
                }

                if (IniSettings.ConvertMoves)
                {
                    foreach (CFCAsset cfcFile in cfcFiles)
                    {
                        string targetPath = Path.Combine(outputGmtPath, cfcFile.File.Name);
                        string inputBepPath = Path.Combine(inputBepFolder.FullName, Path.GetFileNameWithoutExtension(cfcFile.File.FullName) + ".bep");

                        if (File.Exists(inputBepPath))
                        {
                            cfcFile.File.CopyTo(targetPath, true);
                            try
                            {
                                BEP bep = BEP.Read(inputBepPath, inputGame);

                                string outPath = Path.Combine(outputBepPath, Path.GetFileName(inputBepPath));
                                BEP.Write(bep, outPath, outputGame);

                                convertedAnims.Add(outPath);

                                Console.WriteLine("Converted BEP and copied animation file for " + new FileInfo(outPath).Name);
                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine("ERROR CONVERTING BEP " + Path.GetFileName(inputBepPath) + "\n" + ex.ToString());   
                            }
                        }
                    }

                    if (puidFolder.Exists)
                    {
                        foreach (CFCAsset inf in cfcFiles)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(inf.File.FullName);
                            string lowerName = fileName.ToLowerInvariant();
                            ArmpEntry entry = motionGmtPuid.GetMainTable().GetAllEntries().Where(x => x.Name.ToLowerInvariant() == lowerName).FirstOrDefault();

                            if (entry != null)
                            {
                                if (!entry.IsValid)
                                    entry.IsValid = true;
                            }
                            else
                            {
                                ArmpEntry newEntry = motionGmtPuid.GetMainTable().AddEntry(inf.AssetName);
                                string bepName = Path.Combine(outputBepPath, fileName + ".bep");

                                if (File.Exists(bepName))
                                {
                                    ArmpEntry newEntry2 = motionBepPuid.GetMainTable().AddEntry(inf.AssetName);
                                }
                            }
                        }

                        ArmpFileWriter.WriteARMPToFile(motionGmtPuid, Path.Combine(outputPuidDir, "motion_gmt.bin"));
                        ArmpFileWriter.WriteARMPToFile(motionBepPuid, Path.Combine(outputPuidDir, "motion_bep.bin"));

                        string fileName2 = new FileInfo(inputFile).Name;

                        Console.WriteLine("Converted motion files and puid for " + fileName2);

                        convertedSets.Add(inputFile);
                    }
                    else
                    {
                        Console.WriteLine("PUID folder does not exist. Will not be converting puid bins");
                    }



                }



                if (IniSettings.Recurse)
                    foreach (string dependency in CFC.ParseDependingCFCs(inputFile))
                    {
                        PortCFC(inputDir, targetDir, inputGameS, outputGameS, Path.Combine(new FileInfo(inputFile).Directory.FullName, dependency), outputDir, false, removeSkillCond, removeStyleCond, removePlayerCond);
                    }


                DirectoryInfo outputBattleDirectory = new DirectoryInfo(outputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));

                string outputBattleDir = "";

                if (outputBattleDirectory != null)
                    outputBattleDir = outputBattleDirectory.FullName;
                else
                    outputBattleDir = Path.Combine(outputDir, "battle");

                if (!Directory.Exists(outputBattleDir))
                    Directory.CreateDirectory(outputBattleDir);

                string outputCfcDir = Path.Combine(outputBattleDir, "cfc");
                string outputCfcPath = null;

                string[] hactsUsedInCFC = CFC.ParseHActs(inputFile);
                hactsToConvert.AddRange(hactsUsedInCFC);

                if (Directory.Exists(outputBattleDir))
                {
                    Dictionary<int, string> addedSets = new Dictionary<int, string>();

                    DirectoryInfo cfcFolder = new DirectoryInfo(outputBattleDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("Fighter Command"));
                    if (cfcFolder != null)
                    {
                        string cfcInfPath = Path.Combine(cfcFolder.FullName, "File Information.json");

                        JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                        JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];

                        outputCfcPath = Path.Combine(cfcFolder.FullName, fileInf["Files Directory"].ToString());

                        int highest = 0;

                        foreach (var property in sets.Properties())
                        {
                            // Check if the property value is an integer
                            if (property.Value.Type == JTokenType.Integer)
                            {
                                int value = property.Value.Value<int>();
                                if (value > highest)
                                {
                                    highest = value;
                                }
                            }
                        }

                        foreach (string str in convertedSets)
                        {
                            string name = Path.GetFileNameWithoutExtension(str);
                            bool setExists = sets.ContainsKey(name);

                            if (!setExists)
                            {
                                highest++;
                                sets.Add(name, highest);
                                addedSets[highest] = name;
                                convertedSetsMap[name] = highest;

                                Console.WriteLine("Added " + name + " to CFC File Information");
                            }
                        }

                        File.WriteAllText(cfcInfPath, JsonConvert.SerializeObject(fileInf, Formatting.Indented));
                    }
                    else
                    {

                        outputBattleDir = Path.Combine(outputDir, "battle");
                        outputCfcDir = Path.Combine(outputBattleDir, "cfc");
                    }

                    if (!Directory.Exists(outputBattleDir))
                        Directory.CreateDirectory(outputBattleDir);

                    if (!Directory.Exists(outputCfcDir))
                        Directory.CreateDirectory(outputCfcDir);

                    outputCfcPath = Path.Combine(outputCfcDir, Path.GetFileName(inputFile));

                    File.Copy(inputFile, outputCfcPath, true);
                    Console.WriteLine("Moved " + new FileInfo(inputFile) + " to " + outputCfcPath);

                    if (inputGame >= Game.LADIW || outputGame >= Game.LADIW)
                        if (inputGame != outputGame)
                        {
                            CFC.AdjustCFC(outputCfcPath);
                            CFC.AdjustCFCIDs(outputCfcPath, inputGame, outputGame);
                        }

                    CFC.RemoveConditions(outputCfcPath, removeStyleCond, removeSkillCond, removePlayerCond);
                }
            }
        }


        private static object ConvertHActs(string[] hactsList, DirectoryInfo outBattleDir, ARMP inputTalkParam, ARMP outputTalkParam, DirectoryInfo inputHactsDir, string outputDir, DirectoryInfo outputHActDir, Game inputGame, Game outputGame, bool removeSkillCondition, bool removeStyleCondition, bool removePlayerCondition)
        {
            if (!IniSettings.ConvertHAct)
                return null;

            if (inputHactsDir == null)
                return null;

            if (hactsList.Length <= 0)
                return null;

            if (!outputHActDir.Exists)
                outputHActDir.Create();

            object output = null;

            bool inputUsesNumbers = inputGame >= Game.JE;
            bool outputUsesNumbers = outputGame >= Game.JE;

            //hact ID stored instead of string name
            bool isNumbered = char.IsDigit(hactsList[0][0]);

            string[] hactNames = new string[hactsList.Length];
            uint[] origHActIds = new uint[hactsList.Length];

            if (!isNumbered)
            {
                hactNames = hactsList;

                for (int i = 0; i < hactsList.Length; i++)
                    origHActIds[i] = inputTalkParam.GetMainTable().GetEntry(hactNames[i]).ID;
            }
            else
            {
                for (int i = 0; i < hactsList.Length; i++)
                {
                    origHActIds[i] = uint.Parse(hactsList[i]);
                    hactNames[i] = inputTalkParam.GetMainTable().GetEntry(origHActIds[i]).Name;
                }
            }

            if (inputUsesNumbers && outputUsesNumbers)
            {
                Dictionary<uint, uint> hactIds = new Dictionary<uint, uint>();

                for (int i = 0; i < hactNames.Length; i++)
                {
                    string hact = hactNames[i];

                    ArmpEntry entry = null;
                    if (outputTalkParam.GetMainTable().TryGetEntry(hact, out entry))
                    {
                        if (!entry.IsValid)
                        {
                            entry.IsValid = true;
                        }

                        hactIds[origHActIds[i]] = entry.ID;
                    }
                    else
                    {
                        ArmpEntry template = outputTalkParam.GetMainTable().GetEntry(1);

                        string path = template.GetValueFromColumn("path").ToString().Replace(template.Name, "").Replace("talk_", "hact_").Replace("test/", "") + hact;

                        entry = outputTalkParam.GetMainTable().AddEntry(hact);
                        entry.SetValueFromColumn("path", path);

                        if (outputTalkParam.GetMainTable().IsColumnValid("type"))
                            entry.SetValueFromColumn("type", 9);

                        hactIds[origHActIds[i]] = entry.ID;
                    }

                    output = hactIds;
                }

                /*
                CFC.AdjustHActIDsToNumbers(origCfc, hactIds);
                Console.WriteLine("Adjusted HAct IDs on CFC");
                ConvertHActs(inputHactsDir, outputHActDir, inputGame, outputGame, hactNames);
                Console.WriteLine("Converted HActs");
                */

                DirectoryInfo battleDir = inputHactsDir.Parent.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));
                DirectoryInfo chpDir = null;

                if (battleDir.Exists)
                    chpDir = battleDir.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("HAct"));

                DirectoryInfo outputCHPDir = outBattleDir.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("HAct"));

                if (chpDir != null && chpDir.Exists && outputCHPDir != null)
                {
                    string cfcInfPath = Path.Combine(chpDir.FullName, "File Information.json");
                    string cfcInfPath2 = Path.Combine(outputCHPDir.FullName, "File Information.json");

                    if (File.Exists(cfcInfPath))
                    {
                        JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                        JObject fileInf2 = JObject.Parse(File.ReadAllText(cfcInfPath2));
                        string jsonsDirName = fileInf["Files Directory"].ToString();
                        string jsonsDirName2 = fileInf2["Files Directory"].ToString();
                        JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];
                        JObject sets2 = (JObject)fileInf2["File Specific Info"]["Set Order"];

                        DirectoryInfo jsonsDir = new DirectoryInfo(Path.Combine(chpDir.FullName, jsonsDirName));
                        DirectoryInfo jsonsDir2 = new DirectoryInfo(Path.Combine(outputCHPDir.FullName, jsonsDirName));

                        if (jsonsDir.Exists)
                        {
                            foreach (var kv in hactIds.Distinct())
                            {
                                FileInfo chpFile = new FileInfo(Path.Combine(jsonsDir.FullName, kv.Key + ".json"));

                                if (chpFile.Exists)
                                {
                                    string outputPath = Path.Combine(outputCHPDir.FullName, "Extracted", kv.Value + ".json");
                                    chpFile.CopyTo(outputPath, true);
                                    CFC.RemoveConditions(outputPath, removeStyleCondition, removeSkillCondition, removePlayerCondition);
                                    CFC.AdjustCFCIDs(outputPath, inputGame, outputGame);


                                    Console.WriteLine("Copied CHP HACt ID " + kv.Key + $"(now " + kv.Value + ")");

                                    if (!sets2.ContainsKey(kv.Value.ToString()))
                                        sets2[kv.Value.ToString()] = "";
                                }
                            }

                            File.WriteAllText(cfcInfPath2, JsonConvert.SerializeObject(fileInf2, Formatting.Indented));
                        }
                    }
                }

                ConvertHActs(inputHactsDir, outputHActDir, inputGame, outputGame, hactNames);
            }
            else if (inputUsesNumbers && !outputUsesNumbers)
            {
                Dictionary<uint, string> hactReplaces = new Dictionary<uint, string>();

                for (int i = 0; i < hactNames.Length; i++)
                {
                    string hact = hactNames[i];

                    ArmpEntry entry = null;
                    if (outputTalkParam.GetMainTable().TryGetEntry(hact, out entry))
                    {
                        if (!entry.IsValid)
                        {
                            entry.IsValid = true;
                        }

                        hactReplaces[entry.ID] = hact;
                    }
                    else
                    {
                        ArmpEntry template = outputTalkParam.GetMainTable().GetEntry(1);
                        string path = template.GetValueFromColumn("path").ToString().Replace(template.Name, "").Replace("talk_", "hact_").Replace("test/", "") + hact;

                        string name = hact;

                        //Fucking Kiwami 2
                        if (outputGame == Game.YK2)
                            name = "l2_" + name;

                        entry = outputTalkParam.GetMainTable().AddEntry(name);
                        entry.Flags[1] = true;
                        entry.SetValueFromColumn("path", path);
                        //this field does not exist in older agmes
                        try
                        {
                            entry.SetValueFromColumn("type", 9);
                        }
                        catch { }

                        try
                        {
                            entry.SetValueFromColumn("relative_play", (byte)1);
                        }
                        catch { }

                        try
                        {
                            entry.SetValueFromColumn("draw_stop_to_play", (byte)1);
                        }
                        catch { }

                        hactReplaces[origHActIds[i]] = hact;
                    }

                    output = hactReplaces;
                }

                ConvertHActs(inputHactsDir, outputHActDir, inputGame, outputGame, hactNames);

                DirectoryInfo battleDir = inputHactsDir.Parent.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("battle"));
                DirectoryInfo chpDir = null;

                if (battleDir.Exists)
                    chpDir = battleDir.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("HAct"));

                DirectoryInfo outputCHPDir = outBattleDir.GetDirectories().FirstOrDefault(x => x.Name.StartsWith("HAct"));

                if (chpDir != null && chpDir.Exists && outputCHPDir != null)
                {
                    string cfcInfPath = Path.Combine(chpDir.FullName, "File Information.json");
                    string cfcInfPath2 = Path.Combine(outputCHPDir.FullName, "File Information.json");

                    if (File.Exists(cfcInfPath))
                    {
                        JObject fileInf = JObject.Parse(File.ReadAllText(cfcInfPath));
                        JObject fileInf2 = JObject.Parse(File.ReadAllText(cfcInfPath2));
                        string jsonsDirName = fileInf["Files Directory"].ToString();
                        string jsonsDirName2 = fileInf2["Files Directory"].ToString();
                        JObject sets = (JObject)fileInf["File Specific Info"]["Set Order"];
                        JObject sets2 = (JObject)fileInf2["File Specific Info"]["Set Order"];

                        DirectoryInfo jsonsDir = new DirectoryInfo(Path.Combine(chpDir.FullName, jsonsDirName));
                        DirectoryInfo jsonsDir2 = new DirectoryInfo(Path.Combine(outputCHPDir.FullName, jsonsDirName));

                        if (jsonsDir.Exists)
                        {
                            foreach (var kv in hactReplaces.Distinct())
                            {
                                FileInfo chpFile = new FileInfo(Path.Combine(jsonsDir.FullName, kv.Key + ".json"));

                                if (chpFile.Exists)
                                {
                                    string outputPath = Path.Combine(outputCHPDir.FullName, "Extracted", kv.Value + ".json");
                                    chpFile.CopyTo(outputPath, true);
                                    CFC.RemoveConditions(outputPath, removeStyleCondition, removeSkillCondition, removePlayerCondition);

                                    JObject obj = JObject.Parse(File.ReadAllText(outputPath));
                                    ArmpEntry hactEntry = outputTalkParam.GetMainTable().GetAllEntries().FirstOrDefault(x => x.Name.Contains(kv.Value));
                                    obj["Talk_Param ID"] = hactEntry.ID;
                                    obj["Base HAct"] = kv.Value;
                                    obj["Unk Float"] = 0f;
                                    obj["Completion Flag"] = "Null";
                                    obj["Special Effect"] = "Null";

                                    File.WriteAllText(outputPath, JsonConvert.SerializeObject(obj, Formatting.Indented));

                                    CFC.AdjustHActIDsToStrings(outputPath, hactReplaces);

                                    Console.WriteLine("Copied CHP HACt " + kv.Key + $"(now " + kv.Value + ")");

                                    if (!sets2.ContainsKey(kv.Value.ToString()))
                                        sets2[kv.Value.ToString()] = "";
                                }
                            }

                            /*
                            foreach (var kv in hactIds.Distinct())
                            {
                                FileInfo chpFile = new FileInfo(Path.Combine(jsonsDir.FullName, kv.Key + ".json"));

                                if (chpFile.Exists)
                                {
                                    string outputPath = Path.Combine(outputCHPDir.FullName, "Extracted", kv.Value + ".json");
                                    chpFile.CopyTo(outputPath, true);
                                    CFC.RemoveConditions(outputPath, removeStyleCondition, removeSkillCondition, removePlayerCondition);

                                    Console.WriteLine("Copied CHP HACt ID " + kv.Key + $"(now " + kv.Value + ")");

                                    if (!sets2.ContainsKey(kv.Value.ToString()))
                                        sets2[kv.Value.ToString()] = "";
                                }
                            }
                            */

                            File.WriteAllText(cfcInfPath2, JsonConvert.SerializeObject(fileInf2, Formatting.Indented));
                        }
                    }
                }

                /*
                CFC.AdjustHActIDsToStrings(origCfc, hactReplaces);
                Console.WriteLine("Adjusted HAct IDs to strings on CFC");
                ConvertHActs(inputHactsDir, outputHActDir, inputGame, outputGame, hactNames);
                Console.WriteLine("Converted HActs");
                */
            }


            return output;
        }

        private static void ConvertHActs(DirectoryInfo inputHactDir, DirectoryInfo outputHactDir, Game inputGame, Game outputGame, string[] hactsList)
        {
            if (!IniSettings.ConvertHActFiles)
                return;

            foreach (string hact in hactsList)
            {
                DirectoryInfo hactDir = new DirectoryInfo(Path.Combine(inputHactDir.FullName, hact));

                if (hactDir.Exists)
                {
                    DirectoryInfo outHActDir = new DirectoryInfo(Path.Combine(outputHactDir.FullName, hact));

                    if (!outHActDir.Exists)
                        outHActDir.Create();

                    foreach (DirectoryInfo dirPath in hactDir.GetDirectories("*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.FullName.Replace(hactDir.FullName, outHActDir.FullName));
                    }

                    foreach (FileInfo newPath in hactDir.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        File.Copy(newPath.FullName, newPath.FullName.Replace(hactDir.FullName, outHActDir.FullName), true);
                    }
                }
                else
                {
                    FileInfo hactFile = new FileInfo(Path.Combine(inputHactDir.FullName, hact + ".par"));

                    if (File.Exists(hactFile.FullName))
                    {
                        DirectoryInfo outHActDir = new DirectoryInfo(Path.Combine(outputHactDir.FullName, hact));

                        if (!outHActDir.Exists)
                            outHActDir.Create();

                        Console.WriteLine("Extracting " + hactFile.Name);
                        Utils.ExtractFromPar(hactFile.FullName, outHActDir.FullName);


                        foreach (string dir in Directory.GetDirectories(outHActDir.FullName, "*.par"))
                        {
                            string newDir = Path.Combine(outHActDir.FullName, new DirectoryInfo(dir).Name.Replace(".par", ""));

                            if (Directory.Exists(newDir))
                                new DirectoryInfo(newDir).Delete(true);

                            Directory.Move(dir, newDir);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Couldn't find folder or par for " + hact + ", will not convert.");
                    }
                }
            }

            foreach (FileInfo file in outputHactDir.GetFiles("cmn*", SearchOption.AllDirectories))
            {
                if (file.Directory.Name != "cmn")
                    continue;

                CMN cmn = CMN.Read(file.FullName, inputGame);

                cmn.AllNodes = RyuseModule.ConvertNodes(cmn.AllNodes, inputGame, outputGame).OutputNodes.ToArray();
                cmn.GameVersion = CMN.GetVersionForGame(outputGame);
                CMN.LastHActDEGame = outputGame;
                CMN.Write(cmn, file.FullName);

                DirectoryInfo resDir = new DirectoryInfo(Path.Combine(file.Directory.FullName, "..", "000"));

                if(resDir.Exists)
                {
                    foreach (var binFile in resDir.GetFiles("*.bin"))
                    {
                        var res = RES.Read(binFile.FullName, CMN.IsDE(CMN.GetVersionForGame(inputGame)));
                        res.Version = 1;
                        RES.Write(res, binFile.FullName, true);
                    }
                }

                if (!convertedHActs.Contains(file.FullName))
                    convertedHActs.Add(file.FullName);

                Console.WriteLine("Converted " + file.Directory.Parent.Name + "/" + file.Name);
            }
        }

        //should be done last ideally
        private static void ConvertPibs(DirectoryInfo inputParticleFolder, string outputDir, string outputPuidDir, ARMP targetParticlePuid, Game inputGame, Game outputGame)
        {
            Dictionary<string, FileInfo> pibMap = new Dictionary<string, FileInfo>();
            Dictionary<string, FileInfo> texMap = new Dictionary<string, FileInfo>();

            FileInfo[] pibs = inputParticleFolder.GetFiles("*.pib", SearchOption.AllDirectories);
            FileInfo[] textures = inputParticleFolder.GetFiles("*.dds", SearchOption.AllDirectories);

            foreach (FileInfo pib in pibs)
                if (!pibMap.ContainsKey(Path.GetFileNameWithoutExtension(pib.Name)))
                    pibMap[Path.GetFileNameWithoutExtension(pib.Name).ToLowerInvariant()] = pib;

            foreach (FileInfo tex in textures)
                if (!texMap.ContainsKey(Path.GetFileNameWithoutExtension(tex.Name)))
                    texMap[Path.GetFileNameWithoutExtension(tex.Name).ToLowerInvariant()] = tex;


            DirectoryInfo outputParticleFolder = new DirectoryInfo(outputDir).GetDirectories().FirstOrDefault(x => x.Name.StartsWith("particle"));

            if (outputParticleFolder == null)
            {
                outputParticleFolder = new DirectoryInfo(Path.Combine(outputDir, "particle"));
                outputParticleFolder.Create();
            }

            foreach (string str in convertedAnims)
            {
                BEP bep = BEP.Read(str, outputGame);
                DEElementParticle[] particles = bep.AllElements.Where(x => x is DEElementParticle).Cast<DEElementParticle>().ToArray();

                foreach (DEElementParticle particle in particles)
                {
                    if (convertedPibs.Contains(particle.ParticleName.ToLowerInvariant()))
                    {
                        particle.ParticleID = pibVals[particle.ParticleName.ToLowerInvariant()];
                        continue;
                    }

                    string name = particle.ParticleName.ToLowerInvariant();

                    if (!pibMap.ContainsKey(name))
                        continue;

                    FileInfo pibFile = pibMap[name];
                    BaseDEPib pib = (BaseDEPib)PIB.Read(pibFile.FullName);

                    if (inputGame != outputGame)
                    {
                        if (pib.Version == PibVersion.LJ)
                        {
                            if (outputGame == Game.LAD7Gaiden || outputGame >= Game.LADPYIH)
                            {

                                if (inputGame < Game.LAD7Gaiden || inputGame == Game.LADIW)
                                    pib.Version = PibVersion.Gaiden;
                            }
                            else
                                pib.Version = PibVersion.LJ;
                        }
                    }

                    uint newPtcID = 0;

                    BasePib newPib = PIB.Convert(pib, GetPibVersionForGame(outputGame));
                    ArmpEntry pibEntry = null;

                    if (targetParticlePuid.GetMainTable().TryGetEntry(particle.ParticleName, out pibEntry))
                        newPtcID = pibEntry.ID;
                    else
                        newPtcID = targetParticlePuid.GetMainTable().AddEntry(particle.ParticleName).ID;

                    newPib.ParticleID = newPtcID;
                    particle.ParticleID = newPtcID;

                    foreach (string pibTex in newPib.AllTextures)
                    {
                        string texName = pibTex;

                        if (!texName.EndsWith(".dds"))
                            texName += ".dds";


                        string repl = texName.Replace(".dds", "");

                        if (texMap.ContainsKey(repl))
                        {
                            texMap[repl].CopyTo(Path.Combine(outputParticleFolder.FullName, texMap[repl].Name), true);
                        }
                    }

                    pibVals[newPib.Name.ToLowerInvariant()] = newPtcID;
                    PIB.Write(newPib, Path.Combine(outputParticleFolder.FullName, newPib.Name + ".pib"));
                    convertedPibs.Add(newPib.Name.ToLowerInvariant());
                    Console.WriteLine("Converted " + newPib.Name);
                }


                BEP.Write(bep, str, outputGame);
            }

            foreach (string str in convertedHActs)
            {
                CMN cmn = CMN.Read(str, outputGame);

                DEElementParticle[] particles = cmn.AllElements.Where(x => x is DEElementParticle).Cast<DEElementParticle>().ToArray();

                foreach (DEElementParticle particle in particles)
                {
                    if (convertedPibs.Contains(particle.ParticleName.ToLowerInvariant()))
                    {
                        particle.ParticleID = pibVals[particle.ParticleName.ToLowerInvariant()];
                        continue;
                    }

                    string name = particle.ParticleName.ToLowerInvariant();

                    if (!pibMap.ContainsKey(name))
                        continue;

                    FileInfo pibFile = pibMap[name];
                    BaseDEPib pib = (BaseDEPib)PIB.Read(pibFile.FullName);

                    if (inputGame != outputGame)
                    {
                        if (pib.Version == PibVersion.LJ)
                        {
                            if (outputGame == Game.LAD7Gaiden)
                                pib.Version = PibVersion.Gaiden;
                            else
                                pib.Version = PibVersion.LJ;
                        }
                    }

                    uint newPtcID = 0;

                    BasePib newPib = PIB.Convert(pib, GetPibVersionForGame(outputGame));
                    ArmpEntry pibEntry = null;

                    if (targetParticlePuid.GetMainTable().TryGetEntry(particle.ParticleName, out pibEntry))
                        newPtcID = pibEntry.ID;
                    else
                        newPtcID = targetParticlePuid.GetMainTable().AddEntry(particle.ParticleName).ID;

                    newPib.ParticleID = newPtcID;
                    particle.ParticleID = newPtcID;

                    foreach (string pibTex in newPib.AllTextures)
                    {
                        string texName = pibTex;

                        if (!texName.EndsWith(".dds"))
                            texName += ".dds";


                        string repl = texName.Replace(".dds", "");

                        if (texMap.ContainsKey(repl))
                        {
                            texMap[repl].CopyTo(Path.Combine(outputParticleFolder.FullName, texMap[repl].Name), true);
                        }
                    }

                    pibVals[newPib.Name.ToLowerInvariant()] = newPtcID;
                    PIB.Write(newPib, Path.Combine(outputParticleFolder.FullName, newPib.Name + ".pib"));
                    convertedPibs.Add(newPib.Name.ToLowerInvariant());
                    Console.WriteLine("Converted " + newPib.Name);
                }

                CMN.Write(cmn, str);
            }

            ArmpFileWriter.WriteARMPToFile(targetParticlePuid, Path.Combine(outputPuidDir, "particle.bin"));
        }

        private static void ConvertSounds(DirectoryInfo inputSoundFolder, string outputDir, string outputDBDir, ARMP inputSoundDB, ARMP targetSoundDB, Game inputGame, Game outputGame)
        {
            List<uint> cuesheetsToConvert = new List<uint>();

            Type specialSoundsOutput = DEElementSE.GetSpecialSoundTypeForGame(outputGame);

            DirectoryInfo outputSoundDir = new DirectoryInfo(Path.Combine(outputDir, "sound"));

            if(!outputSoundDir.Exists)
                outputSoundDir.Create();

            foreach (string str in convertedHActs)
            {
                CMN cmn = CMN.Read(str, outputGame);

                DEElementSE[] sounds = cmn.AllElements.Where(x => x is DEElementSE).Cast<DEElementSE>().ToArray();

                foreach(DEElementSE sound in sounds)
                {
                    if (convertedSounds.ContainsKey(sound.CueSheet))
                    {
                        sound.CueSheet = (ushort)convertedSounds[sound.CueSheet];
                        continue;
                    }


                    if (Enum.IsDefined(specialSoundsOutput, sound.CueSheet))
                    {
                        continue;
                    }

                    ArmpEntry inputEntry = inputSoundDB.GetMainTable().GetAllEntries().FirstOrDefault(x => (ushort)x.GetValueFromColumn("*cuesheet_id") == sound.CueSheet);

                    if(inputEntry == null)
                        continue;

                    string soundName = (string)inputEntry.GetValueFromColumn("name");

                    if (string.IsNullOrEmpty(soundName))
                        continue;

                    ArmpEntry outputEntry = targetSoundDB.GetMainTable().GetAllEntries().FirstOrDefault(x => (string)x.GetValueFromColumn("name") == soundName);

                    if (outputEntry != null)
                    {
                        sound.CueSheet = (ushort)outputEntry.ID;
                        convertedSounds[sound.CueSheet] = sound.CueSheet;

                        continue;
                    }
                    else
                    {

                        ArmpEntry mainEntry = targetSoundDB.GetMainTable().AddEntry("");
                        mainEntry.SetValueFromColumn("name", soundName);
                        mainEntry.SetValueFromColumn("exists", true);


                        var ents = targetSoundDB.GetMainTable().Indexer.GetAllEntries();
                        ArmpEntry lastEntry = ents[ents.Count - 1];
                        ushort newID = (ushort)(ents.Count + 1); //(ushort)((uint)lastEntry.GetValueFromColumn("0") + 1);
                        mainEntry.SetValueFromColumn("*cuesheet_id", newID);


                        ArmpEntry subTableEntry = targetSoundDB.GetMainTable().Indexer.AddEntry(soundName);
                        subTableEntry.SetValueFromColumn("0", newID);
                        subTableEntry.SetValueFromColumn("2", mainEntry.ID);
                        convertedSounds[sound.CueSheet] = newID;
                        sound.CueSheet = newID;

                        if (soundName.StartsWith("hact_"))
                            mainEntry.SetValueFromColumn("category", targetSoundDB.GetMainTable().GetAllEntries().First(x => ((string)x.GetValueFromColumn("name")).StartsWith("hact")).GetValueFromColumn("category"));

                        FileInfo file = new FileInfo(Path.Combine(inputSoundFolder.FullName, soundName + ".acb"));

                        if(file.Exists)
                        {
                            file.CopyTo(Path.Combine(outputSoundDir.FullName, file.Name), true);
                        }
                    }

                }

                CMN.Write(cmn, str);
            }


            ArmpFileWriter.WriteARMPToFile(targetSoundDB, Path.Combine(outputDBDir, "sound_cuesheet_info.bin"));
        }
        private static CFCAsset[] GetGMTFiles(DirectoryInfo motionDir, string[] assets)
        {
            string[] assetsLow = assets.Select(x => x.ToLowerInvariant()).ToArray();

            DirectoryInfo targetDir = motionDir;

            string deMotion = Path.Combine(motionDir.FullName, "gmt");

            if (Directory.Exists(deMotion))
                targetDir = new DirectoryInfo(deMotion);

            List<CFCAsset> foundFiles = new();

            foreach (FileInfo gmtFile in targetDir.GetFiles("*.gmt", SearchOption.AllDirectories))
            {
                string targetName = Path.GetFileNameWithoutExtension(gmtFile.FullName);

                if (assetsLow.Contains(targetName.ToLowerInvariant()))
                    foundFiles.Add(new CFCAsset() { AssetName = assets[Array.IndexOf(assetsLow, targetName.ToLowerInvariant())], File = gmtFile });
            }

            return foundFiles.ToArray();
        }

        public static Dictionary<string, uint> GenerateTalkEntriesForHActs(List<string> hacts, ARMP talkParamArmp, Game inputGame, Game outputGame)
        {
            Dictionary<string, uint> newHActIDs = new Dictionary<string, uint>();

            bool outputUsesNumbers = outputGame >= Game.JE;

            ArmpEntry template = talkParamArmp.GetMainTable().GetAllEntries().FirstOrDefault(x => x.GetValueFromColumn<string>("path").Contains("talk_"));


            foreach (string str in hacts)
            {
                string name = str;

                //???
                if (outputGame == Game.YK2)
                    name = "l2_" + name;

                ArmpEntry entry = talkParamArmp.GetMainTable().AddEntry(name);
                newHActIDs[str] = entry.ID;

                if (!outputUsesNumbers)
                {
                    entry.Flags[1] = true;
                    entry.SetValueFromColumn("relative_play", (byte)1);
                    entry.SetValueFromColumn("draw_stop_to_play", (byte)1);
                }

                string templatePath = template.GetValueFromColumn("path").ToString();

                string path = templatePath.Replace(template.Name, "").Replace("talk_", "hact_").Replace("test/", "") + str;
                path = path.ToLowerInvariant();
                entry.SetValueFromColumn("path", path);

                if (talkParamArmp.GetMainTable().IsColumnValid("type"))
                    entry.SetValueFromColumn("type", 9);
            }

            return newHActIDs;
        }
    }
}

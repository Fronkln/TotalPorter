using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TotalPorter
{
    internal static class IniSettings
    {
        public static bool Recurse = false;
        public static bool ConvertMoves = false;
        public static bool ConvertGMTs = false;
        public static bool ConvertHAct = false;
        public static bool ConvertHActFiles = false;
        public static bool ConvertPib = false;
        public static bool ConvertSound = false;

        public static string Mode;
        public static string InputGame;
        public static string OutputGame;

        public static string InputDir;
        public static string TargetDir;
        public static string OutputDir;


        public static void CreateIfNotPresent()
        {
            if (File.Exists("settings.ini"))
                return;

            Ini ini = new Ini("settings.ini");

            ini.WriteValue("Mode", "cfc");
            ini.WriteValue("InputGame", "y0");
            ini.WriteValue("OutputGame", "yk2");

            ini.WriteValue("InputDir", "");
            ini.WriteValue("TargetDir", "");
            ini.WriteValue("OutputDir", "");

            ini.WriteValue("Recursion", "1");
            ini.WriteValue("ConvertMoves", "1");
            ini.WriteValue("ConvertGMTs", "1");
            ini.WriteValue("ConvertHAct", "0");
            ini.WriteValue("ConvertHActFiles", "0");
            ini.WriteValue("ConvertSound", "0");

            ini.Save();
        }

        public static void Read()
        {
            Ini ini = new Ini("settings.ini");
            Recurse = ini.GetValue("Recursion", "", "1") == "1";
            ConvertMoves = ini.GetValue("ConvertMoves", "", "0") == "1";
            ConvertGMTs = ini.GetValue("ConvertGMTs", "", "1") == "1";
            ConvertHAct = ini.GetValue("ConvertHAct", "", "1") == "1";
            ConvertHActFiles = ini.GetValue("ConvertHActFiles", "", "1") == "1";
            ConvertSound = ini.GetValue("ConvertSound", "", "1") == "1";

            Mode = ini.GetValue("Mode", "", "cfc");

            InputGame = ini.GetValue("InputGame", "", "");
            OutputGame = ini.GetValue("OutputGame", "", "");

            InputDir = ini.GetValue("InputDir", "", "");
            TargetDir = ini.GetValue("TargetDir", "", "");
            OutputDir = ini.GetValue("OutputDir", "", "");
        }
    }
}

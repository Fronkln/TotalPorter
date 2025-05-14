using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HActLib;

namespace TotalPorter
{
    public static class CHP
    {
        public static void ConvertToDEFormat(string chpFile, uint talkParamID, Game inputGame)
        {
            List<JContainer> tokensToRemove = new List<JContainer>();
            List<JProperty> statesToConvert = new List<JProperty>();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {
                        JObject parent = (JObject)property.Parent;


                        if (property.Name.StartsWith("State Type"))
                        {
                            statesToConvert.Add(property);
                        }

                        if (property.Name.StartsWith("End Motion ID"))
                            property.Value.Replace(0);

                        if (property.Name.Contains("| Weapon ID"))
                        {
                            JObject objecto = (JObject)property.Last;
                            objecto["Weapon ID"].Replace(0);
                        }


                        if (property.Name.Contains("| Have Item"))
                        {
                            JObject objecto = (JObject)property.Last;
                            objecto["Item ID"].Replace(0);
                        }

                        if (property.Name.Contains("| Motion ID"))
                        {
                            // property.Value.Replace(0);
                            //parent["Conditionals"] = "";
                            tokensToRemove.Add(property);
                        }

                        if (property.Name.Contains("Ctrl Type"))
                        {
                            tokensToRemove.Add(property);
                        }

                        if (property.Name.Contains("| Range"))
                        {
                            JObject rangeObj = (JObject)property.Value;
                            int range = (int)rangeObj["Unk Byte 1"];
                            int newRange = ConvertRangeToDE(range);
                            rangeObj["Unk Byte 1"].Replace((byte)newRange);
                        }

                        if (property.Name.Contains("| Weapon Category"))
                        {
                            JObject catObj = (JObject)property.Value;
                            int category = (int)catObj["Weapon Category ID"];
                            int newCategory = ConvertAssetCategoryToDE(category);
                            catObj["Weapon Category ID"].Replace(newCategory);
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

            var jobject = JObject.Parse(File.ReadAllText(chpFile));
            IterateJson(jobject);

            foreach (var token in tokensToRemove)
                token.Remove();

            jobject.Remove("Completion Name");
            jobject.Remove("Unknown Target Value");
            jobject.Remove("Condition Name");

            jobject["Talk_Param ID"] = talkParamID;
            jobject["Completion Flag"] = "Null";
            jobject["Play Area Type"] = 0;

            jobject["X"] = jobject["X?"];
            jobject["Y"] = jobject["Y?"];
            jobject["Z"] = jobject["Z?"];

            jobject["Rotation"] = 0f;
            jobject["Unk Float"] = 0;
            jobject["Base HAct"] = Path.GetFileNameWithoutExtension(chpFile);

            jobject.Remove("X?");
            jobject.Remove("Y?");
            jobject.Remove("Z?");
            jobject.Remove("Unknown Int 3");
            jobject.Remove("Unknown Int 4");


            foreach (JProperty prop in statesToConvert)
            {
                string val = ConvertStateToDE((string)prop.Value, inputGame);

                if (string.IsNullOrEmpty(val))
                    Console.WriteLine("Don't know how to convert state " + prop.Value);

                prop.Value.Replace(val);
            }

            File.WriteAllText(chpFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static string ConvertStateToDE(string inputState, HActLib.Game game)
        {
            switch (inputState)
            {
                default:
                    return "";
                case "Heat":
                    return "HeatOrDragon";
                case "Down":
                    return "Down";
                case "Drunk":
                    if (game == HActLib.Game.Y5)
                        return "FromPocket";
                    break;
                case "DownUtu":
                    return "DownUtu";
                case "HPPinch":
                    return "HPPinch";
                case "Guard":
                    return "Guard";
                case "WallDamage":
                    return "WallHit";
                case "DownAo":
                    return "DownAo";
                case "Piyori":
                    return "Piyori";
                case "Bibiri": //fighter commander gllitch
                    return "Standup";
                case "OnStair":
                    return "OnStair";
                case "SyncBack":
                    return "SyncBack";
                case "SyncFront":
                    return "SyncFront";
                case "Provoke":
                    return "Provoke";
                case "FromPocket":
                    return "FromPocket";
                case "ShakeOff":
                    return "ShakeOff";
                case "Damage":
                    return "Damage";
                case "Standup"://idk how to convert
                    return "Special";
                case "Dash":
                    if (game == Game.Y5)
                    {
                        return "Bullet";
                    }
                    break;

                case "CanSeize":
                    return "CanSeize";

                case "Kamae":
                    return "Kamae";

                case "HeatZero":
                    return "HeatZero";

                case "CanRobWeapon":
                    return "CanRobWeapon";

                case "GuardBreak":
                    return "GuardBreak";

                case "Heavy":
                    return "Heavy";

                case "Bound":
                    return "Bound";

                case "Run":
                    return "Run";
            }

            return "";
        }

        public static int ConvertRangeToDE(int range)
        {
            switch (range)
            {
                //wall
                case 0:
                    return 1;

                //table
                case 4:
                    return 8;
                //pole
                case 5:
                    return 5;

                //heights, push_at_top, throw etc
                case 12:
                case 19:
                case 23:
                    return 2;

            }

            if (range > 22)
                return 0;

            return range;
        }

        public static int ConvertAssetCategoryToDE(int category)
        {
            switch (category)
            {
                //A
                case 1:
                    return 1;

                //B
                case 2:
                    return 2;

                //C
                case 3:
                    return 3;

                //D
                case 4:
                    return 4;

                //H
                case 8:
                    return 8;

                //E???
                case 9:
                    return 5;

                //M
                case 13:
                    return 16;

                //Y
                case 25:
                    return 12;

                //N
                case 10:
                    return 9;

                //P (does not exist
                case 16:
                    return 100;
            }

            return category;
        }
    }
}

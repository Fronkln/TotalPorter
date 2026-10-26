using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HActLib;
using System.Xml.Linq;

namespace TotalPorter
{
    internal static class CFC
    {
        /// <summary>
        /// Reads the cfc and returns all assets it references
        /// </summary>
        /// <param name="cfcFile"></param>
        /// <returns></returns>
        public static string[] Parse(string cfcFile)
        {
            if (!File.Exists(cfcFile))
                return Array.Empty<string>();

            List<string> assets = new();

            void IterateJsonElement(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        // If the element is an object, iterate through its properties
                        foreach (JsonProperty property in element.EnumerateObject())
                        {
                            if (property.Name == "Animation Used")
                            {
                                string anim = property.Value.ToString();

                                if (!string.IsNullOrEmpty(anim) && anim != "Null")
                                    assets.Add(anim);
                            }

                            // Recursively iterate if the value is an object or array
                            IterateJsonElement(property.Value);
                        }
                        break;

                    case JsonValueKind.Array:
                        // If the element is an array, iterate through its items
                        foreach (JsonElement arrayItem in element.EnumerateArray())
                        {
                            // Recursively iterate if the item is an object or array
                            IterateJsonElement(arrayItem);
                        }
                        break;
                }
            }

            string jsonContent = File.ReadAllText(cfcFile);

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                // Root element is the JSON object
                JsonElement root = doc.RootElement;

                IterateJsonElement(root);
            }


            return assets.Distinct().ToArray();
        }

        public static string[] ParseDependingCFCs(string cfcFile)
        {
            if (!File.Exists(cfcFile))
                return Array.Empty<string>();

            List<string> assets = new();

            void IterateJsonElement(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        // If the element is an object, iterate through its properties
                        foreach (JsonProperty property in element.EnumerateObject())
                        {
                            if (property.Name == "Command Set" || property.Name == "Moveset Name")
                            {
                                string anim = property.Value.ToString();

                                if (!string.IsNullOrEmpty(anim) && anim != "Null")
                                    assets.Add(anim + ".json");
                            }

                            // Recursively iterate if the value is an object or array
                            IterateJsonElement(property.Value);
                        }
                        break;

                    case JsonValueKind.Array:
                        // If the element is an array, iterate through its items
                        foreach (JsonElement arrayItem in element.EnumerateArray())
                        {
                            // Recursively iterate if the item is an object or array
                            IterateJsonElement(arrayItem);
                        }
                        break;
                }
            }

            string jsonContent = File.ReadAllText(cfcFile);

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                // Root element is the JSON object
                JsonElement root = doc.RootElement;

                IterateJsonElement(root);
            }


            return assets.Distinct().ToArray();
        }

        public static string[] ParseHActs(string cfcFile)
        {
            if (!File.Exists(cfcFile))
                return Array.Empty<string>();

            List<string> assets = new();

            void IterateJsonElement(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        // If the element is an object, iterate through its properties
                        foreach (JsonProperty property in element.EnumerateObject())
                        {
                            if (property.Name == "HAct ID")
                            {
                                string anim = property.Value.ToString();

                                if (!string.IsNullOrEmpty(anim) && anim != "Null")
                                    assets.Add(anim);
                            }

                            // Recursively iterate if the value is an object or array
                            IterateJsonElement(property.Value);
                        }
                        break;

                    case JsonValueKind.Array:
                        // If the element is an array, iterate through its items
                        foreach (JsonElement arrayItem in element.EnumerateArray())
                        {
                            // Recursively iterate if the item is an object or array
                            IterateJsonElement(arrayItem);
                        }
                        break;
                }
            }

            string jsonContent = File.ReadAllText(cfcFile);

            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
            {
                // Root element is the JSON object
                JsonElement root = doc.RootElement;

                IterateJsonElement(root);
            }


            return assets.Distinct().ToArray();
        }



        public static void AdjustCFC(string cfcFile)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JContainer> tokensToRemove = new List<JContainer>();
            List<JObject> syncObjsToAdjust = new List<JObject>();

            List<JProperty> statesToConvert = new List<JProperty>();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {

                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {
                        if (property.Name.StartsWith("Move Type"))
                        {
                            JObject parent = (JObject)property.Parent;
                            JObject animTableObj = (JObject)parent["Animation Table"];

                            if (animTableObj != null)
                            {
   
                                    syncObjsToAdjust.Add(animTableObj);

                            }
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);


            foreach (var parent in syncObjsToAdjust)
            {
                parent["Unknown Value"] = 0;
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static void AdjustCFCIDs(string cfcFile, Game inputGame, Game outputGame)
        {
            if (!File.Exists(cfcFile))
                return;

            if (outputGame < Game.LADIW && inputGame < Game.LADIW)
                return;

            if (outputGame > Game.LADIW && inputGame > Game.LADIW)
                return;

            bool toIWFormat = outputGame >= Game.LADIW;

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("Condition "))
                        {
                            var objy = (JObject)property.Value;

                            if (property.Name.Contains("Button"))
                            {
                                objy["Unk Value 1"] = 0;
                                objy["Unk Value 2"] = 0;
                                objy["Unk Value 3"] = 0;
                                objy["Unk Value 4"] = 0;
                            }

                            if (property.Name.EndsWith("Range"))
                            {
                                if(inputGame < Game.LADIW && outputGame >= Game.LADIW)
                                {
                                    try
                                    {
                                        GenericHActRange cond = (GenericHActRange)objy.Value<int>("Unk Byte 1");
                                         IWHActRange newCond = (IWHActRange)Enum.Parse(typeof(IWHActRange), cond.ToString());

                                        objy["Unk Byte 1"] = (int)newCond;
                                    }
                                    catch
                                    {
                                        objy["Property Type"] = 0;
                                    }
                                }

                            }

                            if (toIWFormat)
                            {
                                try
                                {
                                    GenericCFCConditions cond = (GenericCFCConditions)objy.Value<int>("Property Type");
                                    LADIWCFCConditions newCond = (LADIWCFCConditions)Enum.Parse(typeof(LADIWCFCConditions), cond.ToString());

                                    objy["Property Type"] = (int)newCond;
                                }
                                catch
                                {
                                    objy["Property Type"] = 0;
                                }
                            }
                            else
                            {
                                try
                                {
                                    LADIWCFCConditions cond = (LADIWCFCConditions)objy.Value<int>("Property Type");
                                    GenericCFCConditions newCond = (GenericCFCConditions)Enum.Parse(typeof(GenericCFCConditions), cond.ToString());

                                    objy["Property Type"] = (int)newCond;
                                }
                                catch
                                {
                                    objy["Property Type"] = 0;
                                }
                            }
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


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static void AdjustHActIDsToNumbers(string cfcFile, Dictionary<uint, uint> hactNumbers)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JToken> hactValues = new();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("HAct ID"))
                        {
                            var objy = (JValue)property.Value;
                            hactValues.Add(objy);
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);

            for (int i = 0; i < hactValues.Count; i++)
            {
                uint value = (uint)hactValues[i];

                if (hactNumbers.ContainsKey(value))
                    hactValues[i] = hactNumbers[value];
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static void AdjustHActStringsToNumbers(string cfcFile, Dictionary<string, uint> hactNumbers)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JToken> hactValues = new();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("HAct ID"))
                        {
                            var objy = (JValue)property.Value;
                            hactValues.Add(objy);
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);

            for (int i = 0; i < hactValues.Count; i++)
            {
                string value = (string)hactValues[i];

                if (hactNumbers.ContainsKey(value))
                    hactValues[i].Replace(hactNumbers[value]);
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        //Convert from string to ID
        public static void AdjustHActIDsToStrings(string cfcFile, Dictionary<uint, string> hactReplaces)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JToken> hactValues = new();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("HAct ID"))
                        {
                            var objy = (JValue)property.Value;
                            hactValues.Add(objy);
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);

            try
            {
                for (int i = 0; i < hactValues.Count; i++)
                {
                    uint value = (uint)hactValues[i];

                    if (hactReplaces.ContainsKey(value))
                        hactValues[i].Replace(hactReplaces[value]);
                    else
                        hactValues[i].Replace("");
                }
            }
            catch
            {
                //already in string format?
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static void RemoveConditions(string cfcFile, bool removeStyle, bool removeSkill, bool removePlayer, bool removeLab8 = false)
        {
            if (!removeSkill && !removeStyle && !removePlayer)
                return;

            if (!File.Exists(cfcFile))
                return;

            List<JContainer> tokensToRemove = new List<JContainer>();


            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {
                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {
                        if(removeLab8 && property.Name.Contains("LAB8"))
                            tokensToRemove.Add(property); 

                        if (property.Name.StartsWith("Condition "))
                        {
                            bool isSkill = property.Name.Contains("Skill");
                            bool isStyle = property.Name.Contains("Style");
                            bool isPlayer = property.Name.Contains("Player ID");

                            if (isSkill && removeSkill)
                                tokensToRemove.Add(property);
                            else if (isStyle && removeStyle)
                                tokensToRemove.Add(property);
                            else if (isPlayer && removePlayer)
                                tokensToRemove.Add(property);
                        }
                        else if (property.Name.StartsWith("State Type"))
                        {
                            bool isPlayer = property.Value.ToString().Contains("IsKaito") || property.Value.ToString().Contains("IsYagami");

                            if (removePlayer && isPlayer)
                            {
                                // string test = (property.Parent as JToken).va
                                tokensToRemove.Add(property.Parent.Parent);
                            }
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);

            foreach (JToken token in tokensToRemove)
                token.Remove();

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }

        public static void ConvertToDEFormat(string cfcFile, Game inputGame)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JContainer> tokensToRemove = new List<JContainer>();
            List<JObject> syncObjsToAdjust = new List<JObject>();
            List<JObject> animTablesToAdjust = new List<JObject>();

            List<JProperty> statesToConvert = new List<JProperty>();


            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {

                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("State Type"))
                        {
                            statesToConvert.Add(property);
                        }


                        if (property.Name.StartsWith("Animation Table"))
                        {
                            if (obj["Move Type"].Value<int>() == 6)
                                //Remove Yakuza 5/0 style startup anims
                                //They dont work. They will freeze the player and the victim
                                animTablesToAdjust.Add(obj);

                            /*
                            if (property.Last.Value<int>() == 6)
                            {
                                if (obj["Animation Used"] != null)
                                    obj["Animation Used"].Replace("Null");
                                else
                                {
                                    if (obj["Animation Table"] != null)
                                    {
                                        obj["Animation Table"] = null;
                                        obj["Animation Used"] = "Null";
                                    }
                                }
                            }
                            */
                           // statesToConvert.Add(property);
                        }

                        if (property.Name.StartsWith("Condition "))
                        {
                            bool isSkill = property.Name.Contains("Skill") || property.Name.Contains("Upgrade");
                            //beacuse i cant be arsed to convert this yet
                            bool isMotion = property.Name.Contains("Motion ID");

                            if (isSkill)
                                tokensToRemove.Add(property);

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
                                int newRange = CHP.ConvertRangeToDE(range);
                                rangeObj["Unk Byte 1"].Replace((byte)newRange);
                            }

                            if (property.Name.Contains("| Weapon Category"))
                            {
                                JObject catObj = (JObject)property.Value;
                                int category = (int)catObj["Weapon Category ID"];
                                int newCategory = CHP.ConvertAssetCategoryToDE(category);
                                catObj["Weapon Category ID"].Replace(newCategory);
                            }


                            if (property.Name.Contains("| Target Change"))
                            {
                                JObject catObj = (JObject)property.Value;
                                if ((int)catObj["Target Position"] == 1)
                                    catObj["Target Position"].Replace(2);
                            }

                        }

                        if (property.Name.StartsWith("Move Type"))
                        {
                            JObject parent = (JObject)property.Parent;
                            var animTable = parent["Animation Table"];

                            //Can be set to just "Null"
                            if (animTable != null && animTable.Type == JTokenType.Object)
                            {
                                JObject animTableObj = (JObject)animTable;

                                if (animTableObj != null)
                                {
                                    foreach (var anim in animTableObj)
                                        syncObjsToAdjust.Add((JObject)anim.Value);

                                }
                            }
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
            }


            var jobject = JObject.Parse(File.ReadAllText(cfcFile));

            string setName = Path.GetFileNameWithoutExtension(cfcFile);
            JObject movesetTable = (JObject)jobject["Moveset Change Table"];

            if(movesetTable != null)
            {
                foreach(var movesetChange in movesetTable)
                {
                    JObject changeObj = (JObject)movesetChange.Value;

                    if (changeObj["Moveset Name"].ToString() == "Null")
                    {
                        changeObj["Moveset Name"] = "Previous Moveset";
                    }
                }
            }

            IterateJson(jobject);

            foreach (var parent in syncObjsToAdjust)
            {
                parent["Motion ID"] = 0;
            }

            foreach (JToken token in tokensToRemove)
                token.Remove();

            foreach (JProperty prop in statesToConvert)
            {
                string val = CHP.ConvertStateToDE((string)prop.Value, inputGame);

                if (string.IsNullOrEmpty(val))
                {
                    val = "Special";
                    Console.WriteLine("Don't know how to convert state replacing it with a placeholder one, Special" + prop.Value);
                }

                prop.Value.Replace(val);
            }

            foreach(JObject obj in animTablesToAdjust)
            {
                obj.Remove("Animation Table");
                obj["Animation Used"] = "Null";
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));

        }

        public static void ConvertToOEFormat(string cfcFile, Game inputGame)
        {
            if (!File.Exists(cfcFile))
                return;

            List<JContainer> tokensToRemove = new List<JContainer>();
            List<JObject> syncObjsToAdjust = new List<JObject>();
            
            List<JProperty> statesToConvert = new List<JProperty>();

            void IterateJson(JToken token)
            {
                if (token is JObject obj)
                {

                    // Iterate through each property in the JObject
                    foreach (var property in obj.Properties())
                    {

                        if (property.Name.StartsWith("State Type"))
                        {
                            statesToConvert.Add(property);
                        }

                        if (property.Name.StartsWith("Condition "))
                        {
                            bool isSkill = property.Name.Contains("Skill") || property.Name.Contains("Upgrade");
                            //beacuse i cant be arsed to convert this yet
                            bool isMotion = property.Name.Contains("Motion ID");

                            if (isSkill)
                                tokensToRemove.Add(property);

                            if (property.Name.StartsWith("End Motion ID"))
                                property.Value.Replace(0);

                            /*
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
                                int newRange = CHP.ConvertRangeToDE(range);
                                rangeObj["Unk Byte 1"].Replace((byte)newRange);
                            }

                            if (property.Name.Contains("| Weapon Category"))
                            {
                                JObject catObj = (JObject)property.Value;
                                int category = (int)catObj["Weapon Category ID"];
                                int newCategory = CHP.ConvertAssetCategoryToDE(category);
                                catObj["Weapon Category ID"].Replace(newCategory);
                            }
                            */

                            if (property.Name.Contains("| Target Change"))
                            {
                                JObject catObj = (JObject)property.Value;
                                if ((int)catObj["Target Position"] == 2)
                                    catObj["Target Position"].Replace(1);
                            }

                        }

                        if (property.Name.StartsWith("Move Type"))
                        {
                            JObject parent = (JObject)property.Parent;
                            JObject animTableObj = (JObject)parent["Animation Table"];

                            if (animTableObj != null)
                            {
                                //foreach (var anim in animTableObj)
                                    syncObjsToAdjust.Add((JObject)animTableObj);

                            }
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
            }

            var jobject = JObject.Parse(File.ReadAllText(cfcFile));
            IterateJson(jobject);


            foreach (var parent in syncObjsToAdjust)
            {
                parent["Unknown Value"] = 0;
            }

            File.WriteAllText(cfcFile, JsonConvert.SerializeObject(jobject, Formatting.Indented));
        }
    }
}


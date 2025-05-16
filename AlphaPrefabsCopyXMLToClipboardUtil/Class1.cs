using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace AlphaPrefabs
{
    public static class DebugActions
    {
        [DebugAction("Alpha prefabs", "Copy XML to Clipboard", false, false, false, false, 0, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCopyXmlToClipboard()
        {
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("No active map found!", MessageTypeDefOf.RejectInput);
                return;
            }
            DebugToolsGeneral.GenericRectTool("xml", delegate (CellRect rect)
            {
                // 1. 完整市场价值计算
                float valueNum = CalculateMarketValue(rect);

                // 2. 完整研究依赖收集
                List<ResearchProjectDef> techList = CollectResearchPrerequisites(rect);

                // 3. 完整 Mod 依赖收集
                List<string> modList = CollectModDependencies(rect);

                // 4. 生成 XML 字符串
                string xml = BuildXmlString(valueNum, techList, modList);

                // 5. 复制到剪贴板
                CopyToClipboard(xml);

                // 反馈提示
                Messages.Message("XML copied to clipboard!", MessageTypeDefOf.SilentInput);
            });
        }

        #region 核心逻辑模块
        private static float CalculateMarketValue(CellRect rect)
        {
            float valueNum = 0f;
            List<Thing> processedThings = new List<Thing>();
            foreach (IntVec3 intVec in rect)
            {
                // 物品价值计算
                foreach (Thing thing in GridsUtility.GetThingList(intVec, Find.CurrentMap))
                {
                    if (processedThings.Contains(thing) || thing.MarketValue <= 0f) continue;

                    processedThings.Add(thing);
                    if (thing.def.Minifiable && thing.def.minifiedDef.tradeability == Tradeability.None)
                    {
                        valueNum += thing.MarketValue;
                    } else if (thing.def.CostList != null)
                    {
                        foreach (ThingDefCountClass cost in thing.def.CostList)
                        {
                            valueNum += cost.thingDef.BaseMarketValue * cost.count;
                        }
                    } else if (thing.def.CostStuffCount > 0)
                    {
                        valueNum += (thing.Stuff?.BaseMarketValue ?? 0f) * thing.def.CostStuffCount;
                    } else
                    {
                        valueNum += thing.MarketValue;
                    }
                }

                // 地形价值计算
                TerrainDef terrain = GridsUtility.GetTerrain(intVec, Find.CurrentMap);
                if (terrain != null)
                {
                    valueNum += terrain.GetStatValueAbstract(StatDefOf.MarketValue);
                }
            }
            return valueNum*0.4f;
        }

        private static List<ResearchProjectDef> CollectResearchPrerequisites(CellRect rect)
        {
            List<ResearchProjectDef> techList = new List<ResearchProjectDef>();
            foreach (IntVec3 intVec in rect)
            {
                // 物品研究依赖
                foreach (Thing thing in GridsUtility.GetThingList(intVec, Find.CurrentMap))
                {
                    CollectTechFromDef(thing.def, techList);
                }

                // 地形研究依赖
                TerrainDef terrain = GridsUtility.GetTerrain(intVec, Find.CurrentMap);
                if (terrain?.researchPrerequisites != null)
                {
                    foreach (var tech in terrain.researchPrerequisites)
                    {
                        if (!techList.Contains(tech)) techList.Add(tech);
                    }
                }
            }
            return techList;
        }

        private static void CollectTechFromDef(ThingDef def, List<ResearchProjectDef> techList)
        {
            if (def?.researchPrerequisites != null)
            {
                foreach (var tech in def.researchPrerequisites)
                {
                    if (!techList.Contains(tech)) techList.Add(tech);
                }
            }

            if (def?.recipeMaker != null)
            {
                // 处理列表依赖
                if (def.recipeMaker.researchPrerequisites != null)
                {
                    foreach (var tech in def.recipeMaker.researchPrerequisites)
                    {
                        if (!techList.Contains(tech)) techList.Add(tech);
                    }
                }

                // 处理单个依赖
                if (def.recipeMaker.researchPrerequisite != null &&
                    !techList.Contains(def.recipeMaker.researchPrerequisite))
                {
                    techList.Add(def.recipeMaker.researchPrerequisite);
                }
            }
        }

        private static List<string> CollectModDependencies(CellRect rect)
        {
            List<string> modList = new List<string>();
            foreach (IntVec3 intVec in rect)
            {
                // 物品 Mod 依赖
                foreach (Thing thing in GridsUtility.GetThingList(intVec, Find.CurrentMap))
                {
                    string packageId = thing.ContentSource?.PackageId?.ToLower();
                    if (IsValidModId(packageId)) AddUnique(modList, packageId);
                }

                // 地形 Mod 依赖
                TerrainDef terrain = GridsUtility.GetTerrain(intVec, Find.CurrentMap);
                string terrainModId = terrain?.modContentPack?.PackageId?.ToLower();
                if (IsValidModId(terrainModId)) AddUnique(modList, terrainModId);
            }
            return modList;
        }

        private static void AddUnique<T>(List<T> list, T item)
        {
            if (item != null && !list.Contains(item))
            {
                list.Add(item);
            }
        }

        private static bool IsValidModId(string packageId,bool needDLC=false)
        {
            return needDLC ? !string.IsNullOrEmpty(packageId) &&
                   packageId != "ludeon.rimworld" : !string.IsNullOrEmpty(packageId) &&
                   packageId != "ludeon.rimworld" &&
                   !packageId.StartsWith("ludeon.rimworld.");
        }
        #endregion

        #region 输出生成模块
        private static string BuildXmlString(float valueNum, List<ResearchProjectDef> techList, List<string> modList)
        {
            StringBuilder xml = new StringBuilder();

            xml.AppendLine("<marketvalue>" + Mathf.RoundToInt(valueNum) + "</marketvalue>");

            xml.AppendLine("<researchPrerequisites>");
            foreach (var tech in techList)
            {
                xml.AppendLine("    <li>" + tech.defName + "</li>");
            }
            xml.AppendLine("</researchPrerequisites>");

            xml.AppendLine("<modPrerequisites>");
            foreach (var mod in modList)
            {
                xml.AppendLine("    <li>" + mod + "</li>");
            }
            xml.Append("</modPrerequisites>");

            return xml.ToString();
        }
        #endregion

        #region 剪贴板解决方案
        private static void CopyToClipboard(string text)
        {
            try
            {
                // 方案一：使用 TextEditor (跨平台兼容)
                TextEditor textEditor = new TextEditor();
                textEditor.text = text;
                textEditor.SelectAll();
                textEditor.Copy();

                // 方案二：备用方法 (Windows 特化)
                // if (Application.platform == RuntimePlatform.WindowsPlayer)
                // {
                //     GUIUtility.systemCopyBuffer = text;
                // }

                Log.Message("[AlphaPrefabs] Clipboard content:\n" + text);
            } catch (Exception ex)
            {
                Log.Error("Clipboard copy failed: " + ex.ToString());
                Messages.Message("Copy failed! Check logs.", MessageTypeDefOf.RejectInput);
            }
        }
        #endregion

        [DebugAction("Alpha prefabs", "Copy Patch XML to Clipboard", false, false, false, false, 0, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCopyPatchXmlToClipboard()
        {
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("No active map found!", MessageTypeDefOf.RejectInput);
                return;
            }
            DebugToolsGeneral.GenericRectTool("patchxml", delegate (CellRect rect)
            {
                // 收集所有研究依赖及其来源Mod
                Dictionary<string, List<ResearchProjectDef>> modTechDict = CollectModTechDependencies(rect);

                // 生成XML字符串
                string xml = BuildPatchXmlString(modTechDict);

                // 复制到剪贴板
                CopyToClipboard(xml);
                if (modTechDict.Count == 0)
                {
                    Messages.Message("no need new patch!", MessageTypeDefOf.RejectInput);
                    return;
                }
                Messages.Message("Patch XML copied to clipboard!", MessageTypeDefOf.SilentInput);
            });
        }

        #region 新增核心逻辑
        private static Dictionary<string, List<ResearchProjectDef>> CollectModTechDependencies(CellRect rect)
        {
            Dictionary<string, List<ResearchProjectDef>> modTechDict = new Dictionary<string, List<ResearchProjectDef>>();

            foreach (IntVec3 intVec in rect)
            {
                // 遍历物品
                foreach (Thing thing in GridsUtility.GetThingList(intVec, Find.CurrentMap))
                {
                    ProcessDefForTechMod(thing.def, modTechDict);
                }

                // 遍历地形
                TerrainDef terrain = GridsUtility.GetTerrain(intVec, Find.CurrentMap);
                ProcessDefForTechMod(terrain, modTechDict);
            }
            return modTechDict;
        }

        private static void ProcessDefForTechMod(Def def, Dictionary<string, List<ResearchProjectDef>> modTechDict)
        {
            if (def == null) return;

            // 获取该Def的所有关联研究
            List<ResearchProjectDef> relatedTechs = new List<ResearchProjectDef>();
            if (def is ThingDef thingDef)
            {
                CollectTechFromDef(thingDef, relatedTechs);
            } else if (def is TerrainDef terrainDef && terrainDef.researchPrerequisites != null)
            {
                relatedTechs.AddRange(terrainDef.researchPrerequisites);
            }

            // 按Mod分类
            foreach (var tech in relatedTechs)
            {
                string packageId = tech.modContentPack?.PackageId?.ToLower();
                if (!IsValidModId(packageId,true)) continue;

                if (!modTechDict.ContainsKey(packageId))
                {
                    modTechDict[packageId] = new List<ResearchProjectDef>();
                }
                if (!modTechDict[packageId].Contains(tech))
                {
                    modTechDict[packageId].Add(tech);
                }
            }
        }


        private static string BuildPatchXmlString(Dictionary<string, List<ResearchProjectDef>> modTechDict)
        {
            StringBuilder xml = new StringBuilder();
            xml.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            xml.AppendLine("<Patch>");

            foreach (var kvp in modTechDict)
            {
                string modPackageId = kvp.Key;
                List<ResearchProjectDef> techs = kvp.Value;

                xml.AppendLine(@"  <Operation Class=""PatchOperationFindMod"">");
                xml.AppendLine("    <mods>");
                xml.AppendLine($"      <li>{modPackageId}</li>");
                xml.AppendLine("    </mods>");
                xml.AppendLine(@"    <match Class=""PatchOperationSequence"">");
                xml.AppendLine("      <operations>");
                xml.AppendLine(@"        <li Class=""PatchOperationAdd"">");
                xml.AppendLine(@"          <xpath>/Defs/AlphaPrefabs.PrefabDef[defName = ""BuildingDefineName1""]/researchPrerequisites</xpath>");
                xml.AppendLine("          <value>");

                // 合并同一Mod的所有科技到单个value节点
                foreach (var tech in techs)
                {
                    xml.AppendLine($"            <li>{tech.defName}</li>");
                }

                xml.AppendLine("          </value>");
                xml.AppendLine("        </li>");
                xml.AppendLine("      </operations>");
                xml.AppendLine("    </match>");
                xml.AppendLine("  </Operation>");
            }

            xml.Append("</Patch>");
            return xml.ToString();
        }
        #endregion
    }
}
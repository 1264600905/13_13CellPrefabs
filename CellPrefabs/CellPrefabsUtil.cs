using KCSG;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CellPrefabs
{
     class CellPrefabsUtil
    {

        public static BuildingInfo buildingInfo;

        // 导出文件夹路径 - 修改为游戏根目录下的MyBuildings文件夹
        private static string exportFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyBuildings");

        // 初始化方法
        static CellPrefabsUtil()
        {
            // 确保导出目录存在
            Directory.CreateDirectory(exportFolderPath);
            Log.Message($"[BuildingExporter] 导出目录已创建: {exportFolderPath}");

        }


        // 区域选择回调
        public static void OnCellRectSelected(CellRect rect)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("未找到活动地图!", MessageTypeDefOf.RejectInput);
                    return;
                }

                // 创建导出目录结构
                string buildingFolder = Path.Combine(exportFolderPath, buildingInfo.defName);
                string uiFolder = Path.Combine(buildingFolder, "Textures", "Prefabs_Preview"); // 修正UI文件夹路径
                string defsFolder = Path.Combine(buildingFolder, "Defs");
                string patchesFolder = Path.Combine(buildingFolder, "Patches");

                Directory.CreateDirectory(buildingFolder);
                Directory.CreateDirectory(uiFolder);
                Directory.CreateDirectory(defsFolder);
                Directory.CreateDirectory(patchesFolder);

                // 1. 导出预览图
                ExportPreviewImage(rect, uiFolder);

                // 2. 导出Defs文件
                ExportDefsFiles(rect, defsFolder);

                // 3. 导出Patch文件
                ExportPatchFile(rect, patchesFolder);

                Messages.Message($"建筑导出完成! 位置: {buildingFolder}", MessageTypeDefOf.SilentInput);
                Log.Message($"[BuildingExporter] 建筑导出完成: {buildingFolder}");

            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出失败: {ex.Message}");
                Messages.Message("导出建筑失败! 查看日志获取详情.", MessageTypeDefOf.RejectInput);
            }
        }

        // 导出预览图
        private static void ExportPreviewImage(CellRect rect, string uiFolder)
        {

            // 获取当前相机
            Camera camera = Find.Camera;


            // 计算相机参数对截图的影响
            float cameraHeight = camera.transform.position.y;
            float cameraAngle = camera.transform.eulerAngles.x;

            // 四个角的格子坐标（包括 .ToVector3Shifted() 保证是格子中心）
            Vector3[] worldCorners = new Vector3[]
            {
        new IntVec3(rect.minX, 0, rect.minZ).ToVector3Shifted(),
        new IntVec3(rect.minX, 0, rect.maxZ).ToVector3Shifted(),
        new IntVec3(rect.maxX, 0, rect.minZ).ToVector3Shifted(),
        new IntVec3(rect.maxX, 0, rect.maxZ).ToVector3Shifted(),
            };

            // 将世界坐标转为屏幕坐标
            Vector3[] screenCorners = worldCorners.Select(v => Find.Camera.WorldToScreenPoint(v)).ToArray();

            // 计算包围矩形的屏幕范围
            float xMin = screenCorners.Min(v => v.x);
            float xMax = screenCorners.Max(v => v.x);
            float yMin = screenCorners.Min(v => v.y);
            float yMax = screenCorners.Max(v => v.y);

            // 转换为 GUI 坐标（原点在左上）
            float x = xMin;
            float y = Screen.height - yMax;
            float width = xMax - xMin;
            float height = yMax - yMin;

            Rect screenRect = new Rect(x, y, width, height);

            // 读取并保存截图
            Texture2D tex = new Texture2D((int)width, (int)height, TextureFormat.RGB24, false);
            tex.ReadPixels(screenRect, 0, 0);
            tex.Apply();

            string previewPath = Path.Combine(uiFolder, $"{buildingInfo.defName}_Preview.png");
            File.WriteAllBytes(previewPath, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);

            Log.Message($"[BuildingExporter] 预览图导出完成: {previewPath}");
        }


        // 导出Defs文件
        // 导出Defs文件
        private static void ExportDefsFiles(CellRect rect, string defsFolder)
        {
            try
            {
                Map map = Find.CurrentMap;

                // 创建临时数据存储
                List<IntVec3> selectedCells = rect.Cells.ToList();
                Dictionary<IntVec3, List<Thing>> cellThings = new Dictionary<IntVec3, List<Thing>>();

                // 填充单元格与物品的映射
                foreach (IntVec3 cell in selectedCells)
                {
                    cellThings[cell] = map.thingGrid.ThingsListAt(cell).ToList();
                }

                // 创建并配置StructureLayoutDef (用于BuildingLayouts.xml)
                StructureLayoutDef structureDef = new StructureLayoutDef
                {
                    defName = buildingInfo.defName,
                    spawnConduits = true,
                    forceGenerateRoof = false,
                    needRoofClearance = false,
                    randomizeWallStuffAtGen = true,
                    tags = new List<string> { "Exported", "Custom" },
                    terrainGrid = CreateTerrainLayout(rect, map),
                    roofGrid = CreateRoofGrid(rect, map),
                    modRequirements = GetNeededMods(selectedCells, cellThings),
                    layouts = CreateLayouts(selectedCells, cellThings, rect)
                };

                // 创建并添加SymbolDefs
                List<SymbolDef> symbolDefs = CreateSymbolDefs(selectedCells, cellThings, map);

                // 生成PrefabDef (用于PrefabDefs.xml)
                string prefabCategory = buildingInfo.category;
                string prefabLabel =  buildingInfo.defName;
                string prefabDescription = buildingInfo.description;
                int marketValue = (int)CalculateMarketValue(rect);

                // 收集并分类研究前提条件
                List<string> vanillaResearch;
                Dictionary<string, List<ResearchProjectDef>> modResearch;
                CollectAndClassifyResearchPrerequisites(rect, out vanillaResearch, out modResearch);

                // 创建PrefabDef XML，只包含本体科技
                string prefabDefXml = GeneratePrefabDefXml(
                    buildingInfo.defName,
                    buildingInfo.label,
                    buildingInfo.label,
                    buildingInfo.description,
                    buildingInfo.priority,
                    buildingInfo.category,
                    "Prefabs_Preview/" + buildingInfo.defName + "_Preview",
                    buildingInfo.defName,
                    buildingInfo.author,
                    marketValue,
                    vanillaResearch
                );

                // 生成XML内容
                string symbolDefsXml = GenerateSymbolDefsXml(symbolDefs);
                string structureDefXml = GenerateBuildingLayoutsXml(structureDef); // 包装在<Defs>标签中

                // 保存到文件 - 使用正确的文件名
                string symbolDefsPath = Path.Combine(defsFolder, "SymbolDefs.xml");
                string buildingLayoutsPath = Path.Combine(defsFolder, "BuildingLayouts.xml");
                string prefabDefsPath = Path.Combine(defsFolder, "PrefabDefs.xml");

                // 只保存非空XML
                SaveIfNotEmpty(symbolDefsPath, symbolDefsXml);
                SaveIfNotEmpty(buildingLayoutsPath, structureDefXml);
                SaveIfNotEmpty(prefabDefsPath, prefabDefXml);

                Log.Message($"[BuildingExporter] Defs文件导出完成: {symbolDefsPath}, {buildingLayoutsPath} 和 {prefabDefsPath}");
            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出Defs文件失败: {ex.Message}");
                throw;
            }
        }

        // 修复BuildingLayouts.xml的标签问题
        private static string GenerateBuildingLayoutsXml(StructureLayoutDef structureDef)
        {
            // 生成基本XML内容
            string xmlContent = structureDef.ToXMLString();

            // 检查是否有标签
            if (structureDef.tags == null || structureDef.tags.Count == 0)
            {
                // 替换空标签列表为<tag/>
                xmlContent = Regex.Replace(xmlContent, @"<tags>\s*<\/tags>", "<tag/>");
            }

            return WrapInDefsTag(xmlContent);
        }

        // 使用你提供的改进版MarketValue计算公式
        private static float CalculateMarketValue(CellRect rect)
        {
            float totalValue = 0f;
            List<Thing> processedThings = new List<Thing>();

            foreach (IntVec3 cell in rect)
            {
                foreach (Thing thing in cell.GetThingList(Find.CurrentMap))
                {
                    // 跳过已处理或无价值的物品
                    if (processedThings.Contains(thing) || thing.MarketValue <= 0f)
                        continue;

                    processedThings.Add(thing);

                    // 处理可最小化物品
                    if (thing.def.Minifiable && thing.def.minifiedDef.tradeability == Tradeability.None)
                    {
                        totalValue += thing.MarketValue;
                    }
                    // 处理有成本列表的物品
                    else if (thing.def.CostList != null)
                    {
                        foreach (ThingDefCountClass costItem in thing.def.CostList)
                        {
                            totalValue += costItem.thingDef.BaseMarketValue * costItem.count;
                        }
                    }
                    // 处理有材料成本的物品
                    else if (thing.def.CostStuffCount > 0)
                    {
                        totalValue += (thing.Stuff?.BaseMarketValue ?? 0f) * thing.def.CostStuffCount;
                    }
                    // 其他情况
                    else
                    {
                        totalValue += thing.MarketValue;
                    }
                }

                // 添加地形价值
                TerrainDef terrain = cell.GetTerrain(Find.CurrentMap);
                if (terrain != null)
                {
                    totalValue += terrain.GetStatValueAbstract(StatDefOf.MarketValue, null);
                }
            }

            return totalValue;
        }



        // 生成PrefabDef XML
        private static string GeneratePrefabDefXml(
            string defName,
            string label,
            string shortLabel,
            string description,
            int priority,
            string category,
            string detailedImage,
            string layout,
            string author,
            int marketValue,
            List<string> researchPrerequisites)
        {
            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            xml.AppendLine("<Defs>");
            xml.AppendLine("  <AlphaPrefabs.PrefabDef>");
            xml.AppendLine($"    <defName>{defName}</defName>");
            xml.AppendLine($"    <label>{label}</label>");
            xml.AppendLine($"    <shortLabel>{shortLabel}</shortLabel>");
            xml.AppendLine($"    <description>{description}</description>");
            xml.AppendLine($"    <priority>{priority}</priority>");
            xml.AppendLine($"    <category>{category}</category>");
            xml.AppendLine($"    <detailedImage>{detailedImage}</detailedImage>");
            xml.AppendLine($"    <layout>{layout}</layout>");
            xml.AppendLine($"    <author>{author}</author>");
            xml.AppendLine($"    <marketvalue>{marketValue}</marketvalue>");

            // 添加研究前提条件
            xml.AppendLine("    <researchPrerequisites>");
            foreach (string research in researchPrerequisites)
            {
                xml.AppendLine($"      <li>{research}</li>");
            }
            xml.AppendLine("    </researchPrerequisites>");

            // 添加空的suggestedMods标签
            xml.AppendLine("    <suggestedMods/>");

            xml.AppendLine("  </AlphaPrefabs.PrefabDef>");
            xml.AppendLine("</Defs>");

            return xml.ToString();
        }

        // 将内容包装在<Defs>标签中
        private static string WrapInDefsTag(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            return $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Defs>
{content}
</Defs>";
        }

        // 仅当内容不为空时保存文件
        private static void SaveIfNotEmpty(string filePath, string content)
        {
            if (!string.IsNullOrWhiteSpace(content) &&
                !content.Trim().Equals("<?xml version=\"1.0\" encoding=\"utf-8\" ?>") &&
                !content.Trim().Equals("<Defs></Defs>") &&
                !content.Trim().Equals("<?xml version=\"1.0\" encoding=\"utf-8\" ?><Defs></Defs>"))
            {
                File.WriteAllText(filePath, content);
                Log.Message($"[BuildingExporter] 保存文件: {filePath}");
            } else
            {
                Log.Message($"[BuildingExporter] 内容为空，跳过保存: {filePath}");
            }
        }

        // 导出Patch文件
        private static void ExportPatchFile(CellRect rect, string patchesFolder)
        {
            try
            {
                // 收集并分类研究前提条件
                List<string> vanillaResearch;
                Dictionary<string, List<ResearchProjectDef>> modResearch;
                CollectAndClassifyResearchPrerequisites(rect, out vanillaResearch, out modResearch);

                // 生成Patch XML，只包含模组科技
                string patchXml = BuildPatchXmlString(modResearch);

                // 改进空XML检测 - 检查是否有实际内容
                bool hasContent = !string.IsNullOrWhiteSpace(patchXml) &&
                                  patchXml.Contains("<Operation") &&
                                  !patchXml.Contains("<Patch></Patch>");

                if (hasContent)
                {
                    string patchPath = Path.Combine(patchesFolder, "Patch.xml");
                    File.WriteAllText(patchPath, patchXml);
                    Log.Message($"[BuildingExporter] Patch文件导出完成: {patchPath}");
                } else
                {
                    Log.Message($"[BuildingExporter] 没有需要导出的Patch内容");
                }
            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出Patch文件失败: {ex.Message}");
                throw;
            }
        }



        // 创建地形布局
        private static List<string> CreateTerrainLayout(CellRect rect, Map map)
        {
            List<string> terrainLayout = new List<string>();

            for (int z = rect.minZ; z <= rect.maxZ; z++)
            {
                string row = "";
                for (int x = rect.minX; x <= rect.maxX; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    row += terrain?.defName ?? ".";

                    if (x < rect.maxX)
                        row += ",";
                }
                terrainLayout.Add(row);
            }

            return terrainLayout;
        }

        // 创建屋顶网格
        private static List<string> CreateRoofGrid(CellRect rect, Map map)
        {
            List<string> roofGrid = new List<string>();

            for (int z = rect.minZ; z <= rect.maxZ; z++)
            {
                string row = "";
                for (int x = rect.minX; x <= rect.maxX; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    RoofDef roof = map.roofGrid.RoofAt(cell);

                    if (roof == null)
                        row += ".";
                    else if (roof == RoofDefOf.RoofRockThick)
                        row += "3";
                    else if (roof == RoofDefOf.RoofRockThin)
                        row += "2";
                    else
                        row += "1";

                    if (x < rect.maxX)
                        row += ",";
                }
                roofGrid.Add(row);
            }

            return roofGrid;
        }

        // 获取所需模组
        private static List<string> GetNeededMods(List<IntVec3> cells, Dictionary<IntVec3, List<Thing>> cellThings)
        {
            HashSet<string> modIds = new HashSet<string>();

            foreach (IntVec3 cell in cells)
            {
                foreach (Thing thing in cellThings[cell])
                {
                    if (thing.def.modContentPack != null &&
                        !thing.def.modContentPack.PackageId.NullOrEmpty() &&
                        !thing.def.modContentPack.PackageId.StartsWith("ludeon.rimworld"))
                    {
                        modIds.Add(thing.def.modContentPack.PackageId);
                    }
                }
            }

            return modIds.ToList();
        }

        // 创建SymbolDefs（优化后）
        private static List<SymbolDef> CreateSymbolDefs(List<IntVec3> cells, Dictionary<IntVec3, List<Thing>> cellThings, Map map)
        {
            List<SymbolDef> symbolDefs = new List<SymbolDef>();
            Dictionary<string, SymbolDef> defNameToSymbol = new Dictionary<string, SymbolDef>();

            foreach (IntVec3 cell in cells)
            {
                foreach (Thing thing in cellThings[cell])
                {
                    // 跳过物品和污物（根据现有逻辑保留）
                    if (thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Filth)
                        continue;

                    // 生成简化的SymbolDef名称
                    string defName = GenerateSymbolDefName(thing);

                    // 避免重复创建相同的SymbolDef
                    if (defNameToSymbol.ContainsKey(defName))
                        continue;

                    // 创建新的SymbolDef
                    SymbolDef symbolDef = new SymbolDef
                    {
                        defName = defName,
                        thing = thing.def.defName,
                        // 仅为可旋转且非默认方向的物品设置rotation
                        rotation = thing.def.rotatable && thing.Rotation != Rot4.North ? thing.Rotation : Rot4.North,
                        // 仅为需要材质信息的物品设置stuff
                        stuff = ShouldIncludeStuff(thing) ? thing.Stuff?.defName : null
                    };

                    // 仅为有颜色的建筑设置color
                    Building building = thing as Building;
                    if (building != null && building.PaintColorDef != null)
                    {
                        symbolDef.color = building.PaintColorDef.defName;
                    }

                    // 解析引用
                    symbolDef.ResolveReferences();

                    symbolDefs.Add(symbolDef);
                    defNameToSymbol[defName] = symbolDef;
                }
            }

            return symbolDefs;
        }

        // 创建布局
        private static List<List<string>> CreateLayouts(List<IntVec3> cells, Dictionary<IntVec3, List<Thing>> cellThings, CellRect rect)
        {
            List<List<string>> layouts = new List<List<string>>();

            // 确定最大层数（每个单元格可能有多个物品堆叠）
            int maxLayers = 0;
            foreach (var cell in cells)
            {
                int validThingsCount = cellThings[cell]
                    .Count(t => t.def.category != ThingCategory.Item && t.def.category != ThingCategory.Filth);
                maxLayers = Math.Max(maxLayers, validThingsCount);
            }

            // 为每一层创建布局
            for (int layer = 0; layer < maxLayers; layer++)
            {
                List<string> layerLayout = new List<string>();

                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    string row = "";
                    for (int x = rect.minX; x <= rect.maxX; x++)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);

                        // 获取该单元格的有效物品（排除Item和Filth）
                        var validThings = cellThings[cell]
                            .Where(t => t.def.category != ThingCategory.Item && t.def.category != ThingCategory.Filth)
                            .ToList();

                        // 如果该层有物品，则添加对应的SymbolDef名称
                        if (layer < validThings.Count)
                        {
                            Thing thing = validThings[layer];
                            string symbolDefName = GenerateSymbolDefName(thing);
                            row += symbolDefName;
                        } else
                        {
                            // 否则添加占位符
                            row += ".";
                        }

                        if (x < rect.maxX)
                            row += ",";
                    }

                    layerLayout.Add(row);
                }

                layouts.Add(layerLayout);
            }

            return layouts;
        }

        // 简化版的符号名称生成（减少不必要的后缀）
        private static string GenerateSymbolDefName(Thing thing)
        {
            string baseName = thing.def.defName;

            // 仅为需要特殊处理的建筑添加额外标识
            if (thing is Building building)
            {
                // 为有颜色的建筑添加颜色标识
                if (building.PaintColorDef != null)
                {
                    baseName += "_" + building.PaintColorDef.defName;
                }

                // 为可旋转建筑添加旋转标识（仅当非默认方向时）
                if (thing.def.rotatable && thing.Rotation != Rot4.North)
                {
                    baseName += "_Rot" + thing.Rotation.AsInt;
                }
            }

            // 为有材质的物品添加材质标识（仅当材质影响外观时）
            if (thing.Stuff != null && ShouldIncludeStuff(thing))
            {
                baseName += "_" + thing.Stuff.defName;
            }

            return baseName;
        }

        // 判断是否应包含材质信息（避免为所有物品都添加材质字段）
        private static bool ShouldIncludeStuff(Thing thing)
        {
            // 仅为建筑、家具等需要材质的物品添加材质信息
            return thing.def.category == ThingCategory.Building ||
                   thing.def.category == ThingCategory.Filth ||
                   thing.def.MadeFromStuff;
        }

        // 生成SymbolDefs的XML（优化后）
        private static string GenerateSymbolDefsXml(List<SymbolDef> symbolDefs)
        {
            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.AppendLine("<Defs>");

            foreach (SymbolDef symbolDef in symbolDefs)
            {
                xml.AppendLine($"  <KCSG.SymbolDef>");
                xml.AppendLine($"    <defName>{EscapeXml(symbolDef.defName)}</defName>");
                xml.AppendLine($"    <thing>{EscapeXml(symbolDef.thing)}</thing>");

                // 仅当rotation非默认值时添加
                if (symbolDef.rotation != Rot4.North)
                {
                    xml.AppendLine($"    <rotation>{symbolDef.rotation.AsInt}</rotation>");
                }

                // 仅当stuff有值时添加
                if (!string.IsNullOrEmpty(symbolDef.stuff))
                {
                    xml.AppendLine($"    <stuff>{EscapeXml(symbolDef.stuff)}</stuff>");
                }

                // 仅当color有值时添加
                if (!string.IsNullOrEmpty(symbolDef.color))
                {
                    xml.AppendLine($"    <color>{EscapeXml(symbolDef.color)}</color>");
                }

                xml.AppendLine($"  </KCSG.SymbolDef>");
            }

            xml.AppendLine("</Defs>");
            return xml.ToString();
        }

        // 简单的XML转义方法
        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
        }

        // 构建Patch XML字符串
        private static string BuildPatchXmlString(Dictionary<string, List<ResearchProjectDef>> modTechDict)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            stringBuilder.AppendLine("<Patch>");
            foreach (KeyValuePair<string, List<ResearchProjectDef>> keyValuePair in modTechDict)
            {
                string key = keyValuePair.Key;
                List<ResearchProjectDef> value = keyValuePair.Value;
                stringBuilder.AppendLine("  <Operation Class=\"PatchOperationFindMod\">");
                stringBuilder.AppendLine("    <mods>");
                stringBuilder.AppendLine("      <li>" + key + "</li>");
                stringBuilder.AppendLine("    </mods>");
                stringBuilder.AppendLine("    <match Class=\"PatchOperationSequence\">");
                stringBuilder.AppendLine("      <operations>");
                stringBuilder.AppendLine("        <li Class=\"PatchOperationAdd\">");
                stringBuilder.AppendLine("          <xpath>/Defs/AlphaPrefabs.PrefabDef[defName = \"" + buildingInfo.defName + "\"]/researchPrerequisites</xpath>");
                stringBuilder.AppendLine("          <value>");
                foreach (ResearchProjectDef researchProjectDef in value)
                {
                    stringBuilder.AppendLine("            <li>" + researchProjectDef.defName + "</li>");
                }
                stringBuilder.AppendLine("          </value>");
                stringBuilder.AppendLine("        </li>");
                stringBuilder.AppendLine("      </operations>");
                stringBuilder.AppendLine("    </match>");
                stringBuilder.AppendLine("  </Operation>");
            }
            stringBuilder.Append("</Patch>");
            return stringBuilder.ToString();
        }

        // 收集并分类所有研究前提条件（区分本体、DLC和模组）
        private static void CollectAndClassifyResearchPrerequisites(CellRect rect,
                                                                     out List<string> vanillaTechs,
                                                                     out Dictionary<string, List<ResearchProjectDef>> modTechDict)
        {
            vanillaTechs = new List<string>();
            modTechDict = new Dictionary<string, List<ResearchProjectDef>>();

            // 用于去重的集合
            HashSet<ResearchProjectDef> allResearch = new HashSet<ResearchProjectDef>();

            foreach (IntVec3 cell in rect)
            {
                // 处理建筑
                foreach (Thing thing in cell.GetThingList(Find.CurrentMap))
                {
                    // 跳过物品和污物
                    if (thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Filth)
                        continue;

                    // 添加建筑的研究前提
                    if (thing.def.researchPrerequisites != null)
                    {
                        foreach (var research in thing.def.researchPrerequisites)
                        {
                            if (research != null)
                                allResearch.Add(research);
                        }
                    }

                    // 添加建筑配方的研究前提
                    if (thing.def.recipeMaker != null)
                    {
                        if (thing.def.recipeMaker.researchPrerequisites != null)
                        {
                            foreach (var research in thing.def.recipeMaker.researchPrerequisites)
                            {
                                if (research != null)
                                    allResearch.Add(research);
                            }
                        }

                        if (thing.def.recipeMaker.researchPrerequisite != null)
                        {
                            allResearch.Add(thing.def.recipeMaker.researchPrerequisite);
                        }
                    }
                }

                // 处理地形
                TerrainDef terrain = cell.GetTerrain(Find.CurrentMap);
                if (terrain != null && terrain.researchPrerequisites != null)
                {
                    foreach (var research in terrain.researchPrerequisites)
                    {
                        if (research != null)
                            allResearch.Add(research);
                    }
                }
            }

            // 分类处理收集到的科技
            foreach (ResearchProjectDef research in allResearch)
            {
                if (research == null) continue;

                string packageId = research.modContentPack?.PackageId?.ToLower();

                // 判断是本体科技还是模组/DLC科技
                if (string.IsNullOrEmpty(packageId) || packageId == "ludeon.rimworld")
                {
                    // 本体科技
                    vanillaTechs.Add(research.defName);
                } else
                {
                    // 模组或DLC科技 - 使用IsValidModId判断
                    if (IsValidModId(packageId, true)) // true表示需要包含DLC
                    {
                        if (!modTechDict.ContainsKey(packageId))
                            modTechDict[packageId] = new List<ResearchProjectDef>();

                        modTechDict[packageId].Add(research);
                    } else
                    {
                        // 这应该不会发生，因为上面已经排除了ludeon.rimworld
                        vanillaTechs.Add(research.defName);
                    }
                }
            }
        }

        private static bool IsValidModId(string packageId, bool needDLC = false)
        {
            return needDLC ? (!string.IsNullOrEmpty(packageId) && packageId != "ludeon.rimworld") : (!string.IsNullOrEmpty(packageId) && packageId != "ludeon.rimworld" && !packageId.StartsWith("ludeon.rimworld."));
        }


    }
}

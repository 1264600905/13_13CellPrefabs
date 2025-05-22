using KCSG;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            // 使用反射调用KCSG.StartupActions的Initialize方法
        

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

                // 确保在鼠标按下时记录点击位置
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    // 获取当前鼠标在屏幕上的位置（屏幕坐标，左下原点）
                    Vector2 currentMousePosition = Event.current.mousePosition;

                    // 处理第一次点击
                    if (!firstClickPosition.HasValue)
                    {
                        firstClickPosition = currentMousePosition;
                        Messages.Message("建筑左上角已经选择，请点击建筑的右下角完成选择", MessageTypeDefOf.SilentInput);
                        return;
                    }

                    // 检测右键点击取消操作
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
                    {
                        ResetClickPositions();
                        Messages.Message("已取消建筑选择", MessageTypeDefOf.SilentInput);
                        return;
                    }


                    // 处理第二次点击
                    secondClickPosition = currentMousePosition;

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
                    ExportPreviewImageUsingClicks(uiFolder);

                    // 2. 导出Defs文件
                    ExportDefsFiles(rect, defsFolder);

                    // 3. 导出Patch文件
                    ExportPatchFile(rect, patchesFolder);

                    Messages.Message($"建筑导出完成! 位置: {buildingFolder}", MessageTypeDefOf.SilentInput);
                    Log.Message($"[BuildingExporter] 建筑导出完成: {buildingFolder}");
                    ResetClickPositions();
                }
            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出失败: {ex.Message}");
                Messages.Message("导出建筑失败! 查看日志获取详情.", MessageTypeDefOf.RejectInput);
            }
        }

        // 存储玩家的两次点击位置
        public static Vector2? firstClickPosition = null;
        public static Vector2? secondClickPosition = null;
        // 重置点击位置
        public static void ResetClickPositions()
        {
            firstClickPosition = null;
            secondClickPosition = null;
        }
        // 使用两次点击的位置导出预览图
        private static void ExportPreviewImageUsingClicks(string uiFolder)
        {
            try
            {
                if (!firstClickPosition.HasValue || !secondClickPosition.HasValue)
                {
                    throw new InvalidOperationException("未记录两次点击位置");
                }

                // 获取两次点击的位置
                Vector2 click1 = firstClickPosition.Value;
                Vector2 click2 = secondClickPosition.Value;

                // 计算截图区域的边界（确保坐标是从左到右，从上到下）
                float left = Mathf.Min(click1.x, click2.x);
                float right = Mathf.Max(click1.x, click2.x);
                float top = Mathf.Min(click1.y, click2.y);
                float bottom = Mathf.Max(click1.y, click2.y);

                // 添加一些边距
                const float padding = 20f;
                left = Mathf.Max(0, left - padding);
                right = Mathf.Min(UI.screenWidth, right + padding);
                top = Mathf.Max(0, top - padding);
                bottom = Mathf.Min(UI.screenHeight, bottom + padding);

                // 计算截图区域的尺寸
                float width = right - left;
                float height = bottom - top;

                // 转换为屏幕坐标（左下原点）
                float screenHeight = UI.screenHeight;
                float screenLeft = left;
                float screenBottom = screenHeight - bottom;

                // 创建屏幕坐标的截图区域
                Rect screenRect = new Rect(screenLeft, screenBottom, width, height);

                // 截图并保存
                Texture2D tex = new Texture2D((int)width, (int)height, TextureFormat.RGB24, false);
                tex.ReadPixels(screenRect, 0, 0);
                tex.Apply();

                string previewPath = Path.Combine(uiFolder, $"{buildingInfo.defName}_Preview.png");
                File.WriteAllBytes(previewPath, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);

                Log.Message($"[BuildingExporter] 预览图导出完成: {previewPath}");
            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出预览图失败: {ex.Message}");
                throw;
            }
        }


        // 导出Defs文件
        private static void ExportDefsFiles(CellRect rect, string defsFolder)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;

                // 获取选区单元格列表
                List<IntVec3> selectedCells = rect.Cells.ToList();

                // 初始化KCSG静态变量
                Dialog_ExportWindow.cells = selectedCells;
                Dialog_ExportWindow.pairsCellThingList = ExportUtils.FillCellThingsList(map);
                Dialog_ExportWindow.defName = buildingInfo.defName;
                Dialog_ExportWindow.tags = new HashSet<string> { "Exported", "Custom" };
                //导出管道
                Dialog_ExportWindow.spawnConduits = true;
                //不随机墙壁材质
                Dialog_ExportWindow.randomizeWallStuffAtGen = false ;
                //不导出污垢
                Dialog_ExportWindow.exportFilth = false;

                //不导出地形
                Dialog_ExportWindow.exportNatural = buildingInfo.exportNatural;
                //不导出植物
                Dialog_ExportWindow.exportPlant = buildingInfo.exportPlant;

                //不随机填充
                Dialog_ExportWindow.isStorage = false;

                // 生成符号定义和布局（使用KCSG原生方法）
                List<SymbolDef> symbolDefs = ExportUtils.CreateSymbolIfNeeded(null);


                // 生成原始布局
                var originalDef = ExportUtils.CreateStructureDef(map, null);
                //对数据新清洗，处理掉多余的非英语，数字和下划线的符号
                //希望没有小天才会把DefName命名成中文
                var originalLayoutStr = originalDef.ToXMLString();
                originalLayoutStr= ProcessXmlString(originalLayoutStr);
                if (buildingInfo.exportCreatureAndItem)
                {
                    originalLayoutStr = StructureLayoutFilter.FilterCreaturesAndItems(originalLayoutStr);
                }

                // 保持与DebugActions.CalculateMarketValue一致的逻辑
                float marketValue = CalculateMarketValue(rect);
                // 收集研究前提（区分本体和非本体）
                List<ResearchProjectDef> vanillaResearch;
                Dictionary<string, List<ResearchProjectDef>> modResearch;
                CollectAndClassifyResearchPrerequisites(rect, out vanillaResearch, out modResearch);

                // 转换为string列表（仅保留本体科技）
                List<string> vanillaResearchNames = vanillaResearch
                    .Select(r => r.defName)
                    .ToList();

                // 生成PrefabDef XML（只包含本体科技）
                string prefabDefXml = GeneratePrefabDefXml(
                    buildingInfo.defName,
                    buildingInfo.label,
                    buildingInfo.shortLabel,
                    buildingInfo.description,
                    buildingInfo.priority,
                    buildingInfo.category,
                    "Prefabs_Preview/" + buildingInfo.defName + "_Preview",
                    buildingInfo.defName,
                    buildingInfo.author,
                    marketValue,
                    vanillaResearchNames, // 只传递本体科技
                    CollectModDependencies(rect)
                );

                // 生成XML内容（使用KCSG的ToXMLString()）
                string symbolDefsXml = string.Join("\n", symbolDefs.Select(s => s.ToXMLString()));
                //清洗SymbolDefsXml
                symbolDefsXml = ProcessXmlString(symbolDefsXml);

                string structureDefXml = originalLayoutStr;

                // 保存文件
                string symbolDefsPath = Path.Combine(defsFolder, "SymbolDefs.xml");
                string buildingLayoutsPath = Path.Combine(defsFolder, "BuildingLayouts.xml");
                string prefabDefsPath = Path.Combine(defsFolder, "PrefabDefs.xml");

                SaveIfNotEmpty(symbolDefsPath, WrapInDefsTag(symbolDefsXml));
                SaveIfNotEmpty(buildingLayoutsPath, WrapInDefsTag(structureDefXml));
                SaveIfNotEmpty(prefabDefsPath, prefabDefXml);

                Log.Message($"[BuildingExporter] Defs文件导出完成: {symbolDefsPath}, {buildingLayoutsPath}, {prefabDefsPath}");
            } catch (Exception ex)
            {
                Log.Error($"[BuildingExporter] 导出Defs文件失败: {ex.Message}");
                throw;
            }
        }


        public static string ProcessXmlString(string xmlString)
        {
            // 匹配模式：下划线后跟随1个或多个中文字符（简体/繁体），无论前后是否有其他字符
            string pattern = @"_[\u4e00-\u9fa5\u3400-\u4dbf\uff00-\uffef]+";
            // 替换规则：删除下划线+中文字符段
            return Regex.Replace(xmlString, pattern, "");
        }

        private static List<string> CollectModDependencies(CellRect rect)
        {
            List<string> modIds = new List<string>();
            foreach (IntVec3 cell in rect)
            {
                // 收集物品的模组依赖
                foreach (Thing thing in cell.GetThingList(Find.CurrentMap))
                {
                    string packageId = thing.def?.modContentPack?.PackageId?.ToLower();
                    if (IsValidModId(packageId, false))
                        AddUnique(modIds, packageId);
                }
                // 收集地形的模组依赖
                TerrainDef terrain = cell.GetTerrain(Find.CurrentMap);
                string terrainPackageId = terrain?.modContentPack?.PackageId?.ToLower();
                if (IsValidModId(terrainPackageId, false))
                    AddUnique(modIds, terrainPackageId);
            }
            return modIds;
        }

        // 辅助方法：添加唯一元素
        private static void AddUnique<T>(List<T> list, T item)
        {
            if (item != null && !list.Contains(item))
                list.Add(item);
        }

        private static bool IsItemSymbol(string symbolName)
        {
            if (string.IsNullOrEmpty(symbolName) || symbolName == ".") return false;

            // 尝试获取符号对应的 ThingDef
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(symbolName);
            if (thingDef != null)
            {
                // 判断是否为物品（非建筑、非地形）
                return thingDef.category == ThingCategory.Item
                    || thingDef.category == ThingCategory.Pawn
                    || thingDef.category == ThingCategory.Plant;
            }

            // 处理特殊符号（如武器、服装等，可能属于 Item 类别）
            return true; // 默认为物品，除非明确是建筑/地形
        }

        private static StructureLayoutDef StripItemInfo(StructureLayoutDef def)
        {
            // 原始 layouts 是 List<List<string>>，需要遍历每一层列表
            List<List<string>> filteredLayouts = new List<List<string>>();

            foreach (List<string> layoutRow in def.layouts) // 遍历每一行（List<string>）
            {
                List<string> filteredRow = new List<string>();
                foreach (string symbol in layoutRow) // 遍历每个符号
                {
                    if (IsItemSymbol(symbol))
                    {
                        filteredRow.Add("."); // 是物品则替换为空白符号
                    } else
                    {
                        filteredRow.Add(symbol);
                    }
                }
                filteredLayouts.Add(filteredRow); // 添加过滤后的行（List<string>）到嵌套列表
            }

            def.layouts = filteredLayouts; // 赋值类型匹配 List<List<string>>
            return def;
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
        // 修改方法签名，接受List<string>类型的研究前提
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
            float marketValue,
            List<string> researchPrerequisites, // 修改为List<string>
            List<string> modPrerequisites)
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


            // 研究前提条件（直接使用string列表）
            xml.AppendLine("    <researchPrerequisites>");
            foreach (var research in researchPrerequisites)
            {
                xml.AppendLine($"      <li>{research}</li>");
            }
            xml.AppendLine("    </researchPrerequisites>");

            // 模组依赖（AlphaPrefabs使用modPrerequisites）
            xml.AppendLine("    <modPrerequisites>");
            foreach (var mod in modPrerequisites)
            {
                xml.AppendLine($"      <li>{mod}</li>");
            }
            xml.AppendLine("    </modPrerequisites>");

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
                List<ResearchProjectDef> vanillaTechs;
                Dictionary<string, List<ResearchProjectDef>> modTechDict;
                CollectAndClassifyResearchPrerequisites(rect, out vanillaTechs, out modTechDict);

                string patchXml = BuildPatchXmlString(modTechDict, buildingInfo.defName);

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



        // 判断是否应包含材质信息（避免为所有物品都添加材质字段）
        private static bool ShouldIncludeStuff(Thing thing)
        {
            // 仅为建筑、家具等需要材质的物品添加材质信息
            return thing.def.category == ThingCategory.Building ||
                   thing.def.category == ThingCategory.Filth ||
                   thing.def.MadeFromStuff;
        }


        // 构建Patch XML字符串
        private static string BuildPatchXmlString(
     Dictionary<string, List<ResearchProjectDef>> modTechDict,
     string prefabDefName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            stringBuilder.AppendLine("<Patch>");

            foreach (var kvp in modTechDict)
            {
                string modId = kvp.Key;
                List<ResearchProjectDef> techs = kvp.Value;

                // 跳过本体科技（只处理模组和DLC科技）
                if (modId == "ludeon.rimworld" || string.IsNullOrEmpty(modId))
                    continue;

                stringBuilder.AppendLine("  <Operation Class=\"PatchOperationFindMod\">");
                stringBuilder.AppendLine($"    <mods><li>{modId}</li></mods>");
                stringBuilder.AppendLine("    <match Class=\"PatchOperationSequence\">");
                stringBuilder.AppendLine("      <operations>");
                stringBuilder.AppendLine("        <li Class=\"PatchOperationAdd\">");
                stringBuilder.AppendLine($"          <xpath>/Defs/AlphaPrefabs.PrefabDef[defName = \"{prefabDefName}\"]/researchPrerequisites</xpath>");
                stringBuilder.AppendLine("          <value>");

                foreach (var tech in techs)
                {
                    stringBuilder.AppendLine($"            <li>{tech.defName}</li>");
                }

                stringBuilder.AppendLine("          </value>");
                stringBuilder.AppendLine("        </li>");
                stringBuilder.AppendLine("      </operations>");
                stringBuilder.AppendLine("    </match>");
                stringBuilder.AppendLine("  </Operation>");
            }

            stringBuilder.AppendLine("</Patch>");
            return stringBuilder.ToString();
        }


        // 修改方法定义（输出List<string>）
        // 保持原方法不变，输出ResearchProjectDef列表
        private static void CollectAndClassifyResearchPrerequisites(
            CellRect rect,
            out List<ResearchProjectDef> vanillaResearch,
            out Dictionary<string, List<ResearchProjectDef>> modResearch)
        {
            vanillaResearch = new List<ResearchProjectDef>();
            modResearch = new Dictionary<string, List<ResearchProjectDef>>();

            List<ResearchProjectDef> allResearch = CollectResearchPrerequisites(rect);

            foreach (var research in allResearch)
            {
                string packageId = research.modContentPack?.PackageId?.ToLower() ?? "";
                bool isVanilla = packageId == "ludeon.rimworld" || string.IsNullOrEmpty(packageId);

                if (isVanilla)
                {
                    vanillaResearch.Add(research);
                } else
                {
                    // 非本体科技（模组或DLC）
                    if (!modResearch.ContainsKey(packageId))
                        modResearch[packageId] = new List<ResearchProjectDef>();

                    modResearch[packageId].Add(research);
                }
            }
        }

        // 新增CollectResearchPrerequisites方法（复制DebugActions逻辑）
        private static List<ResearchProjectDef> CollectResearchPrerequisites(CellRect rect)
        {
            List<ResearchProjectDef> list = new List<ResearchProjectDef>();
            foreach (IntVec3 intVec in rect)
            {
                foreach (Thing thing in intVec.GetThingList(Find.CurrentMap))
                {
                    CollectTechFromDef(thing.def, list);
                }
                TerrainDef terrain = intVec.GetTerrain(Find.CurrentMap);
                if (terrain != null && terrain.researchPrerequisites != null)
                {
                    list.AddRange(terrain.researchPrerequisites);
                }
            }
            return list.Distinct().ToList(); // 去重
        }

        private static void CollectTechFromDef(ThingDef def, List<ResearchProjectDef> techList)
        {
            if (def == null) return;

            if (def.researchPrerequisites != null)
                techList.AddRange(def.researchPrerequisites);

            if (def.recipeMaker != null)
            {
                if (def.recipeMaker.researchPrerequisites != null)
                    techList.AddRange(def.recipeMaker.researchPrerequisites);
                if (def.recipeMaker.researchPrerequisite != null)
                    techList.Add(def.recipeMaker.researchPrerequisite);
            }
        }

        private static bool IsValidModId(string packageId, bool needDLC = false)
        {
            return needDLC ? (!string.IsNullOrEmpty(packageId) && packageId != "ludeon.rimworld") : (!string.IsNullOrEmpty(packageId) && packageId != "ludeon.rimworld" && !packageId.StartsWith("ludeon.rimworld."));
        }



    }
}

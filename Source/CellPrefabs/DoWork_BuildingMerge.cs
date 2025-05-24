using LudeonTK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace RimWorld
{
    public static class DoWork_BuildingMerge
    {
        private static readonly string RootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyBuildings");
        private static readonly string MergePath = Path.Combine(RootPath, "Merge");



        // 主调试菜单项：一键合并建筑文件
        [DebugAction("CellPrefabs预制房间", "2.MergeAllBuilding 合并建筑文件", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void MergeBuildings()
        {
            try
            {
                // 初始化合并目录
                InitializeMergeDirectory();

                // 获取所有建筑目录
                var buildingDirs = GetValidBuildingDirectories();
                if (buildingDirs.Count == 0)
                {
                    Messages.Message("未找到任何建筑目录", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                // 按类别分组建筑
                var categoryGroups = GroupBuildingsByCategory(buildingDirs);

                // 执行合并操作
                PerformMerging(categoryGroups);

                // 复制图片资源
                CopyPreviewImages(buildingDirs);

                // 新增：全局 SymbolDef 去重
                //GlobalRemoveDuplicateSymbolDefs();

                // 输出成功信息
                Messages.Message($"成功合并 {buildingDirs.Count} 个建筑（{categoryGroups.Count} 个类别）",
                    MessageTypeDefOf.SilentInput, historical: true);
            } catch (Exception ex)
            {
                // 统一错误处理
                HandleMergeException(ex);
            }
        }
        private static void InitializeMergeDirectory()
        {
            // 清空并重建Merge目录
            if (Directory.Exists(MergePath))
            {
                Directory.Delete(MergePath, true);
            }
            Directory.CreateDirectory(MergePath);
            Directory.CreateDirectory(Path.Combine(MergePath, "Textures/Prefabs_Preview"));
        }

        private static List<string> GetValidBuildingDirectories()
        {
            if (!Directory.Exists(RootPath))
            {
                Log.Error($"建筑根目录不存在: {RootPath}");
                Messages.Message($"错误：建筑根目录不存在 - {RootPath}",
                    MessageTypeDefOf.RejectInput, historical: false);
                return new List<string>();
            }

            // 获取所有子目录，并排除Merge目录
            var candidateDirs = Directory.GetDirectories(RootPath)
                .Where(dir => !Path.GetFileName(dir).Equals("Merge", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Log.Message($"找到 {candidateDirs.Count} 个候选目录");

            var validDirs = new List<string>();

            // 验证每个目录是否符合建筑文件夹结构
            foreach (var dir in candidateDirs)
            {
                var dirName = Path.GetFileName(dir);

                // 定义必要的目录结构
                var requiredDirs = new[] {
            Path.Combine(dir, "Defs"),
            Path.Combine(dir, "Patches"),
            Path.Combine(dir, "Textures", "Prefabs_Preview")
        };

                // 定义必要的文件（至少需要PrefabDefs.xml）
                var requiredFiles = new[] {
            Path.Combine(dir, "Defs", "PrefabDefs.xml")
        };

                // 定义可选文件
                var optionalFiles = new[] {
            Path.Combine(dir, "Defs", "BuildingLayouts.xml"),
            Path.Combine(dir, "Defs", "SymbolDefs.xml"),
            Path.Combine(dir, "Patches", "Patch.xml")
        };

                // 检查所有必要目录是否存在
                bool allDirsExist = requiredDirs.All(d => Directory.Exists(d));

                // 检查所有必要文件是否存在
                bool allRequiredFilesExist = requiredFiles.All(f => File.Exists(f));

                // 检查预览图片目录是否包含至少一个图片文件
                var previewDir = Path.Combine(dir, "Textures", "Prefabs_Preview");
                bool hasPreviewImage = Directory.Exists(previewDir) &&
                                      Directory.GetFiles(previewDir).Any(f =>
                                          f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                // 检查存在的可选文件
                var existingOptionalFiles = optionalFiles.Count(f => File.Exists(f));

                if (allDirsExist && allRequiredFilesExist && hasPreviewImage)
                {
                    validDirs.Add(dir);
                    Log.Message($"✓ 有效建筑目录: {dirName} (包含 {existingOptionalFiles} 个可选文件)");
                } else
                {
                    Log.Warning($"✗ 不符合结构: {dirName}");

                    // 详细记录缺失的部分
                    if (!allDirsExist)
                    {
                        var missingDirs = requiredDirs.Where(d => !Directory.Exists(d)).ToList();
                        Log.Warning($"  缺失目录: {string.Join(", ", missingDirs.Select(d => Path.GetFileName(d)))}");
                    }

                    if (!allRequiredFilesExist)
                    {
                        var missingFiles = requiredFiles.Where(f => !File.Exists(f)).ToList();
                        Log.Warning($"  缺失必要文件: {string.Join(", ", missingFiles.Select(f => Path.GetFileName(f)))}");
                    }

                    if (!hasPreviewImage)
                    {
                        Log.Warning("  缺失预览图片");
                    }
                }
            }

            if (!validDirs.Any())
            {
                Log.Message($"在 {RootPath} 下未找到任何有效建筑目录");
                Messages.Message($"错误：在 {RootPath} 下未找到任何有效建筑目录",
                    MessageTypeDefOf.RejectInput, historical: false);
            }

            return validDirs;
        }

        private static Dictionary<string, List<string>> GroupBuildingsByCategory(List<string> buildingDirs)
        {
            var groups = new Dictionary<string, List<string>>();

            foreach (var dir in buildingDirs)
            {
                var prefabPath = Path.Combine(dir, "Defs/PrefabDefs.xml");
                if (!File.Exists(prefabPath))
                {
                    Log.Warning($"跳过无效建筑目录：{dir}（缺少PrefabDefs.xml）");
                    continue;
                }

                try
                {
                    var category = GetCategoryFromPrefab(prefabPath);
                    if (string.IsNullOrEmpty(category)) continue;

                    if (!groups.ContainsKey(category)) groups[category] = new List<string>();
                    groups[category].Add(dir);
                } catch (Exception ex)
                {
                    HandleXmlParsingError(dir, ex);
                }
            }

            return groups;
        }

        private static string GetCategoryFromPrefab(string prefabPath)
        {
            var doc = XDocument.Load(prefabPath);
            var categoryElement = doc.Descendants("category").FirstOrDefault();

            if (categoryElement == null)
            {
                Messages.Message($"警告：建筑目录缺少分类标签 - {Path.GetFileName(Path.GetDirectoryName(prefabPath))}",
                    MessageTypeDefOf.RejectInput, historical: false);
                return null;
            }

            var category = categoryElement.Value.Trim();
            if (string.IsNullOrEmpty(category))
            {
                Messages.Message($"警告：建筑目录分类标签为空 - {Path.GetFileName(Path.GetDirectoryName(prefabPath))}",
                    MessageTypeDefOf.RejectInput, historical: false);
            }
            return category;
        }

        private static void PerformMerging(Dictionary<string, List<string>> groups)
        {
            foreach (var (category, buildingDirs) in groups)
            {
                // 创建类别文件夹
                var categoryDir = Path.Combine(MergePath, category);
                Directory.CreateDirectory(categoryDir);
                Directory.CreateDirectory(Path.Combine(categoryDir, "Defs"));
                Directory.CreateDirectory(Path.Combine(categoryDir, "Patches"));

                // 合并Defs文件夹下的XML（BuildingLayouts、PrefabDefs、SymbolDefs）
                MergeXmlFiles(buildingDirs, "Defs", "BuildingLayouts.xml", categoryDir, "Defs", "KCSG.StructureLayoutDef");
                MergeXmlFiles(buildingDirs, "Defs", "PrefabDefs.xml", categoryDir, "Defs", "AlphaPrefabs.PrefabDef");
                MergeXmlFiles(buildingDirs, "Defs", "SymbolDefs.xml", categoryDir, "Defs", "KCSG.SymbolDef");

                // 合并Patches文件夹下的XML（Patch.xml）
                MergeXmlFiles(buildingDirs, "Patches", "Patch.xml", categoryDir, "Patches", "Operation");
            }
        }

        private static void MergeXmlFiles(List<string> buildingDirs, string sourceFolder, string sourceFile,
      string categoryDir, string targetFolder, string elementName)
        {
            var targetPath = Path.Combine(categoryDir, targetFolder, sourceFile);
            var mergedElements = new List<XElement>();
            string rootElementName = null;

            foreach (var buildingDir in buildingDirs)
            {
                var sourcePath = Path.Combine(buildingDir, sourceFolder, sourceFile);
                if (!File.Exists(sourcePath))
                {
                    Log.Message($"跳过不存在的文件: {sourcePath}");
                    continue;
                }

                try
                {
                    var doc = XDocument.Load(sourcePath);
                    var root = doc.Root;

                    if (root == null)
                    {
                        Log.Warning($"文件 {sourcePath} 没有根元素，跳过处理");
                        continue;
                    }

                    // 确定根元素名称（仅在第一次处理时）
                    if (rootElementName == null)
                    {
                        rootElementName = root.Name.LocalName;
                        Log.Message($"使用根元素名称: {rootElementName}");
                    }

                    // 修复：仅获取直接子元素
                    var elements = !string.IsNullOrEmpty(elementName)
                        ? root.Elements(elementName).ToList() // 关键修改
                        : root.Elements().ToList();

                    if (elements.Any())
                    {
                        mergedElements.AddRange(elements);
                        Log.Message($"从 {sourcePath} 合并了 {elements.Count} 个 {elementName} 元素");
                    } else
                    {
                        Log.Message($"在 {sourcePath} 中未找到匹配的 {elementName} 元素");
                    }
                } catch (Exception ex)
                {
                    Log.Error($"合并 {sourcePath} 时出错: {ex.Message}");
                }
            }

            if (mergedElements.Any() && rootElementName != null)
            {
                // 创建新文档，使用确定的根元素名称
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement(rootElementName, mergedElements)
                );

                SaveMergedXml(doc, targetPath);
                Log.Message($"已保存合并文件: {targetPath}, 包含 {mergedElements.Count} 个元素");
            } else
            {
                Log.Message($"合并结果为空，删除可能存在的空文件: {targetPath}");
                if (File.Exists(targetPath)) File.Delete(targetPath);
            }
        }

        private static void SaveMergedXml(XDocument doc, string targetPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                doc.Save(targetPath);
            } catch (Exception ex)
            {
                Log.Error($"保存合并XML文件失败: {targetPath}, 错误: {ex.Message}");
                throw;
            }
        }

        private static void CopyPreviewImages(List<string> buildingDirs)
        {
            var targetImageDir = Path.Combine(MergePath, "Textures/Prefabs_Preview");

            // 清理并重建图片目录
            if (Directory.Exists(targetImageDir)) Directory.Delete(targetImageDir, true);
            Directory.CreateDirectory(targetImageDir);

            foreach (var buildingDir in buildingDirs)
            {
                var sourceImageDir = Path.Combine(buildingDir, "Textures/Prefabs_Preview");
                if (!Directory.Exists(sourceImageDir)) continue;

                try
                {
                    // 复制所有图片到Merge/Textures/UI/Prefabs_Preview
                    foreach (var imageFile in Directory.GetFiles(sourceImageDir))
                    {
                        var fileName = Path.GetFileName(imageFile);
                        File.Copy(imageFile, Path.Combine(targetImageDir, fileName), true);
                    }
                } catch (Exception ex)
                {
                    Log.Warning($"复制图片失败：{ex.Message}");
                }
            }
        }

        #region 错误处理辅助方法
        private static void HandleMergeException(Exception ex)
        {
            Log.Error($"合并失败：{ex.Message}\n{ex.StackTrace}");
            Messages.Message("建筑合并失败，请检查日志文件",
                MessageTypeDefOf.RejectInput, historical: false);
        }

        private static void HandleXmlParsingError(string dir, Exception ex)
        {
            Log.Warning($"解析PrefabDefs.xml失败 - {dir}: {ex.Message}");
            Messages.Message($"警告：建筑目录解析失败 - {Path.GetFileName(dir)}",
                MessageTypeDefOf.RejectInput, historical: false);
        }
        #endregion

        private static void GlobalRemoveDuplicateSymbolDefs()
        {
            // 使用四元组作为唯一键：(defName, thing, stuff, rotation)
            var uniqueSymbolDefs = new Dictionary<(string defName, string thing, string stuff, string rotation), XElement>();
            var categoryDirs = Directory.GetDirectories(MergePath);

            // 第一阶段：收集所有SymbolDefs并识别真正的重复项
            foreach (var categoryDir in categoryDirs)
            {
                var defsPath = Path.Combine(categoryDir, "Defs", "SymbolDefs.xml");

                if (!File.Exists(defsPath))
                {
                    Log.Message($"跳过不存在的文件: {defsPath}");
                    continue;
                }

                try
                {
                    var doc = XDocument.Load(defsPath);
                    var root = doc.Root;

                    if (root == null)
                    {
                        Log.Warning($"文件没有根元素，跳过处理: {defsPath}");
                        continue;
                    }

                    var symbolDefs = root.Elements("KCSG.SymbolDef").ToList();

                    foreach (var def in symbolDefs)
                    {
                        var defNameElement = def.Element("defName");
                        if (defNameElement == null)
                        {
                            Log.Warning($"发现没有defName的SymbolDef，跳过: {def.ToString().Truncate(50)}");
                            continue;
                        }

                        var defName = defNameElement.Value.Trim();
                        var thingElement = def.Element("thing");
                        if (thingElement == null)
                        {
                            Log.Warning($"发现没有thing的SymbolDef，跳过: {def.ToString().Truncate(50)}");
                            continue;
                        }

                        var thing = thingElement.Value.Trim();
                        var stuff = def.Element("stuff")?.Value.Trim() ?? "";
                        var rotation = def.Element("rotation")?.Value.Trim() ?? "";

                        // 创建四元组作为键
                        var key = (defName, thing, stuff, rotation);

                        // 如果发现重复项，记录冲突信息
                        if (uniqueSymbolDefs.ContainsKey(key))
                        {
                            Log.Message($"发现完全重复的SymbolDef - defName: {defName}, Thing: {thing}, Stuff: {stuff}, Rotation: {rotation}");
                            Log.Message($"  来源1: {GetCategoryFromPath(uniqueSymbolDefs[key].Document?.BaseUri)}");
                            Log.Message($"  来源2: {GetCategoryFromPath(def.Document?.BaseUri)}");

                            // 标记当前def为重复项（稍后移除）
                            def.AddAnnotation(new XAttribute("IsDuplicate", "true"));
                        } else
                        {
                            uniqueSymbolDefs[key] = def;
                        }
                    }
                } catch (Exception ex)
                {
                    Log.Error($"收集SymbolDefs时出错: {ex.Message}");
                }
            }

            // 第二阶段：移除所有标记为重复的项
            foreach (var categoryDir in categoryDirs)
            {
                var defsPath = Path.Combine(categoryDir, "Defs", "SymbolDefs.xml");

                if (!File.Exists(defsPath))
                {
                    continue;
                }

                try
                {
                    var doc = XDocument.Load(defsPath);
                    var root = doc.Root;

                    if (root == null)
                    {
                        continue;
                    }

                    var duplicateCount = 0;
                    var symbolDefs = root.Elements("KCSG.SymbolDef").ToList();

                    foreach (var def in symbolDefs)
                    {
                        var isDuplicate = def.Annotations<XAttribute>()
                            .FirstOrDefault(a => a.Name.LocalName == "IsDuplicate")?.Value == "true";

                        if (isDuplicate)
                        {
                            def.Remove();
                            duplicateCount++;
                        }
                    }

                    if (duplicateCount > 0)
                    {
                        Log.Message($"从 {categoryDir} 的SymbolDefs.xml中移除了 {duplicateCount} 个重复项");
                        doc.Save(defsPath);
                    }
                } catch (Exception ex)
                {
                    Log.Error($"移除重复项时出错: {ex.Message}");
                }
            }

            Log.Message($"全局SymbolDef去重完成，共处理 {uniqueSymbolDefs.Count} 个唯一定义");
        }

        // 辅助方法：从文件路径提取类别名称
        private static string GetCategoryFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "未知";

            try
            {
                var uri = new Uri(path);
                var directory = Path.GetDirectoryName(uri.LocalPath);
                return Path.GetFileName(directory);
            } catch
            {
                return "未知";
            }
        }
    }
}
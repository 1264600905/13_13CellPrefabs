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
        private static readonly string RootPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "MyBuildings");
        private static readonly string MergePath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "MyBuildings",
    "Merge"
);



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
            // 使用游戏根目录下的MyBuildings文件夹
            var rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyBuildings");

            if (!Directory.Exists(rootPath))
            {
                Messages.Message($"错误：建筑根目录不存在 - {rootPath}",
                    MessageTypeDefOf.RejectInput, historical: false);
                return new List<string>();
            }

            var directories = Directory.GetDirectories(rootPath)
                .Where(dir => Path.GetFileName(dir).StartsWith("AP_"))
                .ToList();

            if (!directories.Any())
            {
                Messages.Message($"错误：在 {rootPath} 下未找到任何建筑目录",
                    MessageTypeDefOf.RejectInput, historical: false);
            }

            return directories;
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
            var mergedElements = new List<XElement>();
            var targetPath = Path.Combine(categoryDir, targetFolder, sourceFile);

            foreach (var buildingDir in buildingDirs)
            {
                var sourcePath = Path.Combine(buildingDir, sourceFolder, sourceFile);
                if (!File.Exists(sourcePath)) continue;

                try
                {
                    var doc = XDocument.Load(sourcePath);
                    var root = doc.Root;
                    if (root != null)
                    {
                        mergedElements.AddRange(root.Elements(elementName));
                    }
                } catch (Exception ex)
                {
                    Log.Warning($"合并 {sourceFile} 时出错：{ex.Message}");
                }
            }

            if (mergedElements.Any())
            {
                SaveMergedXml(mergedElements, targetPath, elementName);
            } else
            {
                // 如果无内容且文件存在，删除空文件
                if (File.Exists(targetPath)) File.Delete(targetPath);
            }
        }

        private static void SaveMergedXml(List<XElement> elements, string targetPath, string rootElement)
        {
            var rootName = rootElement == "Operation" ? "Patch" : "Defs";
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(rootName, elements)
            );

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            doc.Save(targetPath);
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

        private static void HandleXmlMergingError(string path, Exception ex)
        {
            Log.Warning($"合并XML失败 - {path}: {ex.Message}");
            Messages.Message($"警告：XML合并失败 - {Path.GetFileName(path)}",
                MessageTypeDefOf.RejectInput, historical: false);
        }

        private static void HandleImageCopyError(string dir, Exception ex)
        {
            Log.Warning($"图片复制失败 - {dir}: {ex.Message}");
            Messages.Message($"警告：预览图片复制失败 - {Path.GetFileName(dir)}",
                MessageTypeDefOf.RejectInput, historical: false);
        }
        #endregion
    }
}
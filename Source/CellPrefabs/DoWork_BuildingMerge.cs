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
                InitializeMergeDirectory();

                var buildingDirs = GetValidBuildingDirectories();
                if (buildingDirs.Count == 0)
                {
                    Messages.Message("未找到任何建筑目录", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                // 直接合并所有建筑，不分类
                PerformGlobalMerging(buildingDirs);

                CopyPreviewImages(buildingDirs);

                Messages.Message($"成功合并 {buildingDirs.Count} 个建筑",
                    MessageTypeDefOf.SilentInput, historical: true);
            } catch (Exception ex)
            {
                HandleMergeException(ex);
            }
        }


        private static void PerformGlobalMerging(List<string> buildingDirs)
        {
            // 创建合并目录结构
            Directory.CreateDirectory(Path.Combine(MergePath, "Defs"));
            Directory.CreateDirectory(Path.Combine(MergePath, "Patches"));

            // 合并所有XML文件到全局文件
            MergeXmlFiles(buildingDirs, "Defs", "BuildingLayouts.xml", MergePath, "Defs", "KCSG.StructureLayoutDef");
            MergeXmlFiles(buildingDirs, "Defs", "PrefabDefs.xml", MergePath, "Defs", "AlphaPrefabs.PrefabDef");
            MergeXmlFiles(buildingDirs, "Defs", "SymbolDefs.xml", MergePath, "Defs", "KCSG.SymbolDef");
            MergeXmlFiles(buildingDirs, "Patches", "Patch.xml", MergePath, "Patches", "Operation");
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
            Directory.CreateDirectory(Path.Combine(MergePath, "Defs"));
            Directory.CreateDirectory(Path.Combine(MergePath, "Patches"));
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

        private static void HandleMergeException(Exception ex)
        {
            Log.Error($"合并失败：{ex.Message}\n{ex.StackTrace}");
            Messages.Message("建筑合并失败，请检查日志文件",
                MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
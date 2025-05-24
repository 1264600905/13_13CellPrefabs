using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using LudeonTK;
using Random = System.Random;

namespace CellPrefabs
{
    [StaticConstructorOnStartup]
    public class BuildingExporter : ModBase
    {
        public override string ModIdentifier => "CellPrefabsBuildingExporter";

        private static SettingHandle<string> exportFolderPath;
        private static SettingHandle<string> modsFolderPath;

        static BuildingExporter()
        {
            try
            {
                var harmony = new Harmony("com.example.cellprefabsbuildingexporter");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                exportFolderPath = null;
                modsFolderPath = null;
            } catch (Exception ex)
            {
                Log.Error($"CellPrefabsBuildingExporter初始化失败: {ex}");
            }
        }

        public override void DefsLoaded()
        {
            base.DefsLoaded();
            exportFolderPath = Settings.GetHandle(
                "exportFolderPath",
                "导出文件夹路径",
                "Merge文件夹的路径",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyBuildings", "Merge")
            );

            modsFolderPath = Settings.GetHandle(
                "modsFolderPath",
                "Mods文件夹路径",
                "游戏Mods文件夹的路径",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods")
            );
        }

        [DebugAction("CellPrefabs预制房间", "3.Create Building Prefab Mod 导出为预制房间扩展Mod", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExportBuilding()
        {
            try
            {
                if (!Directory.Exists(exportFolderPath.Value))
                {
                    Messages.Message("源文件夹不存在，请检查路径设置。", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Find.WindowStack.Add(new Dialog_CreateModSetting((name, packageId) =>
                {
                    try
                    {
                        string safeModName = Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '_'));
                        string targetFolderPath = Path.Combine(modsFolderPath.Value, safeModName);

                        // 确保目标文件夹不存在
                        if (Directory.Exists(targetFolderPath))
                        {
                            Directory.Delete(targetFolderPath, true);
                        }
                        Directory.CreateDirectory(targetFolderPath);

                        string mergePath = exportFolderPath.Value;

                        // 直接处理Merge目录下的Defs和Patches文件夹
                        string defsPath = Path.Combine(mergePath, "Defs");
                        string patchesPath = Path.Combine(mergePath, "Patches");

                        // 复制Defs下的XML文件
                        if (Directory.Exists(defsPath))
                        {
                            string targetDefs = Path.Combine(targetFolderPath, "Defs");
                            Directory.CreateDirectory(targetDefs);

                            // 直接复制所有XML文件，不使用时间戳前缀
                            foreach (string file in Directory.GetFiles(defsPath, "*.xml"))
                            {
                                string fileName = Path.GetFileName(file);
                                // 移除时间戳前缀，直接使用原文件名
                                File.Copy(file, Path.Combine(targetDefs, fileName), true);
                            }
                        }

                        // 复制Patches下的XML文件（如果有）
                        if (Directory.Exists(patchesPath) && Directory.GetFiles(patchesPath).Any())
                        {
                            string targetPatches = Path.Combine(targetFolderPath, "Patches");
                            Directory.CreateDirectory(targetPatches);

                            // 直接复制所有XML文件，不使用时间戳前缀
                            foreach (string file in Directory.GetFiles(patchesPath, "*.xml"))
                            {
                                string fileName = Path.GetFileName(file);
                                // 移除时间戳前缀，直接使用原文件名
                                File.Copy(file, Path.Combine(targetPatches, fileName), true);
                            }
                        }

                        // 处理共享纹理（Merge/Textures）
                        ProcessTextures(mergePath, targetFolderPath);

                        // 创建About文件夹
                        CreateAboutFolder(targetFolderPath, name, packageId);

                        Messages.Message($"导出完成：{targetFolderPath}", MessageTypeDefOf.PositiveEvent);
                    } catch (Exception ex)
                    {
                        Log.Error($"导出失败：{ex}");
                        Messages.Message($"导出失败：{ex.Message}，查看日志获取详情", MessageTypeDefOf.RejectInput);
                    }
                }));
            } catch (Exception ex)
            {
                Log.Error($"初始化失败：{ex}");
                Messages.Message($"初始化失败：{ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }



        private static void ProcessTextures(string mergePath, string targetFolderPath)
        {
            string texturesSource = Path.Combine(mergePath, "Textures");
            if (!Directory.Exists(texturesSource)) return;

            string targetTextures = Path.Combine(targetFolderPath, "Textures");
            Directory.CreateDirectory(targetTextures);

            // 复制所有纹理文件（保持层级）
            foreach (string file in Directory.GetFiles(texturesSource, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(texturesSource.Length + 1);
                string targetFile = Path.Combine(targetTextures, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }

            // 生成预览图（从Textures/Prefabs_Preview随机选取）
            string previewSource = Path.Combine(texturesSource, "Prefabs_Preview");
            string[] previews = Directory.GetFiles(previewSource, "*.png");
            if (previews.Length > 0)
            {
                string aboutPath = Path.Combine(targetFolderPath, "About");
                Directory.CreateDirectory(aboutPath);
                File.Copy(previews[0], Path.Combine(aboutPath, "Preview.png"), true);
            }
        }
       
        private static void ProcessFilesInFolder(string sourceDir, string destDir, string folderName, string timestamp)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                // 构建新文件名：文件夹名_时间戳_原文件名
                string newFileName = $"{folderName}_{timestamp}_{fileName}";
                string destFile = Path.Combine(destDir, newFileName);

                File.Copy(file, destFile, true);
            }

            // 递归处理子文件夹
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDestDir = Path.Combine(destDir, Path.GetFileName(subDir));
                Directory.CreateDirectory(subDestDir);
                ProcessFilesInFolder(subDir, subDestDir, folderName, timestamp);
            }
        }

        private static void CopyAndRenameFiles(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            // 处理子文件
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                // 直接使用原文件名，不添加任何前缀
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }

            // 递归处理子文件夹
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDestDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyAndRenameFiles(subDir, subDestDir);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }

        private static void CreateAboutFolder(string modFolderPath, string customName, string customPackageId)
        {
            string aboutFolderPath = Path.Combine(modFolderPath, "About");
            Directory.CreateDirectory(aboutFolderPath);

            // 创建About.xml
            string aboutXmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ModMetaData>
    <name>{customName}</name>
    <author>unknown</author>
    <url></url>
    <supportedVersions><li>1.5</li></supportedVersions>
    <packageId>{customPackageId}</packageId>
    <modDependencies>
        <li>
            <packageId>trigger.cellprefabs</packageId>
            <displayName>Cell Prefabs</displayName>
        </li>
    </modDependencies>
    <loadBefore />
    <loadAfter>
        <li>trigger.cellprefabs</li>
    </loadAfter>
    <modIconPath IgnoreIfNoMatchingField=""True"">13_13_Cell_Prefabs_Logo</modIconPath>
    <description>
        自动导出的建筑预制件
    </description>
</ModMetaData>";
            File.WriteAllText(Path.Combine(aboutFolderPath, "About.xml"), aboutXmlContent);

            // 创建Manifest.xml（保持原有逻辑或同步修改）
            string manifestXmlContent = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Manifest>
    <identifier>{customPackageId}</identifier>
    <version>1.0.0.0</version>
    <targetVersions>
        <li>1.5</li>
    </targetVersions>
    <dependencies>
        <li>sarg.cellprefabs</li>
    </dependencies>
    <incompatibleWith>
    </incompatibleWith>
    <loadBefore>
    </loadBefore>
    <loadAfter>
        <li>sarg.cellprefabs</li>
        <li>trigger.cellprefabs</li>
    </loadAfter>
    <manifestUri></manifestUri>
    <downloadUri></downloadUri>
    <supportedLanguages>
        <li>ChineseSimplified</li>
    </supportedLanguages>
</Manifest>";
            File.WriteAllText(Path.Combine(aboutFolderPath, "Manifest.xml"), manifestXmlContent);
        }
    }
}
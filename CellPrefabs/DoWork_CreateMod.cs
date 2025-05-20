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
                    Log.Error($"源文件夹不存在: {exportFolderPath.Value}");
                    Messages.Message("源文件夹不存在，请检查路径设置。", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                // 打开弹窗获取用户输入
                Find.WindowStack.Add(new Dialog_CreateModSetting((name, packageId) =>
                {
                    try
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string targetFolderName = $"CellPrefabsBuilding_{timestamp}";
                        string targetFolderPath = Path.Combine(modsFolderPath.Value, targetFolderName);
                        Directory.CreateDirectory(targetFolderPath);

                        // 获取所有AP_13_13_*格式的文件夹
                        string[] sourceFolders = Directory.GetDirectories(exportFolderPath.Value)
                           .Where(d => Path.GetFileName(d).StartsWith("AP_13_13_"))
                           .ToArray();

                        foreach (string sourceFolder in sourceFolders)
                        {
                            string folderName = Path.GetFileName(sourceFolder);
                            ProcessSourceFolder(sourceFolder, targetFolderPath, folderName, timestamp); // 使用你已有的方法
                        }

                        // 单独处理Textures文件夹（保持原有层级）
                        string texturesSource = Path.Combine(exportFolderPath.Value, "Textures");
                        if (Directory.Exists(texturesSource))
                        {
                            CopyDirectoryRecursive(texturesSource, Path.Combine(targetFolderPath, "Textures"));

                            // 随机抽取一张PNG图片并改名Preview.png放在About.xml同级目录
                            string[] textureFiles = Directory.GetFiles(texturesSource, "*.png", SearchOption.AllDirectories);
                            if (textureFiles.Length > 0)
                            {
                                Random random = new Random();
                                int randomIndex = random.Next(textureFiles.Length);
                                string selectedTexture = textureFiles[randomIndex];

                                // 确保About文件夹存在
                                string aboutFolderPath = Path.Combine(targetFolderPath, "About");
                                Directory.CreateDirectory(aboutFolderPath);

                                string previewImagePath = Path.Combine(aboutFolderPath, "Preview.png");
                                File.Copy(selectedTexture, previewImagePath, true);
                            } else
                            {
                                Log.Warning("未在Textures文件夹中找到PNG图片，无法生成预览图");
                            }
                        }

                        // 创建About文件夹并使用用户输入的名称和PackageId
                        CreateAboutFolder(targetFolderPath, name, packageId);

                        Log.Message($"建筑导出完成，位置: {targetFolderPath}");
                        Messages.Message($"建筑导出完成!\n位置: {targetFolderPath}", MessageTypeDefOf.PositiveEvent, historical: false);
                    } catch (Exception ex)
                    {
                        Log.Error($"建筑导出失败: {ex}");
                        Messages.Message($"建筑导出失败: {ex.Message}", MessageTypeDefOf.RejectInput, historical: false);
                    }
                }));
            } catch (Exception ex)
            {
                Log.Error($"建筑导出失败: {ex}");
                Messages.Message($"建筑导出失败: {ex.Message}", MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private static void ProcessSourceFolder(string sourceFolder, string targetFolderPath, string folderName, string timestamp)
        {
            // 获取源文件夹下的子文件夹（如Defs、Patches）
            string[] subFolders = Directory.GetDirectories(sourceFolder);

            foreach (string subFolder in subFolders)
            {
                string subFolderName = Path.GetFileName(subFolder);
                string targetSubFolderPath = Path.Combine(targetFolderPath, subFolderName);

                // 创建目标子文件夹（如Mod根目录下的Defs、Patches）
                Directory.CreateDirectory(targetSubFolderPath);

                // 处理子文件夹中的文件，添加文件夹名前缀
                ProcessFilesInFolder(subFolder, targetSubFolderPath, folderName, timestamp);
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

        private static void CopyAndRenameFiles(string sourceDir, string destDir, string timestamp)
        {
            Directory.CreateDirectory(destDir);

            // 处理子文件
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string newFileName = $"{Path.GetFileName(sourceDir)}_{timestamp}_{fileName}"; // 文件夹名_时间_原文件名
                string destFile = Path.Combine(destDir, newFileName);

                File.Copy(file, destFile, true);
            }

            // 递归处理子文件夹
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDestDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyAndRenameFiles(subDir, subDestDir, timestamp);
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
        <li>triger.alphaprefabs</li>
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
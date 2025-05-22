using RimWorld;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using UnityEngine;
using Verse;
using System.Linq;

namespace CellPrefabs
{
    // 存储建筑信息的类
    public class BuildingInfo
    {
        public string defName;
        public string label;
        public string shortLabel;
        public string description;
        public int priority;
        public string category;
        public string author;
        public bool exportNatural = true; // 新增：是否导出地形
        public bool exportPlant = false;    // 新增：是否导出植物
        public bool exportCreatureAndItem = true; // 新增：是否导出角色和物品
    }

    class Dialog_EnterBuildingInfo : Window
    {
        private BuildingInfo buildingInfo = new BuildingInfo();
        private Action<BuildingInfo> onCloseCallback;

        private bool translateMode = false;
        private string defName = "";
        private string label = "Type Build label";
        private string description = "Type Building description";
        private string priority = "1";
        private string category = "";
        private string author = "unknown";

        //翻译模式前的文本
        private string originalLabel = "";
        private string originalDescription = "";
        private bool wasChecked = false;

        // 新增：三个勾选框的状态
        private bool exportNatural = true;
        private bool exportPlant = true;
        private bool exportCreatureAndItem = false;

        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600, 600); // 增大窗口高度以容纳新选项

        public Dialog_EnterBuildingInfo(Action<BuildingInfo> callback)
        {
            onCloseCallback = callback;
            closeOnCancel = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30), "输入建筑信息Input Building Info");
            Text.Font = GameFont.Small;

            Rect contentRect = new Rect(inRect.x, inRect.y + 40, inRect.width, inRect.height - 100);
            Rect viewRect = new Rect(0, 0, contentRect.width - 20, 400); // 增大视图高度

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);

            float y = 0;

            // DefName
            Widgets.Label(new Rect(0, y, 150, 30), "键值名称(DefName) :");
            Rect defNameRect = new Rect(160, y, viewRect.width - 250, 30);
            defName = Widgets.TextField(defNameRect, defName);

            Rect autoButtonRect = new Rect(defNameRect.x + defNameRect.width + 10, y, 80, 30);
            if (Widgets.ButtonText(autoButtonRect, "自动生成"))
            {
                defName = GenerateAutoDefName();
            }
            y += 40;

            // Label
            Widgets.Label(new Rect(0, y, 150, 24), "标签 (Label):");
            label = Widgets.TextField(new Rect(160, y, viewRect.width - 160, 30), label);
            y += 40;

            // Description
            Widgets.Label(new Rect(0, y, 150, 24), "描述 (Description):");
            description = Widgets.TextArea(new Rect(160, y, viewRect.width - 160, 80), description);
            y += 90;

            // Priority
            Widgets.Label(new Rect(0, y, 150, 24), "优先级 (Priority):");
            priority = Widgets.TextField(new Rect(160, y, viewRect.width - 160, 30), priority);
            y += 40;

            // Category
            Widgets.Label(new Rect(0, y, 150, 30), "类别 (Category):");
            Rect categoryInputRect = new Rect(160, y, viewRect.width - 250, 30);
            category = Widgets.TextField(categoryInputRect, category);

            Rect categoryBtnRect = new Rect(categoryInputRect.x + categoryInputRect.width + 10, y, 80, 30);
            if (Widgets.ButtonText(categoryBtnRect, "选择类别"))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                options.AddRange(ValidCategories.Select(c =>
                    new FloatMenuOption(c, () => category = c)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 40;

            // 作者
            Widgets.Label(new Rect(0, y, 150, 24), "作者(Author):");
            author = Widgets.TextField(new Rect(160, y, viewRect.width - 160, 30), author);
            y += 40;

            // 新增：不导出地形勾选框
             Widgets.CheckboxLabeled(
                new Rect(0, y, viewRect.width, 30),
                "导出地形 (export terrain)",
                ref exportNatural);
            y += 30;

            // 新增：不导出植物勾选框
            Widgets.CheckboxLabeled(
                new Rect(0, y, viewRect.width, 30),
                "导出植物 (export plants)",
               ref exportPlant);
            y += 30;

            // 新增：导出角色和物品勾选框
              Widgets.CheckboxLabeled(
                new Rect(0, y, viewRect.width, 30),
                "强制导出 (force export all thing)",
               ref exportCreatureAndItem);
            y += 30;

            // 绘制翻译模式勾选框（移除onChanged参数）
            Widgets.CheckboxLabeled(
                new Rect(0, y, viewRect.width, 30),
                "翻译模式 (Translate Mode)",
                ref translateMode
            );
            y += 30;

            // 检查状态是否变化
            if (wasChecked != translateMode)
            {
                if (translateMode)
                {
                    // 勾选时保存当前值，并设置为固定文本
                    originalLabel = label;
                    originalDescription = description;
                    label = "翻译模式下不适用not use in translate mod";
                    description = "翻译模式下不适用not use in translate mod";
                } else
                {
                    // 取消勾选时恢复原值
                    label = originalLabel;
                    description = originalDescription;
                }
            }

            Widgets.EndScrollView();

            // 按钮
            if (Widgets.ButtonText(new Rect(inRect.width / 2 - 100, inRect.height - 50, 90, 30), "确定"))
            {
                if (ValidateInput())
                {
                    buildingInfo.defName = "AP_" + defName;
                    buildingInfo.label = translateMode ? $"{buildingInfo.defName}.label" : label;
                    buildingInfo.description = translateMode ? $"{buildingInfo.defName}.description" : description;
                    buildingInfo.shortLabel = buildingInfo.label;
                    buildingInfo.priority = int.Parse(priority);
                    buildingInfo.category = category;
                    buildingInfo.author = author;

                    // 保存勾选框状态
                    buildingInfo.exportNatural = !exportNatural;
                    buildingInfo.exportPlant = !exportPlant;
                    buildingInfo.exportCreatureAndItem = !exportCreatureAndItem;

                    onCloseCallback?.Invoke(buildingInfo);
                    Close();
                }
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 10, inRect.height - 50, 90, 30), "取消"))
            {
                onCloseCallback?.Invoke(null);
                Close();
            }
        }

        private bool ValidateInput()
        {
            // 放宽DefName校验：允许数字开头，包含字母、数字、下划线
            if (!string.IsNullOrEmpty(defName) && !Regex.IsMatch(defName, @"^[a-zA-Z0-9_]+$"))
            {
                Messages.Message("DefName仅允许包含字母、数字和下划线！", MessageTypeDefOf.RejectInput);
                return false;
            }
            if (string.IsNullOrEmpty(defName))
            {
                Messages.Message("DefName不能为空!", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (string.IsNullOrEmpty(label))
            {
                Messages.Message("标签不能为空!", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (string.IsNullOrEmpty(category))
            {
                Messages.Message("类别不能为空!", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (!int.TryParse(priority, out int _))
            {
                Messages.Message("优先级必须是数字!", MessageTypeDefOf.RejectInput);
                return false;
            }

            CellPrefabsUtil.ResetClickPositions();
            return true;
        }

        // 类别列表保持不变
        private static readonly List<string> ValidCategories = new List<string> {
            "AP_13_13_Dorm", "AP_13_13_Labor", "AP_13_13_Power",
            "AP_13_13_WorkShop", "AP_13_13_StoreRoom", "AP_13_13_Farm",
            "AP_13_13_Culture", "AP_13_13_Anomal", "AP_13_13_Defens",
            "AP_13_13_Atomics", "AP_13_13_Bath", "AP_13_13_Oil"
        };

        private string GenerateAutoDefName()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }
}
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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
    }

    class Dialog_EnterBuildingInfo : Window
    {
        private BuildingInfo buildingInfo = new BuildingInfo();
        private Action<BuildingInfo> onCloseCallback;

        private string defName = "";
        private string label = "Build label";
        private string description = "Building description";
        private string priority = "1";
        private string category = "";
        private string author = "unknown";

        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600, 500); // 原500x400，扩大20%

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
            Rect viewRect = new Rect(0, 0, contentRect.width - 20, 300);

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);

            float y = 0;

            // DefName
            // 在DoWindowContents方法中修改DefName输入部分
            Widgets.Label(new Rect(0, y, 150, 30), "键值名称(DefName) :");
            Rect defNameRect = new Rect(160, y, viewRect.width - 250, 30);
            defName = Widgets.TextField(defNameRect, defName);

            Rect autoButtonRect = new Rect(defNameRect.x + defNameRect.width + 10, y, 80, 30);
            if (Widgets.ButtonText(autoButtonRect, "自动生成"))
            {
                defName = GenerateAutoDefName();
            }
            y += 40; // 增大间距

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

            // 修改Category输入部分
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
            y += 40; // 增大间距

             // 作者
            Widgets.Label(new Rect(0, y, 150, 24), "作者Author:");
            author = Widgets.TextField(new Rect(160, y, viewRect.width - 160, 30), author);
            y += 40;
            Widgets.EndScrollView();

            // 按钮
            if (Widgets.ButtonText(new Rect(inRect.width / 2 - 100, inRect.height - 50, 90, 30), "确定"))
            {
                if (ValidateInput())
                {
                    buildingInfo.defName = "AP_" + defName;
                    buildingInfo.label = label;
                    buildingInfo.shortLabel = label;
                    buildingInfo.description = description;
                    buildingInfo.priority = int.Parse(priority);
                    buildingInfo.category = category;
                    buildingInfo.author = author;
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

            // 确保defName正确设置
            buildingInfo.defName = defName; // 不再添加"AP_"前缀，因为在其他地方添加

            CellPrefabsUtil.ResetClickPositions();
            return true;
        }

        // 在 BuildingExporter 类顶部添加静态变量
        private static readonly List<string> ValidCategories = new List<string> {
    "AP_13_13_Dorm", "AP_13_13_Labor", "AP_13_13_Power",
    "AP_13_13_WorkShop", "AP_13_13_StoreRoom", "AP_13_13_Farm",
    "AP_13_13_Culture", "AP_13_13_Anomal", "AP_13_13_Defens",
    "AP_13_13_Atomics", "AP_13_13_Bath", "AP_13_13_Oil"
};

        // 在Dialog_EnterBuildingInfo类中添加
        private string GenerateAutoDefName()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }
}

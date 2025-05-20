using Verse;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using RimWorld;
using KCSG;
using LudeonTK;
using System.Text.RegularExpressions;
namespace CellPrefabs
{
    public class DoWork_BuildingExporter
    {
      
        public DoWork_BuildingExporter(){
            new CellPrefabsUtil();
        }

        // 主调试菜单项：一键导出建筑
        [DebugAction("预制房间", "一键导出建筑", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExportBuilding()
        {
            // 打开对话框让用户输入建筑信息
            Find.WindowStack.Add(new Dialog_EnterBuildingInfo(OnBuildingInfoEntered));
        }

        // 建筑信息输入完成后的回调
        private static void OnBuildingInfoEntered(BuildingInfo info)
        {
            if (info == null)
            {
                Log.Message("[BuildingExporter] 用户取消了导出");
                return;
            }

            CellPrefabsUtil.buildingInfo = info;

            // 使用矩形工具选择建筑区域
            DebugToolsGeneral.GenericRectTool("选择要导出的建筑区域", CellPrefabsUtil.OnCellRectSelected);
        }

    }
    }

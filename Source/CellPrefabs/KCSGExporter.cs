using KCSG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CellPrefabs
{
    public static class KCSGExporter
    {
        /// <summary>
        /// 导出指定区域的建筑布局和符号定义
        /// </summary>
        /// <param name="map">当前地图</param>
        /// <param name="selectionCells">选中的单元格列表</param>
        /// <param name="defName">结构定义名称</param>
        /// <param name="tags">自定义标签</param>
        /// <returns>生成的XML内容</returns>
        public static string ExportBuildingLayout(
            Map map,
            List<IntVec3> selectionCells,
            string defName,
            List<string> tags = null)
        {
            // 初始化KCSG静态变量
            InitializeKCSGVariables(map, selectionCells, defName, tags);

            // 1. 生成符号定义（SymbolDef）
            List<SymbolDef> symbolDefs = ExportUtils.CreateSymbolIfNeeded(null);

            // 2. 生成结构布局（StructureLayoutDef）
            StructureLayoutDef structureDef = ExportUtils.CreateStructureDef(map, null);

            // 3. 合并符号和布局为完整XML
            return CombineToXml(symbolDefs, structureDef);
        }

        /// <summary>
        /// 初始化KCSG所需的静态变量
        /// </summary>
        private static void InitializeKCSGVariables(
            Map map,
            List<IntVec3> cells,
            string defName,
            List<string> tags)
        {
            // 清空旧数据
            Dialog_ExportWindow.cells.Clear();
            Dialog_ExportWindow.pairsCellThingList.Clear();

            // 设置选区单元格
            Dialog_ExportWindow.cells = cells;

            // 填充单元格物品映射
            Dialog_ExportWindow.pairsCellThingList = ExportUtils.FillCellThingsList(map);

            // 配置导出参数
            Dialog_ExportWindow.defName = defName;
            Dialog_ExportWindow.tags = tags != null ? new HashSet<string>(tags) : new HashSet<string>();
            Dialog_ExportWindow.spawnConduits = true; // 启用自动管道（可自定义）
            Dialog_ExportWindow.randomizeWallStuffAtGen = true; // 随机墙体材质（可自定义）
        }

        /// <summary>
        /// 合并符号和布局为XML
        /// </summary>
        private static string CombineToXml(
            List<SymbolDef> symbolDefs,
            StructureLayoutDef structureDef)
        {
            // 生成符号定义XML
            string symbolsXml = string.Join(Environment.NewLine, symbolDefs.Select(s => s.ToXMLString()));

            // 生成结构布局XML
            string layoutXml = structureDef.ToXMLString();

            // 包装在<Defs>根节点中
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Defs>
{symbolsXml}
{layoutXml}
</Defs>";
        }

        /// <summary>
        /// 从Rect选区获取单元格列表（辅助方法）
        /// </summary>
        public static List<IntVec3> GetCellsFromRect(Map map, Rect rect)
        {
            CellRect cellRect = new CellRect(
     minX: Mathf.RoundToInt(rect.xMin),  // 对应构造函数的第一个参数minX
     minZ: Mathf.RoundToInt(rect.yMin),  // 对应构造函数的第二个参数minZ
     width: Mathf.RoundToInt(rect.width),  // 对应构造函数的第三个参数width
     height: Mathf.RoundToInt(rect.height)  // 对应构造函数的第四个参数height
 );

            return cellRect.Cells
                .Where(cell => cell.InBounds(map))
                .ToList();
        }
    }

    
}

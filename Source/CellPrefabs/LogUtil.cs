using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CellPrefabs
{
     class LogUtil
    {

        //开启MOD日志
        public static bool IsDebugEnabled = true;
        public static void Message(String logStr, MessageTypeDef logType)
        {
            if(!IsDebugEnabled)
            {
                return;
            }
            Messages.Message(logStr, logType);
        }


    }
}

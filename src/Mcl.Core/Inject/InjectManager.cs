using Crane.MethodHook;
using MPayNameSpace;
using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
namespace Mcl.Core.Inject
{
    public class InjectManager
    {
        public static bool IsInitialized = false;
        private static void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                string absoluteUri = new Uri(url).AbsoluteUri;
                Process.Start(absoluteUri);
            }
        }
        public static void Init()
        {
            if (!IsInitialized)
            {
                IsInitialized = true;

                try
                {
                    Microsoft.VisualBasic.Interaction.MsgBox("打几把转服由原神团队制作，寓意着每天都能打几把宝马，本软件完全免费，获取更新请加\n QQ Group:165465824", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, "BigDick-打几巴转服");
                    OpenUrl("https://discord.gg/EvbY4YVeuq");
                    OpenUrl("https://qm.qq.com/q/ZVismgIX62");
                    var sourceMethod = typeof(CppCliUnisdkMPay).GetMethod("GetSAuthPropStr");
                    var targetMethod = typeof(InjectManager).GetMethod("NewGetSAuthPropStr");
                    MethodHookManager.Instance.AddHook(new MethodHook(sourceMethod, targetMethod));
                    MethodHookManager.Instance.StartHook();
                }
                catch (Exception e)
                {
                    Microsoft.VisualBasic.Interaction.MsgBox(e.ToString(), Microsoft.VisualBasic.MsgBoxStyle.YesNo, "BigDick转服");
                }

            }
        }
        public static string NewGetSAuthPropStr(CppCliUnisdkMPay a)
        {
            var methodHook = MethodHookManager.Instance.GetHook(MethodBase.GetCurrentMethod());
            var __result = methodHook.InvokeOriginal<string>(a);

            if (Microsoft.VisualBasic.Interaction.MsgBox("是否使用SAuth登录", Microsoft.VisualBasic.MsgBoxStyle.YesNo, "BigDick转服") == Microsoft.VisualBasic.MsgBoxResult.Yes)
            {
                __result = Microsoft.VisualBasic.Interaction.InputBox("输入Sauth",
                                           "BigDick转服",
                                           __result,
                                           -1, -1);
                JObject jobj = JObject.Parse(__result);
                if (jobj.TryGetValue("sauth_json", StringComparison.CurrentCulture, out var token))
                {
                    __result = token.Value<string>();
                }
            }

            return __result;
        }
    }
}

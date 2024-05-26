using UnityEngine;

namespace SigmaDimensionsPlugin
{
    //from CashnipLeaf: Why is this called "Debug"?? It can easily get mixed up with UnityEngine.Debug 
    //TODO: Rename to something other than "Debug"
    internal static class Debug
    {
        internal static bool debug = false;
        internal const string Tag = "[SigmaLog SD]";

        internal static void Log(string message)
        {
            if (debug)
            {
                UnityEngine.Debug.Log(Tag + ": " + message);
            }
        }

        internal static void Log(string Method, string message)
        {
            if (debug)
            {
                UnityEngine.Debug.Log(Tag + " " + Method + ": " + message);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class DebugWarning : MonoBehaviour
    {
        void Start()
        {
            if (Debug.debug)
            {
                PopupDialog.SpawnPopupDialog
                (
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    UserSettings.nodeName,
                    UserSettings.nodeName + " Warning",
                    "\n<color=#FF9231><b>Debug Spam is activated.</b>\n\n" +
                    "This feature will greatly affect performance:\n" +
                    "use it only for debugging purposes.</color>",
                    "OK",
                    true,
                    UISkinManager.GetSkin("MainMenuSkin")
                );
            }
        }
    }
}

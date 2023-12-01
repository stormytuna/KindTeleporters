using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace KindTeleporters
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class KindTeleportersBase : BaseUnityPlugin
    {
        public const string ModGUID = "stormytuna.KindTeleporters";
        public const string ModName = "KindTeleporters";
        public const string ModVersion = "1.0.1";

        public static ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModGUID);
        public static KindTeleportersBase Instance;

        private readonly Harmony harmony = new Harmony(ModGUID);

        private void Awake() {
            if (Instance is null) {
                Instance = this;
            }

            Log.LogInfo("Kind Teleporters has awoken!");

            harmony.PatchAll();
        }
    }
}

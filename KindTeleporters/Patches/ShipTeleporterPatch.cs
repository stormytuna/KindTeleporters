using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KindTeleporters.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    public class ShipTeleporterPatch
    {
        private static readonly CodeMatch[] inverseTeleporterPatchIlMatch = new CodeMatch[] {
            new CodeMatch(i => i.IsLdloc()),
            new CodeMatch(i => i.LoadsConstant(1)),
            new CodeMatch(i => i.LoadsConstant(0)),
            new CodeMatch(i => i.Calls(dropAllHeldItemsMethodInfo))
        };
        private static readonly CodeMatch[] teleporterPatchIlMatch = new CodeMatch[] {
            new CodeMatch(i => i.IsLdarg(0)),
            new CodeMatch(i => i.opcode == OpCodes.Ldfld), // Cba fetching the field info of the field loaded /shrug
            new CodeMatch(i => i.LoadsConstant(1)),
            new CodeMatch(i => i.LoadsConstant(0)),
            new CodeMatch(i => i.Calls(dropAllHeldItemsMethodInfo))
        };

        private static readonly MethodInfo dropAllHeldItemsMethodInfo = typeof(PlayerControllerB).GetMethod("DropAllHeldItems", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo dropAllButHeldItemMethodInfo = typeof(ShipTeleporterPatch).GetMethod("DropAllButHeldItem", BindingFlags.Static | BindingFlags.NonPublic);

        [HarmonyTranspiler, HarmonyPatch("TeleportPlayerOutWithInverseTeleporter")]
        public static IEnumerable<CodeInstruction> InverseTeleporterDropAllButHeldItem(IEnumerable<CodeInstruction> instructions) {
            /*
             * 	// playerControllerB.DropAllHeldItems();
             *  ldloc.0
             *  ldc.i4.1
             *  ldc.i4.0
             *  callvirt instance void GameNetcodeStuff.PlayerControllerB::DropAllHeldItems(bool, bool)
             *  
             *  becomes
             *  
             *  // ShipTeleporterPatch.DropAllButHeldItem(playerControllerB)
             *  ldloc.0
             *  callvirt void ShipTeleporterPatch::DropAllButHeldItem(PlayerControllerB)
             */

            CodeMatcher codeMatcher = new CodeMatcher(instructions);

            codeMatcher.Start();
            codeMatcher.MatchStartForward(inverseTeleporterPatchIlMatch);
            codeMatcher.Advance(1);
            codeMatcher.RemoveInstructionsWithOffsets(0, 2);
            codeMatcher.Insert(new CodeInstruction(OpCodes.Callvirt, dropAllButHeldItemMethodInfo));

            KindTeleportersBase.Log.LogInfo("Patched 'ShipTeleporterPatch.TeleportPlayerOutWithInverseTeleporter' :D");

            return codeMatcher.Instructions();
        }

        [HarmonyTranspiler, HarmonyPatch("beamUpPlayer", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> TeleporterDropAllButHeldItem(IEnumerable<CodeInstruction> instructions) {
            /*
             * 	// playerControllerB.DropAllHeldItems();
		     *  ldarg.0
		     *  ldfld class GameNetcodeStuff.PlayerControllerB ShipTeleporter/'<beamUpPlayer>d__32'::'<playerToBeamUp>5__2'
		     *  ldc.i4.1
		     *  ldc.i4.0
		     *  callvirt instance void GameNetcodeStuff.PlayerControllerB::DropAllHeldItems(bool, bool)
             *  
             *  becomes
             *  
             *  // ShipTeleporterPatch.DropAllButHeldItem(playerControllerB)
             *  ldarg.0
             *  ldfld class GameNetcodeStuff.PlayerControllerB ShipTeleporter/'<beamUpPlayer>d__32'::'<playerToBeamUp>5__2'
             *  callvirt void ShipTeleporterPatch::DropAllButHeldItem(PlayerControllerB)
             */

            CodeMatcher codeMatcher = new CodeMatcher(instructions);

            codeMatcher.End();
            codeMatcher.MatchStartBackwards(teleporterPatchIlMatch);
            codeMatcher.Advance(2);
            codeMatcher.RemoveInstructionsWithOffsets(0, 2);
            codeMatcher.Insert(new CodeInstruction(OpCodes.Callvirt, dropAllButHeldItemMethodInfo));

            KindTeleportersBase.Log.LogInfo("Patched 'ShipTeleporterPatch.beamUpPlayer' :D");

            return codeMatcher.Instructions();
        }

        private static void DropAllButHeldItem(PlayerControllerB player) {
            for (int i = 0; i < player.ItemSlots.Length; i++) {
                GrabbableObject grabbableObject = player.ItemSlots[i];
                if (i == player.currentItemSlot || grabbableObject is null) {
                    continue;
                }

                grabbableObject.parentObject = null;
                grabbableObject.heldByPlayerOnServer = false;

                if (player.isInElevator) {
                    grabbableObject.transform.SetParent(player.playersManager.elevatorTransform, worldPositionStays: true);
                } else {
                    grabbableObject.transform.SetParent(player.playersManager.propsContainer, worldPositionStays: true);
                }

                player.SetItemInElevator(player.isInHangarShipRoom, player.isInElevator, grabbableObject);
                grabbableObject.EnablePhysics(enable: true);
                grabbableObject.EnableItemMeshes(enable: true);
                grabbableObject.transform.localScale = grabbableObject.originalScale;
                grabbableObject.isHeld = false;
                grabbableObject.isPocketed = false;
                grabbableObject.startFallingPosition = grabbableObject.transform.parent.InverseTransformPoint(grabbableObject.transform.position);
                grabbableObject.FallToGround(randomizePosition: true);
                grabbableObject.fallTime = Random.Range(-0.3f, 0.05f);

                if (player.IsOwner) {
                    grabbableObject.DiscardItemOnClient();
                } else if (!grabbableObject.itemProperties.syncDiscardFunction) {
                    grabbableObject.playerHeldBy = null;
                }

                if (player.IsOwner) {
                    HUDManager.Instance.holdingTwoHandedItem.enabled = false;
                    HUDManager.Instance.itemSlotIcons[i].enabled = false;
                    HUDManager.Instance.ClearControlTips();
                    player.activatingItem = false;
                }

                player.ItemSlots[i] = null;
            }

            var heldItem = player.ItemSlots[player.currentItemSlot];
            player.twoHanded = heldItem?.itemProperties.twoHanded ?? false;
            player.carryWeight = Mathf.Clamp(1f - (heldItem?.itemProperties.weight ?? 1f - 1f), 0f, 10f);
            player.currentlyHeldObjectServer = heldItem;
        }
    }
}

using System.Reflection;
using FezGame.Components;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

/*
 * While the patches for most items in the game are handled in CollectTreasurePatch, owls are handled separately. This
 * patch uses an ILHook to check if we're in an archipelago and if so, skip over the line which increments the number of
 * collected owls.
 */
namespace FEZAP.Archipelago
{
    public class CollectOwlPatch(Game game) : GameComponent(game)
    {
        private ILHook GameNpcStateTryStopTalkingHook;

        public override void Initialize()
        {
            base.Initialize();

            // Patch GameNpcState.TryStopTalking to not increment the CollectedOwls count
            GameNpcStateTryStopTalkingHook = new ILHook(typeof(GameNpcState).GetMethod("TryStopTalking", BindingFlags.NonPublic | BindingFlags.Instance), CreateGameNpcStateTryStopTalkingHook);
        }

        private void CreateGameNpcStateTryStopTalkingHook(ILContext il)
        {
            ILCursor cursor = new(il);
            ILLabel skipLabel = il.DefineLabel();

            // If we're in an archipelago, skip over GameState.SaveData.CollectedOwls++;
            cursor.GotoNext(MoveType.After, i => i.MatchCall("FezEngine.Components.NpcState", "UpdateAction"));
            cursor.EmitDelegate(ArchipelagoManager.IsConnected);
            cursor.Emit(OpCodes.Brtrue, skipLabel);
            cursor.GotoNext(MoveType.After, i => i.MatchStfld("FezGame.Structure.SaveData", "CollectedOwls"));
            cursor.MarkLabel(skipLabel);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            GameNpcStateTryStopTalkingHook.Dispose();
        }
    }
}

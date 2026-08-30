using System.Reflection;
using FezEngine.Services.Scripting;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using FezGame.Structure;
using MonoMod.RuntimeDetour;

/*
 * Prevents Gomez from being able to carry items if the Carry ability has not been unlocked in the Archipelago.
 */
namespace FEZAP.Archipelago
{
    public class CarryPatch : IFezapPatch
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IDotService DotService { private get; set; }

        private Hook LiftAllowedHook;

        public void Init()
        {
            Type LiftAction = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.Lift");
            LiftAllowedHook = new Hook(LiftAction.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance), LiftAllowedHooked);
        }

        private void LiftAllowedHooked(Action<object> original, object self)
        {
            if (ItemManager.ReceivedAbilityData.Carry || !ArchipelagoManager.IsConnected())
            {
                original(self);
                return;
            }

            PlayerManager.Action = ActionType.Idle;
            PlayerManager.CarriedInstance = null;
            PlayerManager.PushedInstance = null;

            string LiftMsg = "You can't carry objects yet";
            DotService.Say($"@{LiftMsg}", true, true);
        }

        public void Dispose()
        {
            LiftAllowedHook.Dispose();
        }
    }
}

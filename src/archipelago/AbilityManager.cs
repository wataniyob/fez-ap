using FEZAP.Archipelago;
using FezEngine.Services.Scripting;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using FezGame.Structure;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace FezAP.src.archipelago
{
    public class AbilityManager
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IDotService DotService { private get; set; }

        private Hook LiftAllowedHook;

        private Hook TurnPivotAllowedHook;

        private Hook ValvesBoltsAllowedHook;

        private Hook GrabTombstoneAllowedHook;

        public void Init()
        {
            Type LiftAction = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.Lift");
            LiftAllowedHook = new Hook(LiftAction.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance), LiftAllowedHooked);

            Type PivotsHost = typeof(Fez).Assembly.GetType("FezGame.Components.PivotsHost");
            Type PivotState = PivotsHost.GetNestedType("PivotState", BindingFlags.NonPublic);
            TurnPivotAllowedHook = new Hook(PivotState.GetMethod("Spin", BindingFlags.Public | BindingFlags.Instance), TurnObjectsAllowedHooked);

            Type ValvesBoltsHost = typeof(Fez).Assembly.GetType("FezGame.Components.ValvesBoltsTimeswitchesHost");
            Type ValveState = ValvesBoltsHost.GetNestedType("ValveState", BindingFlags.NonPublic);
            ValvesBoltsAllowedHook = new Hook(ValveState.GetMethod("GrabOnto", BindingFlags.Public | BindingFlags.Instance), TurnObjectsAllowedHooked);

            Type PivotTombstoneAction = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.PivotTombstone");
            GrabTombstoneAllowedHook = new Hook(PivotTombstoneAction.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance), TurnObjectsAllowedHooked);
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

        private void TurnObjectsAllowedHooked(Action<object> original, object self)
        {
            if (ItemManager.ReceivedAbilityData.TurnObjects || !ArchipelagoManager.IsConnected())
            {
                original(self);
                return;
            }

            PlayerManager.Action = ActionType.Idle;

            string PivotMsg = "You can't turn objects yet";
            DotService.Say($"@{PivotMsg}", true, true);
        }
    }
}

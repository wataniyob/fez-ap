using FEZAP.Archipelago;
using FezEngine.Components.Scripting;
using FezEngine.Services.Scripting;
using FezEngine.Tools;
using FezGame;
using FezGame.Components;
using FezGame.Components.Actions;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FezAP.src.archipelago
{
    public class AbilityManager
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IDotService DotService { private get; set; }

        private Hook LiftAllowedHook;

        private Hook PushPivotAllowedHook;

        private Hook ValvesBoltsAllowedHook;

        private Hook GrabTombstoneAllowedHook;

        public void Init()
        {
            Type LiftAction = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.Lift");
            LiftAllowedHook = new Hook(LiftAction.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance), LiftAllowedHooked);

            Type PivotsHost = typeof(Fez).Assembly.GetType("FezGame.Components.PivotsHost");
            Type PivotState = PivotsHost.GetNestedType("PivotState", BindingFlags.NonPublic);
            PushPivotAllowedHook = new Hook(PivotState.GetMethod("Spin", BindingFlags.Public | BindingFlags.Instance), PushPivotAllowedHooked);

            Type ValvesBoltsHost = typeof(Fez).Assembly.GetType("FezGame.Components.ValvesBoltsTimeswitchesHost");
            Type ValveState = ValvesBoltsHost.GetNestedType("ValveState", BindingFlags.NonPublic);
            ValvesBoltsAllowedHook = new Hook(ValveState.GetMethod("GrabOnto", BindingFlags.Public | BindingFlags.Instance), PushPivotAllowedHooked);

            Type PivotTombstoneAction = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.PivotTombstone");
            GrabTombstoneAllowedHook = new Hook(PivotTombstoneAction.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Instance), PushPivotAllowedHooked);
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

            string LiftMsg = "You can't carry things yet";
            DotService.Say($"@{LiftMsg}", true, true);
        }

        private void PushPivotAllowedHooked(Action<object> original, object self)
        {
            if (ItemManager.ReceivedAbilityData.TurnObjects || !ArchipelagoManager.IsConnected())
            {
                original(self);
                return;
            }

            PlayerManager.Action = ActionType.Idle;

            string PivotMsg = "You can't turn pivot objects yet";
            DotService.Say($"@{PivotMsg}", true, true);
        }
    }
}

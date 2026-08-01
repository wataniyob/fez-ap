using System.Reflection;
using FezEngine.Services.Scripting;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

/*
 * This patch prevents collected items from being added to your inventory while connected to an AP. While most items are
 * handled by OpenTreasure.Act, bits and owls are handled separately so they have their own patches.
 *
 * OpenTreasure.Act is an incredibly long method which handles way too many things. It handles multi-stage animations
 * both for picking up items and opening chests, doing camera changes, playing sounds, AND it's responsible for all side
 * effects of picking up those items, such as marking the trile/etc as collected or inactive, incrementing the counters/
 * adding items to the list in the save file, etc. Modding this method is a total PITA since if we use a Hook, we have
 * to reimplement the _entire method_ including all of the animation stuff just to change the parts we don't want.
 *
 * Luckily, almost all of what we want to change is inside a big switch statement. We can use an ILHook to manipulate
 * the IL of the method which lets us change just what we care about. We first go to the start of the switch statement
 * and insert a call to our own delegate method, where we reimplement just a part of the method (in C#!!). Next we
 * insert a branch instruction which branches to a label. Finally, we go to after the switch statement (just after the
 * OnHudElementChanged call which we also want to skip) and mark that position as our label. That means that after
 * executing our delegate method, it will then skip over all the stuff we don't want to execute. I think this is the
 * only way to mod this method that doesn't completely suck.
 */
namespace FEZAP.Archipelago
{
    public class CollectTreasurePatch(Game game) : GameComponent(game)
    {
        private FieldInfo OpenTreasureTreasureActorType;
        private FieldInfo OpenTreasureTreasureInstance;
        private ILHook OpenTreasureActHook;

        [ServiceDependency]
        public IGameStateManager GameState { get; set; }

        [ServiceDependency]
        public IGomezService GomezService { get; set; }

        [ServiceDependency]
        public IDotService DotService { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            // Manipulate the IL for OpenTreasure to reduce side effects
            Type OpenTreasure = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.OpenTreasure");
            OpenTreasureTreasureActorType = OpenTreasure.GetField("treasureActorType", BindingFlags.NonPublic | BindingFlags.Instance);
            OpenTreasureTreasureInstance = OpenTreasure.GetField("treasureInstance", BindingFlags.NonPublic | BindingFlags.Instance);
            OpenTreasureActHook = new ILHook(OpenTreasure.GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance), CreateOpenTreasureActSwitchHook);
        }

        private void CreateOpenTreasureActSwitchHook(ILContext il)
        {
            ILCursor cursor = new(il);
            ILLabel dontSkipLabel = il.DefineLabel();
            ILLabel skipLabel = il.DefineLabel();

            cursor.GotoNext(MoveType.After, i => i.MatchCallvirt("FezGame.Services.IPlayerManager", "set_Action")); // Move cursor to right after base.PlayerManager.Action = ActionType.Idle;
            cursor.EmitDelegate(ArchipelagoManager.IsConnected); // Are we in an Archipelago?
            cursor.Emit(OpCodes.Brfalse, dontSkipLabel); // Branch to the original method code if not
            cursor.Emit(OpCodes.Ldarg_0); // Push `this` onto the stack, to be the "self" argument in our hook
            cursor.EmitDelegate(OpenTreasureActSwitchHooked); // Call our hooked method
            cursor.Emit(OpCodes.Br, skipLabel); // Branch to after the code we want to skip
            cursor.MarkLabel(dontSkipLabel); // Put label at original code position

            cursor.GotoNext(MoveType.After, i => i.MatchCallvirt("FezGame.Services.IGameStateManager", "OnHudElementChanged")); // Move cursor to right after base.GameState.OnHudElementChanged();
            cursor.MarkLabel(skipLabel); // Put label for where we want to skip to
        }

        private void OpenTreasureActSwitchHooked(object self)
        {
            ActorType treasureActorType = (ActorType)OpenTreasureTreasureActorType.GetValue(self);
            TrileInstance treasureInstance = (TrileInstance)OpenTreasureTreasureInstance.GetValue(self);

            switch (treasureActorType)
            {
                case ActorType.SecretCube:
                    treasureInstance.Collected = true;
                    if (treasureInstance.GlobalSpawn)
                    {
                        GomezService.OnCollectedGlobalAnti();
                    }
                    else
                    {
                        GomezService.OnCollectedAnti();
                    }
                    SpeedRun.AddCube(anti: true);
                    break;
                case ActorType.CubeShard:
                    GomezService.OnCollectedShard();
                    SpeedRun.AddCube(anti: false);
                    break;
                case ActorType.PieceOfHeart:
                    GomezService.OnCollectedPieceOfHeart();
                    DotService.Say("DOT_HEART_A", nearGomez: true, hideAfter: false).Ended = delegate
                    {
                        DotService.Say("DOT_HEART_B", nearGomez: true, hideAfter: false).Ended = delegate
                        {
                            DotService.Say("DOT_HEART_C", nearGomez: true, hideAfter: true);
                        };
                    };
                    break;
                case ActorType.SkeletonKey:
                    if (!GameState.SaveData.OneTimeTutorials.ContainsKey("DOT_KEY_A"))
                    {
                        GameState.SaveData.OneTimeTutorials.Add("DOT_KEY_A", value: true);
                        DotService.Say("DOT_KEY_A", nearGomez: true, hideAfter: false).Ended = delegate
                        {
                            DotService.Say("DOT_KEY_B", nearGomez: true, hideAfter: true);
                        };
                    }
                    break;
                case ActorType.NumberCube:
                    DotService.Say("DOT_ANCIENT_ARTIFACT", nearGomez: true, hideAfter: false).Ended = delegate
                    {
                        DotService.Say("DOT_NUMBERS_A", nearGomez: true, hideAfter: true);
                    };
                    break;
                case ActorType.TriSkull:
                    DotService.Say("DOT_ANCIENT_ARTIFACT", nearGomez: true, hideAfter: false).Ended = delegate
                    {
                        DotService.Say("DOT_TRISKULL_A", nearGomez: true, hideAfter: false).Ended = delegate
                        {
                            DotService.Say("DOT_TRISKULL_B", nearGomez: true, hideAfter: false).Ended = delegate
                            {
                                DotService.Say("DOT_TRISKULL_C", nearGomez: true, hideAfter: false).Ended = delegate
                                {
                                    DotService.Say("DOT_TRISKULL_D", nearGomez: true, hideAfter: true);
                                };
                            };
                        };
                    };
                    break;
                case ActorType.LetterCube:
                    DotService.Say("DOT_ANCIENT_ARTIFACT", nearGomez: true, hideAfter: false).Ended = delegate
                    {
                        DotService.Say("DOT_ALPHABET_A", nearGomez: true, hideAfter: true);
                    };
                    break;
                case ActorType.Tome:
                    DotService.Say("DOT_ANCIENT_ARTIFACT", nearGomez: true, hideAfter: false).Ended = delegate
                    {
                        DotService.Say("DOT_TOME_A", nearGomez: true, hideAfter: false).Ended = delegate
                        {
                            DotService.Say("DOT_TOME_B", nearGomez: true, hideAfter: true);
                        };
                    };
                    break;
                case ActorType.TreasureMap:
                    if (!GameState.SaveData.OneTimeTutorials.ContainsKey("DOT_TREASURE_MAP")) // This "OneTimeTutorial" doesn't exist but it will stand in for our first map collection
                    {
                        GameState.SaveData.OneTimeTutorials.Add("DOT_TREASURE_MAP", value: true);
                        DotService.Say("DOT_TREASURE_MAP_A", nearGomez: true, hideAfter: false).Ended = delegate
                        {
                            DotService.Say("DOT_TREASURE_MAP_B", nearGomez: true, hideAfter: false).Ended = delegate
                            {
                                DotService.Say("DOT_TREASURE_MAP_C", nearGomez: true, hideAfter: false).Ended = delegate
                                {
                                    DotService.Say("DOT_TREASURE_MAP_D", nearGomez: true, hideAfter: true);
                                };
                            };
                        };
                    }
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            OpenTreasureActHook.Dispose();
        }
    }
}

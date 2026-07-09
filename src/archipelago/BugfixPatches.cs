using System.Collections;
using System.Reflection;
using FezEngine;
using FezEngine.Services.Scripting;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame;
using FezGame.Components;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace FEZAP.Archipelago
{
    public class BugfixPatches(Game game) : GameComponent(game)
    {
        private Hook BitUpdateHook;
        private Hook BitTryInitializeHook;

        private MethodInfo BitShineOnYouCrazyDiamondsMethod;
        private FieldInfo BitCollectSoundsField;
        private FieldInfo BitTrackedBitsField;
        private FieldInfo BitTrackedCollectsField;
        private FieldInfo BitSolidCubesField;
        private int bitCollectedCount = 0;

        private ConstructorInfo SwooshingCubeConstructor;
        private FieldInfo SwooshingCubeSplineField;
        private MethodInfo SwooshingCubeDisposeMethod;

        private FieldInfo OpenTreasureTreasureActorType;
        private FieldInfo OpenTreasureTreasureInstance;
        private ILHook OpenTreasureActHook;

        private Hook FinalRebuildHostTryInitializeHook;
        private Hook FinalRebuildHostUpdateHook;
        private int finalCubeShards;
        private int finalSecretCubes;

        private Hook DotServiceSayDelegateHook;

        [ServiceDependency]
        public IGameStateManager GameState { get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { get; set; }

        [ServiceDependency]
        public IPlayerManager PlayerManager { get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { get; set; }

        [ServiceDependency]
        public IGomezService GomezService { get; set; }

        [ServiceDependency]
        public IDotService DotService { get; set; }

        [ServiceDependency]
        public IDotManager Dot { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            // Hook the SplitUpCubeHost.Update method to not add a bit to the save file or attempt to spawn a cube
            Type BitHost = typeof(Fez).Assembly.GetType("FezGame.Components.SplitUpCubeHost");
            BitShineOnYouCrazyDiamondsMethod = BitHost.GetMethod("ShineOnYouCrazyDiamonds", BindingFlags.NonPublic | BindingFlags.Instance);
            BitCollectSoundsField = BitHost.GetField("CollectSounds", BindingFlags.NonPublic | BindingFlags.Instance);
            BitTrackedBitsField = BitHost.GetField("TrackedBits", BindingFlags.NonPublic | BindingFlags.Instance);
            BitTrackedCollectsField = BitHost.GetField("TrackedCollects", BindingFlags.NonPublic | BindingFlags.Instance);
            BitUpdateHook = new Hook(BitHost.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), BitUpdateHooked);
            // Also hook TryInitialize to not add in-progress bits and try to assemble a cube
            BitTryInitializeHook = new Hook(BitHost.GetMethod("TryInitialize", BindingFlags.NonPublic | BindingFlags.Instance), BitTryInitializeHooked);
            BitSolidCubesField = BitHost.GetField("SolidCubes", BindingFlags.NonPublic | BindingFlags.Instance);
            Type SwooshingCube = BitHost.GetNestedType("SwooshingCube", BindingFlags.NonPublic);
            SwooshingCubeConstructor = SwooshingCube.GetConstructor([typeof(TrileInstance), typeof(Mesh), typeof(Vector3), typeof(Quaternion)]);
            SwooshingCubeSplineField = SwooshingCube.GetField("Spline", BindingFlags.Public | BindingFlags.Instance);
            SwooshingCubeDisposeMethod = SwooshingCube.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);

            // Manipulate the IL for OpenTreasure to reduce side effects
            Type OpenTreasure = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.OpenTreasure");
            OpenTreasureTreasureActorType = OpenTreasure.GetField("treasureActorType", BindingFlags.NonPublic | BindingFlags.Instance);
            OpenTreasureTreasureInstance = OpenTreasure.GetField("treasureInstance", BindingFlags.NonPublic | BindingFlags.Instance);
            OpenTreasureActHook = new ILHook(OpenTreasure.GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance), CreateOpenTreasureActSwitchHook);

            // Patch FinalRebuildHost to use cube count before the archipelago goal was marked as achieved
            Type FinalRebuildHost = typeof(Fez).Assembly.GetType("FezGame.Components.FinalRebuildHost");
            FinalRebuildHostTryInitializeHook = new Hook(FinalRebuildHost.GetMethod("TryInitialize", BindingFlags.NonPublic | BindingFlags.Instance), FinalRebuildHostTryInitializeHooked);
            FinalRebuildHostUpdateHook = new Hook(FinalRebuildHost.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FinalRebuildHostUpdateHooked);

            // Patch DotHost.Say delegate to prevent getting interrupted by Emotional Support
            Type DotService = typeof(Fez).Assembly.GetType("FezGame.Services.Scripting.DotService");
            Type DotServiceSayDelegateType = DotService.GetNestedTypes(BindingFlags.NonPublic).First(type => type.Name.Contains("<Say>")); // Is this stable across platforms?
            MethodInfo DotServiceSayDelegate = DotServiceSayDelegateType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).First(m => m.Name.Contains("m__0")); // Is this stable across platforms?
            DotServiceSayDelegateHook = new Hook(DotServiceSayDelegate, DotServiceSayDelegateHooked);
        }

        private void BitUpdateHooked(Action<object, GameTime> original, object self, GameTime gameTime)
        {
            if (!ArchipelagoManager.IsConnected())
            {
                original(self, gameTime);
                return;
            }

            // Reimplement a simplified version of the method which just checks for bit collections and does some animations
            if (GameState.Loading || GameState.TimePaused || !CameraManager.Viewpoint.IsOrthographic())
                return;
            BitShineOnYouCrazyDiamondsMethod.Invoke(self, [(float)gameTime.ElapsedGameTime.TotalSeconds]);
            IList TrackedCollects = (IList)BitTrackedCollectsField.GetValue(self);
            if (PlayerManager.Action != ActionType.GateWarp && PlayerManager.Action != ActionType.LesserWarp && !PlayerManager.Action.IsSwimming())
            {
                List<TrileInstance> TrackedBits = (List<TrileInstance>)BitTrackedBitsField.GetValue(self);
                TrileInstance collect = PlayerManager.AxisCollision[VerticalDirection.Up].Surface ?? PlayerManager.AxisCollision[VerticalDirection.Down].Surface;
                if (collect != null && collect.Trile.ActorSettings.Type == ActorType.GoldenCube && TrackedBits.Contains(collect) && !LevelManager.Triles.Values.Any(x => x.Overlaps && x.OverlappedTriles.Contains(collect) && x.Position == collect.Position))
                {
                    SoundEffect[] CollectSounds = (SoundEffect[])BitCollectSoundsField.GetValue(self);
                    CollectSounds[bitCollectedCount].Emit();
                    bitCollectedCount++;
                    if (bitCollectedCount >= 8)
                        bitCollectedCount = 0;
                    Mesh SolidCubes = (Mesh)BitSolidCubesField.GetValue(self);
                    object swooshingCube = SwooshingCubeConstructor.Invoke([collect, SolidCubes, new Vector3(0, 0, 0), SolidCubes.Rotation]);
                    TrackedCollects.Add(swooshingCube);
                    GameState.SaveData.ThisLevel.DestroyedTriles.Add(collect.OriginalEmplacement);
                    GameState.SaveData.ThisLevel.FilledConditions.SplitUpCount++;
                    LevelManager.ClearTrile(collect);
                    TrackedBits.Remove(collect);
                }
            }
            for (int i = TrackedCollects.Count - 1; i >= 0; i--)
            {
                object swooshingCube = TrackedCollects[i];
                Vector3SplineInterpolation spline = (Vector3SplineInterpolation)SwooshingCubeSplineField.GetValue(swooshingCube);
                if (spline.Reached)
                {
                    TrackedCollects.RemoveAt(i);
                    // Do we need to dispose it? The game doesn't...
                }
            }
            // The rest of this method isn't needed
        }

        private void BitTryInitializeHooked(Action<object> original, object self)
        {
            if (ArchipelagoManager.IsConnected())
            {
                // Clear this early
                IList TrackedCollects = (IList)BitTrackedCollectsField.GetValue(self);
                foreach (object swooshingCube in TrackedCollects)
                {
                    SwooshingCubeDisposeMethod.Invoke(swooshingCube, null);
                }
                TrackedCollects.Clear();
            }
            original(self);
        }

        private void CreateOpenTreasureActSwitchHook(ILContext il)
        {
            /*
             * OpenTreasure.Act is an incredibly long method which handles way too many things. It handles multi-stage
             * animations both for picking up items and opening chests, doing camera changes, playing sounds, AND it's
             * responsible for all side effects of picking up those items, such as marking the trile/etc as collected
             * or inactive, incrementing the counters/adding items to the list in the save file, etc. Modding this
             * method is a total PITA since if we use a Hook, we have to reimplement the _entire method_ including all
             * of the animation stuff just to change the parts we don't want.
             *
             * Luckily, almost all of what we want to change is inside a big switch statement. We can use an ILHook to
             * manipulate the IL of the method which lets us change just what we care about. We first go to the start of
             * the switch statement and insert a call to our own delegate method, where we reimplement just a part of
             * the method (in C#!!). Next we insert a branch instruction which branches to a label. Finally, we go to
             * after the switch statement (just after the OnHudElementChanged call which we also want to skip) and mark
             * that position as our label. That means that after executing our delegate method, it will then skip over
             * all the stuff we don't want to execute. I think this is the only way to mod this method that doesn't
             * completely suck.
             */

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

        private void FinalRebuildHostTryInitializeHooked(Action<object> original, object self)
        {
            if (!ArchipelagoManager.IsConnected() || (LevelManager.Name != "HEX_REBUILD"))
            {
                original(self);
                return;
            }
            finalCubeShards = GameState.SaveData.CubeShards;
            finalSecretCubes = GameState.SaveData.SecretCubes;
            original(self);
            Fezap.locationManager.MonitorGoal();
        }

        private void FinalRebuildHostUpdateHooked(Action<object, GameTime> original, object self, GameTime gameTime)
        {
            if (!ArchipelagoManager.IsConnected() || (LevelManager.Name != "HEX_REBUILD"))
            {
                original(self, gameTime);
                return;
            }
            int origCubeShards = GameState.SaveData.CubeShards;
            int origSecretCubes = GameState.SaveData.SecretCubes;
            GameState.SaveData.CubeShards = finalCubeShards;
            GameState.SaveData.SecretCubes = finalSecretCubes;
            original(self, gameTime);
            GameState.SaveData.CubeShards = origCubeShards;
            GameState.SaveData.SecretCubes = origSecretCubes;
        }

        private bool DotServiceSayDelegateHooked(Func<object, float, float, bool> original, object self, float f1, float f2)
        {
            if (Dot.Behaviour == DotHost.BehaviourType.SpiralAroundWithCamera)
                return false;

            return original(self, f1, f2);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            BitUpdateHook.Dispose();
            BitTryInitializeHook.Dispose();
            OpenTreasureActHook.Dispose();
            FinalRebuildHostTryInitializeHook.Dispose();
            FinalRebuildHostUpdateHook.Dispose();
            DotServiceSayDelegateHook.Dispose();
        }
    }
}

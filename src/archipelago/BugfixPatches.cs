using System.Reflection;
using FezEngine;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoMod.RuntimeDetour;

namespace FEZAP.Archipelago
{
    public class BugfixPatches : GameComponent
    {
        private Type BitHost;
        private Hook BitUpdateHook;

        private MethodInfo BitShineOnYouCrazyDiamondsMethod;
        private FieldInfo BitCollectSoundsField;
        private FieldInfo BitTrackedBitsField;
        private int bitCollectedCount = 0;

        public BugfixPatches(Game game) : base(game) {}

        [ServiceDependency]
        public IGameStateManager GameState { get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { get; set; }

        [ServiceDependency]
        public IPlayerManager PlayerManager { get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            // Hook the SplitUpCubeHost.Update method to not add a bit to the save file or attempt to spawn a cube
            BitHost = typeof(Fez).Assembly.GetType("FezGame.Components.SplitUpCubeHost");
            BitShineOnYouCrazyDiamondsMethod = BitHost.GetMethod("ShineOnYouCrazyDiamonds", BindingFlags.NonPublic | BindingFlags.Instance);
            BitCollectSoundsField = BitHost.GetField("CollectSounds", BindingFlags.NonPublic | BindingFlags.Instance);
            BitTrackedBitsField = BitHost.GetField("TrackedBits", BindingFlags.NonPublic | BindingFlags.Instance);
            BitUpdateHook = new Hook(BitHost.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), BitUpdateHooked);
        }

        private void BitUpdateHooked(Action<object, GameTime> original, object self, GameTime gameTime)
        {
            if (!ArchipelagoManager.IsConnected())
            {
                original(self, gameTime);
                return;
            }

            // Reimplement a much simplified version of the method which just does animations and checks for bit collections
            if (GameState.Loading || GameState.TimePaused || !CameraManager.Viewpoint.IsOrthographic())
                return;
            BitShineOnYouCrazyDiamondsMethod.Invoke(self, new object[] { (float)gameTime.ElapsedGameTime.TotalSeconds });
            if (PlayerManager.Action != ActionType.GateWarp && PlayerManager.Action != ActionType.LesserWarp && !PlayerManager.Action.IsSwimming())
            {
                List<TrileInstance> TrackedBits = (List<TrileInstance>)BitTrackedBitsField.GetValue(self);
                TrileInstance collect = PlayerManager.AxisCollision[VerticalDirection.Up].Surface ?? PlayerManager.AxisCollision[VerticalDirection.Down].Surface;
                if (collect != null && collect.Trile.ActorSettings.Type == ActorType.GoldenCube && TrackedBits.Contains(collect) && !LevelManager.Triles.Values.Any((TrileInstance x) => x.Overlaps && x.OverlappedTriles.Contains(collect) && x.Position == collect.Position))
                {
                    SoundEffect[] CollectSounds = (SoundEffect[])BitCollectSoundsField.GetValue(self);
                    CollectSounds[bitCollectedCount].Emit();
                    bitCollectedCount++;
                    if (bitCollectedCount >= 8)
                        bitCollectedCount = 0;
                    GameState.SaveData.ThisLevel.DestroyedTriles.Add(collect.OriginalEmplacement);
                    GameState.SaveData.ThisLevel.FilledConditions.SplitUpCount++;
                    LevelManager.ClearTrile(collect);
                    TrackedBits.Remove(collect);
                }
            }
            // The rest of this method isn't needed
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            BitUpdateHook.Dispose();
        }
    }
}

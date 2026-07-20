using System.Collections;
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

/*
 * While the patches for most items in the game are handled in CollectTreasurePatch, bits are handled separately. This
 * patch prevents collected bits from increasing the count of bits in the inventory, as well as not showing the
 * in-progress cube being assembled. If we're not connected to an AP, all the original code runs instead.
 */
namespace FEZAP.Archipelago
{
    public class CollectBitPatch(Game game) : GameComponent(game)
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            BitUpdateHook.Dispose();
            BitTryInitializeHook.Dispose();
        }
    }
}

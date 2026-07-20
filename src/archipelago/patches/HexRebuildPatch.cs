using System.Reflection;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;

/*
 * Entering HEX_REBUILD with enough cubes will complete your goal in the archipelago. If the archipelago server is
 * configured to auto collect items on goal completion, that means you will end up with all 64 cubes collected,
 * regardless of how many cubes were actually earned.
 *
 * This patch tracks how many cubes were earned at the moment the goal is completed and ensures that the cutscene plays
 * out displaying the correct number of cubes, as well as sending the player to the correct version of the gomez_house
 * ending. This patch also is the place which calls MonitorGoal, so we have full control over when the end goal is
 * checked so we can track our collected cubes properly.
 */
namespace FEZAP.Archipelago
{
    public class HexRebuildPatch(Game game) : GameComponent(game)
    {
        private Hook FinalRebuildHostTryInitializeHook;
        private Hook FinalRebuildHostUpdateHook;
        private int finalCubeShards;
        private int finalSecretCubes;

        [ServiceDependency]
        public IGameStateManager GameState { get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            // Patch FinalRebuildHost to use cube count before the archipelago goal was marked as achieved
            Type FinalRebuildHost = typeof(Fez).Assembly.GetType("FezGame.Components.FinalRebuildHost");
            FinalRebuildHostTryInitializeHook = new Hook(FinalRebuildHost.GetMethod("TryInitialize", BindingFlags.NonPublic | BindingFlags.Instance), FinalRebuildHostTryInitializeHooked);
            FinalRebuildHostUpdateHook = new Hook(FinalRebuildHost.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FinalRebuildHostUpdateHooked);
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            FinalRebuildHostTryInitializeHook.Dispose();
            FinalRebuildHostUpdateHook.Dispose();
        }
    }
}

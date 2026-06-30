using Archipelago.MultiClient.Net.Models;
using FezEngine.Services;
using FezEngine.Services.Scripting;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Services;
using FEZUG.Features;
using FEZUG.Features.Console;

namespace FEZAP.Archipelago
{
    internal enum ItemSound
    {
        Progression,
        NonProgression,
        Trap,
        Filler,
    }

    public class ItemManager
    {
        [ServiceDependency]
        public IGameStateManager GameState { get; set; }

        [ServiceDependency]
        public ICameraService CameraService { private get; set; }

        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public ILevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameService GameService { private get; set; }

        [ServiceDependency]
        public IDotService DotService { private get; set; }

        private static readonly List<string> EmotionalSupportMsgs = [
            " wants you to know you got this",
            " believes in you",
            " is cheering you on",
            " is rooting for you"
        ];

        private void ClearCollectibleSaveData()
        {
            GameState.SaveData.Artifacts = [];
            GameState.SaveData.CollectedOwls = 0;
            GameState.SaveData.CollectedParts = 0;
            GameState.SaveData.CubeShards = 0;
            GameState.SaveData.Keys = 0;
            GameState.SaveData.Maps = [];
            GameState.SaveData.PiecesOfHeart = 0;
            GameState.SaveData.SecretCubes = 0;
        }

        public void RestoreReceivedItems()
        {
            ClearCollectibleSaveData();

            List<ItemInfo> itemsReceived = [.. ArchipelagoManager.session.Items.AllItemsReceived];
            foreach (ItemInfo item in itemsReceived)
            {
                if (!(item.ItemName.Contains("Trap") || (item.ItemName == "Emotional Support")))
                {
                    HandleReceivedItem(item);
                }
            }

            LocationManager.receivedCollectibleData = new(
                GameState.SaveData.Artifacts,
                GameState.SaveData.CollectedOwls,
                GameState.SaveData.CollectedParts,
                GameState.SaveData.CubeShards,
                GameState.SaveData.Keys,
                GameState.SaveData.Maps,
                GameState.SaveData.PiecesOfHeart,
                GameState.SaveData.SecretCubes
            );
        }

        public void HandleReceivedItem(ItemInfo item)
        {
            switch (item.ItemName)
            {
                case "Golden Cube":
                    GameState.SaveData.CubeShards += 1;
                    break;
                case "Anti-Cube":
                    GameState.SaveData.SecretCubes += 1;
                    break;
                case "Cube Bit":
                    GameState.SaveData.CollectedParts += 1;
                    if (GameState.SaveData.CollectedParts == 8)
                    {
                        GameState.SaveData.CollectedParts = 0;
                        GameState.SaveData.CubeShards += 1;
                    }
                    break;
                case "Owl":
                    GameState.SaveData.CollectedOwls += 1;
                    break;
                case "Heart Cube":
                    GameState.SaveData.PiecesOfHeart += 1;
                    break;
                case "Arch Map":
                    if (!GameState.SaveData.Maps.Contains("MAP_ARCH"))
                        GameState.SaveData.Maps.Add("MAP_ARCH");
                    break;
                case "Crypt Map A":
                    if (!GameState.SaveData.Maps.Contains("MAP_CRYPT_A"))
                        GameState.SaveData.Maps.Add("MAP_CRYPT_A");
                    break;
                case "Crypt Map B":
                    if (!GameState.SaveData.Maps.Contains("MAP_CRYPT_B"))
                        GameState.SaveData.Maps.Add("MAP_CRYPT_B");
                    break;
                case "Crypt Map C":
                    if (!GameState.SaveData.Maps.Contains("MAP_CRYPT_C"))
                        GameState.SaveData.Maps.Add("MAP_CRYPT_C");
                    break;
                case "Crypt Map D":
                    if (!GameState.SaveData.Maps.Contains("MAP_CRYPT_D"))
                        GameState.SaveData.Maps.Add("MAP_CRYPT_D");
                    break;
                case "QR Code Map":
                    if (!GameState.SaveData.Maps.Contains("MAP_MYSTERY"))
                        GameState.SaveData.Maps.Add("MAP_MYSTERY");
                    break;
                case "Pivot Map":
                    if (!GameState.SaveData.Maps.Contains("MAP_PIVOT"))
                        GameState.SaveData.Maps.Add("MAP_PIVOT");
                    break;
                case "Ritual Map":
                    if (!GameState.SaveData.Maps.Contains("MAP_RITUAL"))
                        GameState.SaveData.Maps.Add("MAP_RITUAL");
                    break;
                case "Tree Sky Map":
                    if (!GameState.SaveData.Maps.Contains("MAP_TREE_SKY"))
                        GameState.SaveData.Maps.Add("MAP_TREE_SKY");
                    break;
                case "The Writing Cube":
                    if (!GameState.SaveData.Artifacts.Contains(ActorType.LetterCube))
                        GameState.SaveData.Artifacts.Add(ActorType.LetterCube);
                    break;
                case "The Counting Cube":
                    if (!GameState.SaveData.Artifacts.Contains(ActorType.NumberCube))
                        GameState.SaveData.Artifacts.Add(ActorType.NumberCube);
                    break;
                case "The Tome Artifact":
                    if (!GameState.SaveData.Artifacts.Contains(ActorType.Tome))
                        GameState.SaveData.Artifacts.Add(ActorType.Tome);
                    break;
                case "The Skull Artifact":
                    if (!GameState.SaveData.Artifacts.Contains(ActorType.TriSkull))
                        GameState.SaveData.Artifacts.Add(ActorType.TriSkull);
                    break;
                case "Sunglasses":
                    GameState.SaveData.HasFPView = true;
                    break;
                case "Boileroom Door Unlocked":
                    DoorManager.BoileroomUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Lighthouse Door Unlocked":
                    DoorManager.LighthouseUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Tree Door Unlocked":
                    DoorManager.TreeUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Well Door Unlocked":
                    DoorManager.WellUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Windmill Door Unlocked":
                    DoorManager.WindmillUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Mausoleum Door Unlocked":
                    DoorManager.MausoleumUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Sewer Hub Door Unlocked":
                    DoorManager.SewerHubUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Sewer Pillars Door Unlocked":
                    DoorManager.SewerPillarsUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Arch Door Unlocked":
                    DoorManager.ArchUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Bell Tower Door Unlocked":
                    DoorManager.BellTowerUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Cabin Door Unlocked":
                    DoorManager.CabinUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Throne Door Unlocked":
                    DoorManager.ThroneUnlocked = true;
                    Fezap.doorManager.HandleDoors();
                    break;
                case "Rotation Trap":
                    DoRotationTrap();
                    break;
                case "Reload Trap":
                    DoReloadTrap();
                    break;
                case "Gravity Trap":
                    DoGravityTrap();
                    break;
                case "Emotional Support":
                    DoEmotionalSupport(item);
                    break;
                default:
                    FezugConsole.Print($"Unknown item: {item.ItemDisplayName}", FezugConsole.OutputType.Error);
                    break;
            }
        }

        private void DoRotationTrap()
        {
            if (LevelManager.Flat)
                return;
            List<int> rotationOptions = [-2, -1, 1, 2];
            int index = RandomHelper.Random.Next(rotationOptions.Count);
            CameraService.Rotate(rotationOptions[index]);
        }

        private void DoReloadTrap()
        {
            #if DEBUG
            // TODO: Some triles are weirdly absent until an input is given
            WarpLevel.Warp(LevelManager.Name);
            #endif  // DEBUG
        }

        private void DoGravityTrap()
        {
            #if DEBUG
            int gravityTrapDuration = 15;

            // Increase the gravity
            // TODO: Fix gravity trap causing doors to get stuck until level reload
            GameService.SetGravity(false, 4);

            // Add delayed effect
            // TODO: Extend the timer rather than creating a new one if one exists already
            TimeSpan targetTime = Fezap.GameTime.TotalGameTime + new TimeSpan(0, 0, gravityTrapDuration);
            DelayedAction delayedAction = new(targetTime, () => { GameService.SetGravity(false, 1); });
            Fezap.delayedActions.Add(delayedAction);

            // Add countdown for when the gravity trap will end
            for (int i = 1; i <= gravityTrapDuration; i++)
            {
                TimeSpan timeOfMsg = Fezap.GameTime.TotalGameTime + new TimeSpan(0, 0, gravityTrapDuration - i);
                string msg = $"{i}";
                DelayedAction msgAction = new(timeOfMsg, () => { FezugConsole.Print(msg); });
                Fezap.delayedActions.Add(msgAction);
            }
            #endif  // DEBUG
        }

        private void DoEmotionalSupport(ItemInfo item)
        {
            string msg = item.Player.Name + RandomHelper.InList(EmotionalSupportMsgs);
            _ = DotService.Say($"@{msg}", true, true);
        }
    }
}

using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Structure;
using Microsoft.Xna.Framework;

namespace FEZAP.Archipelago
{
    public class DoorManager
    {
        [ServiceDependency]
        public ILevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public ILevelMaterializer LevelMaterializer { private get; set; }

        public static bool BoileroomUnlocked = false;
        public static bool LighthouseUnlocked = false;
        public static bool TreeUnlocked = false;
        public static bool WellUnlocked = false;
        public static bool WindmillUnlocked = false;
        public static bool MausoleumUnlocked = false;
        public static bool SewerHubUnlocked = false;
        public static bool SewerPillarsUnlocked = false;
        public static bool ArchUnlocked = false;
        public static bool BellTowerUnlocked = false;
        public static bool CabinUnlocked = false;
        public static bool ThroneUnlocked = false;

        public void ResetDoors()
        {
            BoileroomUnlocked = false;
            LighthouseUnlocked = false;
            TreeUnlocked = false;
            WellUnlocked = false;
            WindmillUnlocked = false;
            MausoleumUnlocked = false;
            SewerHubUnlocked = false;
            SewerPillarsUnlocked = false;
            ArchUnlocked = false;
            BellTowerUnlocked = false;
            CabinUnlocked = false;
            ThroneUnlocked = false;
        }

        public void HandleDoors()
        {
            TrileEmplacement pos;

            switch (LevelManager.Name)
            {
                case "VILLAGEVILLE_3D":
                    pos = new(35, 30, 36);
                    if (BoileroomUnlocked)
                        SwapToDoor(pos, "VILLAGE DOORS 1", "VILLAGE DOORS 0", 2);
                    break;
                case "LIGHTHOUSE":
                    pos = new(21, 20, 27);
                    if (LighthouseUnlocked)
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 0);
                    break;
                case "RAILS":
                    pos = new(14, 21, 14);
                    if (WellUnlocked)
                        SwapToDoor(pos, "INDUST DOOR A 1", "INDUST DOOR A 0");
                    break;
                case "PIVOT_ONE":
                    pos = new(26, 61, 30);
                    if (WindmillUnlocked)
                        SwapToDoor(pos, "INDUST DOOR A 1", "INDUST DOOR A 0");
                    break;
                case "MAUSOLEUM":
                    pos = new(21, 13, 23);
                    if (MausoleumUnlocked)
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 2);
                    break;
                case "SEWER_HUB":
                    pos = new(10, 42, 9);
                    if (SewerHubUnlocked)
                        SwapToDoor(pos, "SEWER DOOR 2 B", "SEWER DOOR 2 A");
                    break;
                case "SEWER_PILLARS":
                    pos = new(8, 14, 30);
                    if (SewerPillarsUnlocked)
                        SwapToDoor(pos, "SEWER DOOR 1", "SEWER DOOR 0");
                    break;
                case "TREE":
                    pos = new(41, 50, 2);
                    if (TreeUnlocked)
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 0);
                    pos = new(24, 59, 20);
                    if (!CabinUnlocked)
                        SwapToLockedDoor(pos, 1);
                    else
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 3);
                    break;
                case "NATURE_HUB":
                    pos = new(16, 18, 15);
                    if (!ArchUnlocked)
                        SwapToLockedDoor(pos, 1);
                    else
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 3);
                    pos = new(0, 14, 27);
                    if (!BellTowerUnlocked)
                        SwapToLockedDoor(pos, 3);
                    else
                        SwapToDoor(pos, "WOODEN DOOR B", "WOODEN DOOR A", 1);
                    break;
                case "TREE_SKY":
                    pos = new(11, 51, 9);
                    if (!ThroneUnlocked)
                        SwapToLockedDoor(pos, 1);
                    else
                        SwapToDoor(pos, "ZU DOOR B", "ZU DOOR A", 3);
                    break;
            }
        }

        private void SwapToDoor(TrileEmplacement pos, string bottomTrile, string topTrile, byte rotate = 255)
        {
            TrileInstance instance = LevelManager.TrileInstanceAt(ref pos);
            if (instance != null)
            {
                if (rotate < 4)
                    instance.SetPhiLight(rotate);
                Trile trile = LevelManager.TrileSet.Triles.First(trile => trile.Value.Name == bottomTrile).Value;
                LevelManager.SwapTrile(instance, trile);
            }
            pos += Vector3.UnitY;
            instance = LevelManager.TrileInstanceAt(ref pos);
            if (instance != null)
            {
                if (rotate < 4)
                    instance.SetPhiLight(rotate);
                Trile trile = LevelManager.TrileSet.Triles.First(trile => trile.Value.Name == topTrile).Value;
                LevelManager.SwapTrile(instance, trile);
            }
        }

        private void SwapToLockedDoor(TrileEmplacement pos, byte rotate = 255)
        {
            LoadLockedDoorTriles();
            SwapToDoor(pos, "Locked Door B", "Locked Door", rotate);
        }

        private void LoadLockedDoorTriles()
        {
            // NATURE_HUB and TREE_SKY don't have any locked doors meaning the locked door graphics aren't available in
            // the LevelMaterializer. This forces them to be loaded if they aren't already
            try
            {
                Trile trile = LevelManager.TrileSet.Triles.First(trile => trile.Value.Name == "Locked Door").Value;
                LevelMaterializer.RebuildTrile(trile);
                trile = LevelManager.TrileSet.Triles.First(trile => trile.Value.Name == "Locked Door B").Value;
                LevelMaterializer.RebuildTrile(trile);
            }
            catch (ArgumentException)
            {
                // These are already loaded
            }
        }
    }
}

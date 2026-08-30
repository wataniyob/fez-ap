using FezEngine.Tools;
using System.Reflection;

namespace FEZAP.Archipelago
{
    public interface IFezapPatch
    {
        public void Init();
        public void Dispose();
    }

    public class PatchManager
    {
        private List<IFezapPatch> Patches = [];

        public void Init()
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => t.IsClass && typeof(IFezapPatch).IsAssignableFrom(t)))
            {
                IFezapPatch patch = (IFezapPatch)Activator.CreateInstance(type);
                ServiceHelper.InjectServices(patch);
                patch.Init();
                Patches.Add(patch);
            }
        }

        public void Dispose()
        {
            foreach (IFezapPatch patch in Patches)
            {
                patch.Dispose();
            }
            Patches = [];
        }
    }
}

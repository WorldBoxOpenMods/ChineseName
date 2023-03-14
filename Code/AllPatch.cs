using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
namespace Chinese_Name
{
    internal class AllPatch
    {
        private const string patch_id = "inmny.Chinese_Name";
        public static void patch_all()
        {
            Harmony.CreateAndPatchAll(typeof(AllPatch), patch_id);
        }
        public static void unpatch_all()
        {
            Harmony.UnpatchID(patch_id);
        }
    }
}

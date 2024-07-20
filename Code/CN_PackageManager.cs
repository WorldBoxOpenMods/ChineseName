using Chinese_Name.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    internal class CN_PackageManager
    {
        public static List<CN_PackageMeta> packages_to_load = new();
        private static HashSet<CN_PackageMeta> packages_to_delete = new();
        public static bool IsDirty { get; private set; } = true;
        public static void LiftUp(CN_PackageMeta package)
        {
            int idx = packages_to_load.IndexOf(package);
            packages_to_load.Swap(idx, idx - 1);
            IsDirty = true;
        }
        public static void LiftDown(CN_PackageMeta package)
        {
            int idx = packages_to_load.IndexOf(package);
            packages_to_load.Swap(idx, idx + 1);
            IsDirty = true;
        }
        public static void AddPackageToLoad(CN_PackageMeta package)
        {
            packages_to_load.Add(package);
            if (!string.IsNullOrEmpty(package.meta_url))
            {
                packages_to_delete.Remove(package);
            }
            IsDirty = true;
        }
        public static void RemovePackageToLoad(CN_PackageMeta package)
        {
            packages_to_load.Remove(package);
            if (!string.IsNullOrEmpty(package.meta_url))
            {
                packages_to_delete.Add(package);
            }
            IsDirty = true;
        }
        public static void FindAllPackagesToLoad()
        {
            packages_to_load.Clear();

            var top_folder = GeneralUtils.CombinePath(ModClass.Instance.GetDeclaration().FolderPath, "Packages");

            var package_folders = Directory.GetDirectories(top_folder);

            var package_order = new List<string>();
            var order_setting_path = GeneralUtils.CombinePath(top_folder, "order.json");
            if(File.Exists(order_setting_path))
            {
                package_order = GeneralUtils.DeserializeFromJson<List<string>>(File.ReadAllText(order_setting_path));
            }

            foreach (var package_folder_name in package_order)
            {
                for(int i=0; i< package_folders.Length; i++)
                {
                    var package_path = package_folders[i];
                    if (Path.GetFileNameWithoutExtension(package_path) == package_folder_name && File.Exists(Path.Combine(package_path, "meta.json")))
                    {
                        packages_to_load.Add(new(package_path));
                        package_folders[i] = null;
                        break;
                    }
                }
            }
            foreach (var package_path in package_folders)
            {
                if (string.IsNullOrEmpty(package_path))
                {
                    continue;
                }
                if (File.Exists(Path.Combine(package_path, "meta.json")))
                    packages_to_load.Add(new(package_path));
            }
        }
        public static void SavePackageLoadOrder()
        {

        }
        public static void LoadAllPackages()
        {
            foreach(var package in packages_to_load)
            {
                LoadLocalPackage(package.local_path);
            }
            IsDirty = false;
        }
        public static void LoadLocalPackage(string folder_path)
        {
            CN_NameGeneratorLibrary.Instance.LoadNonRepeatFolder(folder_path, x=>!x.EndsWith("meta.json"));
            WordLibraryManager.Instance.MergeWithFolder(folder_path);
        }
        public static void UnloadAllPackages()
        {
            CN_NameGeneratorLibrary.Instance.UnLoadAll();
            WordLibraryManager.Instance.UnloadAll();

            foreach (var package in packages_to_delete)
            {
                package.RemoveFromLocal();
            }
            packages_to_delete.Clear();
        }
        public static bool IsEarliestLoadedPackage(CN_PackageMeta meta)
        {
            return packages_to_load.First() == meta;
        }
        public static bool IsLatestLoadedPackage(CN_PackageMeta meta)
        {
            return packages_to_load.Last() == meta;
        }
    }
}

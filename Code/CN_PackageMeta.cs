using Chinese_Name.ui;
using Chinese_Name.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name
{
    public class CN_PackageMeta
    {
        public string name;
        public string author;
        public string version;
        public string description;
        [NonSerialized]
        public string meta_url;
        [NonSerialized]
        public string local_path;

        internal PackageUtils.MetaUrlInfo meta_url_info { get
            {
                if (__meta_url_info == null)
                {
                    __meta_url_info = PackageUtils.MetaUrlInfo.Deserialize(meta_url);
                }
                return __meta_url_info;
            } }
        private PackageUtils.MetaUrlInfo __meta_url_info;
        internal string UID => meta_url_info.GetUID() ?? Path.GetFileNameWithoutExtension(local_path);
        public CN_PackageMeta(string git_or_path)
        {
            if (string.IsNullOrEmpty(git_or_path)) // Json deserialize
            {
                return;
            }
            if (PackageUtils.IsLocalPath(git_or_path))
            {
                local_path = git_or_path;
                var read_meta = GeneralUtils.DeserializeFromJson<CN_PackageMeta>(File.ReadAllText(GeneralUtils.CombinePath(local_path, "meta.json")));
                BaseInfoFromAnother(read_meta);
                meta_url = PackageUtils.GetGitUrlFromLocalPackage(local_path);
            }
            else
            {
                meta_url = git_or_path;
            }
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj) && obj is CN_PackageMeta meta && meta.UID == UID;
        }
        public override int GetHashCode()
        {
            return UID.GetHashCode();
        }
        internal void BaseInfoFromAnother(CN_PackageMeta another)
        {
            name = another.name;
            author = another.author;
            version = another.version;
            description = another.description;
        }
        internal async Task DownloadAsync(IDoubleProgress<DownloadUtils.MultiDownloadProgress, DownloadUtils.SingleDownloadProgress> progress = null)
        {
            if (string.IsNullOrEmpty(meta_url))
            {
                return;
            }
            if (string.IsNullOrEmpty(local_path))
            {
                local_path = PackageUtils.GenLocalPathFromMetaUrl(meta_url);
                if (Directory.Exists(local_path))
                {
                    Directory.Delete(local_path, true);
                }
                Directory.CreateDirectory(local_path);
            }

            var meta_url_info = PackageUtils.MetaUrlInfo.Deserialize(meta_url);
            string folder_path = GeneralUtils.GetDirectoryName(meta_url_info.path);

            if (progress is PackageDownloadProgress package_download_progress)
            {
                package_download_progress.Report("Finding package files to download...");
            }
            var download_items = await PackageUtils.GetFolderDownloadItems(meta_url_info.owner, meta_url_info.repo, folder_path, meta_url_info.git_type, local_path);

            var total_size = (ulong)download_items.Sum(item => item.size);
            var multi_progress = new DownloadUtils.MultiDownloadProgress()
            {
                CurrentFileIndex = 0,
                TotalFiles = download_items.Count,
                TotalBytesToReceive = total_size
            };
            progress?.Report(multi_progress);
            foreach (var item in download_items)
            {
                await DownloadUtils.DownloadFileAsync(item.download_url, item.save_path, progress);

                multi_progress.CurrentFileIndex++;
                multi_progress.BytesReceived += (ulong)item.size;
                progress?.Report(multi_progress);
            }
            File.WriteAllText(GeneralUtils.CombinePath(local_path, "meta_url"), meta_url);
        }

        internal void RemoveFromLocal()
        {
            if (string.IsNullOrEmpty(local_path))
            {
                return;
            }
            try
            {
                Directory.Delete(local_path, true);
            }
            catch(Exception e)
            {
                ModClass.LogError(e.Message);
                ModClass.LogError(e.StackTrace);
            }
        }
    }
}

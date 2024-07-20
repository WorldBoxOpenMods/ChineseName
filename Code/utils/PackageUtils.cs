using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name.utils
{
    internal static class PackageUtils
    {
        public enum GitType
        {
            Gitee,
            Github
        }
        public static string GetAPI_RepoContentsUnderPath(GitType git_type)
        {
            switch (git_type)
            {
                case GitType.Gitee:
                    return "https://gitee.com/api/v5/repos/{0}/{1}/contents/{2}";
                case GitType.Github:
                    return "https://api.github.com/repos/{0}/{1}/contents/{2}";
            }
            throw new NotImplementedException();
        }
        public class MetaUrlInfo
        {
            private string _owner;
            private string _repo;
            private string _path;
            private GitType _git_type;
            public string owner { get=>_owner; set { _owner = value; UID_dirty = true; } }
            public string repo { get => _repo; set { _repo = value; UID_dirty = true; } }
            public string path { get => _path; set { _path = value; UID_dirty = true; } }
            public GitType git_type { get => _git_type; set { _git_type = value; UID_dirty = true; } }

            private string UID;
            private bool UID_dirty = true;
            public static MetaUrlInfo Deserialize(string url)
            {
                var info = new MetaUrlInfo();
                if (!string.IsNullOrEmpty(url))
                {
                    var uri = new Uri(url);
                    var path = uri.AbsolutePath; // /inmny/wb-cn-library/raw/master/PackagesStored/default/meta.json
                    var path_parts = path.Split('/'); // ["", "inmny", "wb-cn-library", "raw", "master", ...]

                    info.owner = path_parts[1];
                    info.repo = path_parts[2];
                    info.path = string.Join("/", path_parts.Skip(5));
                    if (uri.Host == "gitee.com")
                        info.git_type = GitType.Gitee;
                    else if (uri.Host == "github.com")
                        info.git_type = GitType.Github;
                }
                else
                {
                    info.UID_dirty = false;
                    info.UID = null;
                }

                return info;
            }
            public string GetUID()
            {
                if (UID_dirty)
                {
                    var sb = new StringBuilder();
                    sb.Append($"{owner}_{repo}");

                    foreach (var part in GeneralUtils.GetDirectoryName(path).Split('/'))
                    {
                        sb.Append($"_{part}");
                    }
                    UID = sb.ToString();
                }
                return UID;
            }
        }
        struct ContentItem
        {
            public string name;
            public long? size;
            public string download_url;
            public string type;
        }
        public struct DownloadItem
        {
            public string download_url;
            public string save_path;
            public long size;
        }
        public static async Task<List<DownloadItem>> GetFolderDownloadItems(string owner, string repo, string folder_path, GitType git_type, string prefix = "")
        {
            var api_url = GetAPI_RepoContentsUnderPath(git_type);
            api_url = string.Format(api_url, owner, repo, folder_path);

            string raw_contents = await DownloadUtils.GetHttpContent(api_url);
            // ModClass.LogInfo(raw_contents);
            var contents = GeneralUtils.DeserializeFromJson<List<ContentItem>>(raw_contents);

            var result = new List<DownloadItem>();
            foreach(var c in contents)
            {
                if (c.type == "dir")
                {
                    result.AddRange(await GetFolderDownloadItems(owner, repo, GeneralUtils.CombinePath(folder_path, c.name), git_type, GeneralUtils.CombinePath(prefix, c.name)));
                }
                else if (c.type == "file")
                {
                    result.Add(new DownloadItem()
                    {
                        download_url = c.download_url,
                        save_path = GeneralUtils.CombinePath(prefix, c.name),
                        size = c.size ?? 128
                    });
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            return result;
        } 
        public static bool IsLocalPath(string pPath)
        {
            return !pPath.StartsWith("http");
        }
        public static string GetGitUrlFromLocalPackage(string pLocalPath)
        {
            string meta_url_file = GeneralUtils.CombinePath(pLocalPath, "meta_url");
            if (File.Exists(meta_url_file))
            {
                return File.ReadAllText(meta_url_file);
            }
            return "";
        }

        public static async void ExtractMetaFromGitUrl(this CN_PackageMeta meta_to_save)
        {
            var meta_url = GeneralUtils.CombinePath(meta_to_save.meta_url);
            string raw_meta = await DownloadUtils.GetHttpContent(meta_url);
            var read_meta = GeneralUtils.DeserializeFromJson<CN_PackageMeta>(raw_meta);
            meta_to_save.BaseInfoFromAnother(read_meta);
        }
        public static void ExtractMetaFromLocalFile(this CN_PackageMeta meta_to_save, string meta_file_path)
        {
            var read_meta = GeneralUtils.DeserializeFromJson<CN_PackageMeta>(File.ReadAllText(meta_file_path));
            meta_to_save.BaseInfoFromAnother(read_meta);
        }
        public static string GenLocalPathFromMetaUrl(string meta_url)
        {
            return GeneralUtils.CombinePath(ModClass.Instance.GetDeclaration().FolderPath, "Packages", MetaUrlInfo.Deserialize(meta_url).GetUID());
        }
    }
}

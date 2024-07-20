using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name.utils
{
    internal class DownloadUtils
    {
        public static async Task<string> GetHttpContent(string url)
        {
            var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        public struct MultiDownloadProgress
        {
            public int TotalFiles { get; set; }
            public int CurrentFileIndex { get; set; }
            public ulong BytesReceived { get; set; }
            public ulong? TotalBytesToReceive { get; set; }
        }
        /// <summary>
        /// From https://www.cnblogs.com/h82258652/p/10950580.html
        /// </summary>
        public struct SingleDownloadProgress
        {
            public ulong BytesReceived { get; set; }
            public ulong? TotalBytesToReceive { get; set; }
        }
        private const int BufferSize = 8192;
        /// <summary>
        /// From https://www.cnblogs.com/h82258652/p/10950580.html
        /// </summary>
        internal static async Task DownloadFileAsync(string url, string file_path, IProgress<SingleDownloadProgress> progress = null)
        {
            var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch(HttpRequestException e)
            {
                ModClass.LogError($"Error when downloading from {url}");
                ModClass.LogError(e.Message);
                ModClass.LogError(e.StackTrace);
                return;
            }

            var content = response.Content;
            if (content == null)
            {
                throw new Exception("No content in response");
            }

            var headers = content.Headers;
            var content_length = headers.ContentLength;
            using var response_stream = await content.ReadAsStreamAsync().ConfigureAwait(false);

            string dir = GeneralUtils.GetDirectoryName(file_path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using var file_stream = new FileStream(file_path, FileMode.Create);

            var buffer = new byte[BufferSize];
            int bytesRead;
            var bytes = new List<byte>();

            var download_progress = new SingleDownloadProgress();
            if (headers.ContentLength.HasValue)
            {
                download_progress.TotalBytesToReceive = (ulong)content_length.Value;
            }
            progress?.Report(download_progress);

            while ((bytesRead = await response_stream.ReadAsync(buffer, 0, BufferSize).ConfigureAwait(false)) > 0)
            {
                await file_stream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                download_progress.BytesReceived += (ulong)bytesRead;
                progress?.Report(download_progress);
            }
        }
    }
}

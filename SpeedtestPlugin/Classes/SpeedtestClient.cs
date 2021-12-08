namespace Loupedeck.SpeedtestPlugin.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    public class SpeedtestClient
    {
        private static Int32 DownloadSize => 25 * 1000 * 1000;
        private static Int32 UploadSize => 5;

        private static Char[] Chars => new[]
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
            'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
            'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };

        private JToken _serverProperties;

        public SpeedtestClient() => this.GetNearestServers().GetAwaiter().GetResult();

        private async Task GetNearestServers()
        {
            var urlForServers = "https://www.speedtest.net/api/js/servers";
            var paramsForServers = "?engine=js";

            try
            {
                var client = new HttpClient();
                client.BaseAddress = new Uri(urlForServers);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage responseMessage = await client.GetAsync(paramsForServers);
                if (responseMessage.IsSuccessStatusCode)
                {
                    var responseString = await responseMessage.Content.ReadAsStringAsync();
                    var jsonServers = JArray.Parse(responseString);

                    this._serverProperties = jsonServers[0];
                }
            }
            catch (Exception)
            {
                this._serverProperties = null;
            }
        }

        public Int64 PingServer()
        {
            Ping ping = new Ping();
            PingReply reply = null;

            try
            {
                Uri hostUri = new Uri(this._serverProperties["url"]?.ToString() ?? String.Empty);
                IPAddress ip = Dns.GetHostEntry(hostUri.Host).AddressList[0];
                reply = ping.Send(ip);
            }
            catch (Exception)
            {
                // ignored
            }

            return reply?.RoundtripTime ?? -1;
        }

        public Double TestDownloadSpeed(Int32 simultaneousDownloads = 2, Int32 retryCount = 2)
        {
            var testData = this.GenerateDownloadUrls();

            return TestSpeed(testData, async (client, url) =>
            {
                var data = await client.GetByteArrayAsync(url);
                return DownloadSize;
            }, simultaneousDownloads);
        }

        public Double TestUploadSpeed(Int32 simultaneousUploads = 2, Int32 retryCount = 2)
        {
            var testData = GenerateUploadData(retryCount);
            return TestSpeed(testData, async (client, uploadData) =>
            {
                var uploadUrl = $"https://{this._serverProperties["host"]}/upload?nocache={Guid.NewGuid().ToString()}";

                await client.PostAsync(uploadUrl, new StringContent(uploadData));
                return uploadData.Length;
            }, simultaneousUploads);
        }


        private static Double TestSpeed<T>(IEnumerable<T> testData, Func<HttpClient, T, Task<Double>> doWork,
            Int32 concurrencyCount = 2)
        {
            var throttler = new SemaphoreSlim(concurrencyCount);
            var size = new List<Double>();

            Task<Double>[] downloadTasks = testData.Select(async data =>
            {
                await throttler.WaitAsync();
                var client = new SpeedTestHttpClient();
                try
                {
                    var timer = new Stopwatch();
                    timer.Start();
                    size.Add(await doWork(client, data));
                    timer.Stop();
                    return timer.Elapsed.TotalSeconds;
                }
                finally
                {
                    client.Dispose();
                    throttler.Release();
                }
            }).ToArray();

            var results = Task.WhenAll(downloadTasks).Result;

            var avg = results.Average();
            var avgSize = size.Average();
            return Math.Round(avgSize / avg, 2);
        }

        private IEnumerable<String> GenerateDownloadUrls(Int32 count = 4)
        {
            var downloadUriBase = $"https://{this._serverProperties["host"]}/download?";
            for (var i = 0; i < count; i++)
            {
                yield return $"{downloadUriBase}nocache={Guid.NewGuid().ToString()}&size={DownloadSize}";
            }
        }

        private static IEnumerable<string> GenerateUploadData(Int32 count = 4)
        {
            var random = new Random();
            var result = new List<string>();

            for (var sizeCounter = 1; sizeCounter < UploadSize + 1; sizeCounter++)
            {
                var size = sizeCounter * 200 * 1024;
                var builder = new StringBuilder(size);

                builder.AppendFormat("content{0}=", sizeCounter);

                for (var i = 0; i < size; ++i)
                {
                    builder.Append(Chars[random.Next(Chars.Length)]);
                }

                for (var i = 0; i < count; i++)
                {
                    result.Add(builder.ToString());
                }
            }

            return result;
        }
    }
}
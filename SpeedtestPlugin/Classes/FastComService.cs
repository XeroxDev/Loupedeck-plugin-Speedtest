namespace Loupedeck.SpeedtestPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    public class FastComService : IISpeedService
    {
        public IEnumerable<SpeedManager.ServerResult> PossibleServers { get; set; }
        public String GetSpeedUrl(SpeedManager.ServerResult server, Int64 BytesPerTest, Boolean isUpload, System.Guid TryGuid) => server.server.Replace("/speedtest", $"/speedtest/range/0-{BytesPerTest}") + $"&_rng={Guid.NewGuid()}";

        public async Task RefreshPossibleServers()
        {
            if (String.IsNullOrEmpty(Token))
            {
                var jsonFilePath = await this.GetJsonFilePath();
                Token = await this.GetToken(jsonFilePath);
            }
            var resp = await SpeedTester.GetNewClient().GetStringAsync($"https://api.fast.com/netflix/speedtest/v2?https=true&urlCount={this.MaxServers}&token={Token}");
            this.PossibleServers = JsonConvert.DeserializeObject<FastAPIServerListResponse>(resp).targets.Select(a => new SpeedManager.ServerResult { server = a.url }).ToArray();
        }

        public Int64 GetServiceLikeableSize(Int32 megabytes) => ByteSize.BytesFromMB(megabytes);

        private readonly Int32 MaxServers = 10;

        private static String Token { get; set; }



        public class FastAPIServerListResponse
        {
            public class ClientInfo
            {
                public String ip;
                public String asn;
                public String isp;
                public Location location;

            }
            public class Location
            {
                public String city;
                public String country;
            }
            public ClientInfo client;
            public Target[] targets;
            public class Target
            {
                public String name;
                public String url;
                public Location location;
            }
        }

        private async Task<String> GetToken(String jsFilePath)
        {
            try
            {
                if (String.IsNullOrEmpty(jsFilePath))
                {
                    return "";
                }

                var javascript = await SpeedTester.GetNewClient().GetStringAsync(jsFilePath);

                var index = javascript?.IndexOf("token:");
                if ((index > -1) == false)
                {
                    return "";
                }

                javascript = javascript.Substring(index ?? 0);

                index = javascript?.IndexOf(",");
                if (index is null or (-1))
                {
                    return "";
                }

                javascript = javascript.Substring(0, index ?? 0);

                return javascript.Replace("\"", "").Replace("token:", "");
            }
            catch
            {
                return null;
            }
        }

        private async Task<String> GetJsonFilePath()
        {
            try
            {
                var urlBase = "https://Fast.com";
                var html = await SpeedTester.GetNewClient().GetStringAsync(urlBase);

                Int32? index = html.IndexOf("<script src=");
                if (index is null or (-1))
                {
                    return null;
                }

                html = html.Substring((index ?? 0) + 13);
                var jsFileName = html.Substring(0, html.IndexOf("\""));
                return urlBase + jsFileName;
            }
            catch
            {
                return null;
            }
        }


    }
}

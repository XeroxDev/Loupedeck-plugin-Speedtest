namespace Loupedeck.SpeedtestPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    public class SpeedTestDotNetService : IISpeedService
    {
        public IEnumerable<SpeedManager.ServerResult> PossibleServers { get; set; }

        public class APIServer
        {
            public String host { get; set; }
        }
        public String GetSpeedUrl(SpeedManager.ServerResult server, Int64 BytesPerTest, Boolean isUpload, Guid TryGuid) => $"https://{server.server}/{(isUpload ? "upload?" : $"download?size={BytesPerTest}&")}nocache={Guid.NewGuid().ToString().ToLower()}&guid={TryGuid.ToString().ToLower()}";


        public Int64 GetServiceLikeableSize(Int32 megabytes) => ByteSize.BytesFromBits(value: BitSize.BitsFromMbits(megabytes * 10));

        public async Task RefreshPossibleServers()
        {
            var client = SpeedTester.GetNewClient();
            var pos_servers = JsonConvert.DeserializeObject<IEnumerable<APIServer>>(await client.GetStringAsync("https://www.speedtest.net/api/js/servers?engine=js&limit=10&https_functional=true"));
            this.PossibleServers = pos_servers.Select(a => new SpeedManager.ServerResult { server = a.host }).ToArray();
        }
    }
}

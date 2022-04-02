namespace Loupedeck.SpeedtestPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;

    public interface IISpeedService
    {

        String GetSpeedUrl(SpeedManager.ServerResult server, Int64 BytesPerTest, Boolean isUpload, Guid TryGuid);
        Task RefreshPossibleServers();
        IEnumerable<SpeedManager.ServerResult> PossibleServers { get; }
        Int64 GetServiceLikeableSize(Int32 megabytes);
    }

    public class SpeedManager
    {

        public class SpeedTesterManagerServerException : Exception
        {
            public SpeedTesterManagerServerException(String server) => this.badServer = server;
            public String badServer { get; set; }
            public SpeedTesterManagerServerException(String server, String message) : base(message) => this.badServer = server;

            public SpeedTesterManagerServerException(String server, String message, Exception innerException) : base(message, innerException) => this.badServer = server;

            protected SpeedTesterManagerServerException(String server, SerializationInfo info, StreamingContext context) : base(info, context) => this.badServer = server;
        }
        private class BadServerEntry
        {
            public String server;
            public DateTime expires;
        }
        private readonly List<BadServerEntry> badServers = new();
        public void AddBadServer(String server, TimeSpan howLong)
        {
            lock (this.badServers)
            {
                var existing = this.badServers.FirstOrDefault(a => a.server == server);
                if (existing != null)
                {
                    this.badServers.Remove(existing);
                }

                this.badServers.Add(new BadServerEntry { server = server, expires = DateTime.Now + howLong });
            }
        }
        protected IEnumerable<ServerResult> NonBannedServers(IISpeedService service)
        {
            this.UnbanExpiredBadServers();
            lock (this.badServers)
            {
                var ret = service.PossibleServers?.Where(a => this.badServers.Any(bs => bs.server == a.server) == false && a.pingFailed==false)?.ToArray();
                if (ret?.Length == 0)//if no possible servers we clear any bad servers that were on the list for that service then return null, this should cause the server list to be refreshed
                {
                    var remove = service.PossibleServers?.Where(a => this.badServers.Any(bs => bs.server == a.server));
                    if (remove == null)
                    {
                        return null;
                    }

                    this.badServers.RemoveAll(entry => service.PossibleServers?.Any(s => s.server == entry.server) == true);
                }
                return ret;
            }

        }

        private void UnbanExpiredBadServers()
        {
            lock (this.badServers)
            {
                this.badServers.RemoveAll(bs => bs.expires < DateTime.Now);
            }
        }

        public async Task<TestResult> TestService(IISpeedService service, Boolean uploadTest, Int32 maxServers, Int32 maxTests, Int32 maxConcurrentTests, Int32 mbPerTest, Boolean no_ping_refresh = false, Boolean throwOnServerException=false)
        {
            this.UnbanExpiredBadServers();
            if ((this.NonBannedServers(service)?.Count() > 0) == false)//if there are no unbanned servers or no servers at all lets refresh
            {

                await service.RefreshPossibleServers();
            }
            var retExceptions = new List<Exception>();
            var ret = new TestResult();
            if (!no_ping_refresh)
            {
                ret = await this.RefreshServerPings(service);
                this.HandleResultAddExceptionsToList(retExceptions, ret, throwOnServerException);
            }

            var bytes = service.GetServiceLikeableSize(mbPerTest);
            var urls = this.GetSpeedUrls(service, this.NonBannedServers(service), maxServers, maxTests, bytes, uploadTest);
            var tester = new SpeedTester();
            try
            {
                ret.bytesPerSecond = await tester.GetSpeedBytesPerSec(urls.Select(a => a.url).ToArray(), maxConcurrentTests, ifUploadHowManyBytes: uploadTest ? bytes : 0);
                return ret;
            }
            catch (AggregateException aex)
            {
                if (throwOnServerException)
                {
                    throw aex;
                }

                foreach (var itm in aex.InnerExceptions)
                {
                    if (itm is SpeedTester.SpeedTesterException ste)
                    {
                        retExceptions.Add(this.HandleException(ste, urls));
                    }else
                    {
                        throw itm;
                    }
                }
                
            }
            catch (SpeedTester.SpeedTesterException ex)
            {
                retExceptions.Add( this.HandleException(ex, urls));
            }
            finally
            {
                GC.Collect();
            }
            ret.serverErrors = retExceptions.Where(e=> e != null).Count() > 0 ? new AggregateException(retExceptions.Where(e => e != null))  : null;
            return ret;
        }

        private SpeedTesterManagerServerException HandleException(SpeedTester.SpeedTesterException ex, IEnumerable<ServerUrl> urls)
        {
            var host = urls.FirstOrDefault(a => a.url == ex.url);
            return new SpeedTesterManagerServerException(host?.server, $"SpeedTester TestService failure for {host?.server}", ex);
        }

        public async Task<TestResult> RefreshServerPings(IISpeedService service, Int32 times = 2)
        {
            this.UnbanExpiredBadServers();
            if (service.PossibleServers == null)
            {
                await service.RefreshPossibleServers();
            }

            var posServers = this.NonBannedServers(service).ToList();
            var origCnt = posServers.Count;
            posServers.ForEach(a => { a.pingChecks = 0; a.pingAvg = 0; });
            var exceptions = await Task.WhenAll(posServers.Select(a => this.UpdateServerPing(a, times)));
            var ignoredServers = exceptions.Where(a => a != null).Select(a => a.badServer);
            posServers = this.NonBannedServers(service).ToList();
            if (posServers.Count == 0)
            {
                throw new SpeedTesterManagerServerException(null,$"There are no good servers left after the ping test, started with {origCnt}");
            }

            var ret = (min: posServers.Min(a => a.pingAvg), max: posServers.Max(a => a.pingAvg));
            Classes.BasicLog.LogEvt(posServers, $"Ping test done time service: {service.GetType()}");


            return new TestResult {maxPing=ret.max,minPing=ret.min,serverErrors= exceptions.Where(e => e != null).Count() > 0 ? new AggregateException(exceptions.Where(e => e != null)) : null };
        }
        public class TestResult
        {
            public Int64 bytesPerSecond;
            public AggregateException serverErrors;
            public Double minPing;
            public Double maxPing;
        }
        protected void HandleResultAddExceptionsToList(List<Exception> exceptions, TestResult result, Boolean throwOnServerException = false)
        {
            var notNullExceptions = result?.serverErrors?.InnerExceptions?.Where(e => e != null).ToArray();
            if (notNullExceptions?.Length > 0 == false)
            {
                return;
            }
            if (throwOnServerException)
            {
                throw result.serverErrors;
            }
            exceptions.AddRange(notNullExceptions);
        }
        public async Task<TestResult> DoRationalPreTestAndTest(IISpeedService service, Boolean doUpload, Boolean noPingTestFirst = false, Boolean alwaysDoSmallerFinalTest = false, Boolean throwOnServerException=false)
        {
            //first we do a small test only 6 mb total transferred, if that completes in medium time then we will do a 50 mb final test, if that is fast we can do our 250mb test
            var stopwatch = new Stopwatch();


            var superSlowSeconds = 10; //4.8 megabits a second
            var mediumSeconds = 5; //9.6 megabits a second
            var do_big_final = !alwaysDoSmallerFinalTest;
            var retExceptions = new List<Exception>();
            if (!noPingTestFirst)
            {
                var pres = await this.RefreshServerPings(service);
                this.HandleResultAddExceptionsToList(retExceptions, pres, throwOnServerException);
            }

            stopwatch.Start();
            var res = await this.TestService(service, doUpload, 2, 3, 5, 2, no_ping_refresh: true);
            this.HandleResultAddExceptionsToList(retExceptions, res, throwOnServerException);
            stopwatch.Stop();
            if (stopwatch.Elapsed.TotalSeconds > mediumSeconds)
            {
                do_big_final = false;
            }
            Classes.BasicLog.LogEvt($"Initial stopwatch time: {stopwatch.Elapsed.TotalSeconds} noPingTestFirst: {noPingTestFirst} doUpload: {doUpload} alwaysDoSmallerFinalTest: {alwaysDoSmallerFinalTest} mediumSeconds: {mediumSeconds} superSlowSeconds: {superSlowSeconds}");

            var finalMaxTests = do_big_final ? 10 : 5;
            var finalMBPerTest = do_big_final ? 25 : 10;
            var finalMaxConcurrent = do_big_final ? 5 : 3;
            if (stopwatch.Elapsed.TotalSeconds <= superSlowSeconds)
            {
                res = await this.TestService(service, doUpload, 3, finalMaxTests, finalMaxConcurrent, finalMBPerTest, no_ping_refresh: true);
                this.HandleResultAddExceptionsToList(retExceptions, res, throwOnServerException);
            }
            if (retExceptions.Count > 0)
            {
                res.serverErrors = new AggregateException(retExceptions);
            }
            return res;
        }
        public async Task<SpeedTesterManagerServerException> UpdateServerPing(SpeedManager.ServerResult server, Int32 times)
        {
            var tester = new SpeedTester();
            try
            {
                server.pingFailed = false;
                var host = server.server;
                var pings = await tester.PingServer(host, times, false);
                var time = pings.Sum() + server.pingAvg * server.pingChecks;
                server.pingChecks += times;
                server.pingAvg = time / server.pingChecks;
                await tester.PingServer(server.server, 1, true);//make sure we can tcp connect

            }
            catch (SpeedTester.SpeedTesterException ex)
            {
                server.pingFailed = true;
                Classes.BasicLog.LogEvt(ex, $"Ping failure on: {server.server}");
                return new SpeedTesterManagerServerException(server.server, $"SpeedTester TestService failure for {server.server}", ex);
            }
            return null;

        }
        public IEnumerable<ServerUrl> GetSpeedUrls(IISpeedService service, IEnumerable<ServerResult> servers, Int32 maxDiffServers, Int32 TotalTests, Int64 BytesPerTest, Boolean isUpload)
        {
            var tryGuid = Guid.NewGuid();
            var ret = new List<ServerUrl>();
            var test_servers = servers.OrderBy(a => a.pingAvg).Take(Math.Min(maxDiffServers, servers.Count())).ToArray();

            var cur_server = 0;

            while (TotalTests-- > 0)
            {
                var serverIndex = cur_server++ % test_servers.Length;
                ret.Add(new ServerUrl { url = service.GetSpeedUrl(test_servers[serverIndex], BytesPerTest, isUpload, tryGuid), server = test_servers[serverIndex].server });
            }
            return ret;
        }
        public class ServerUrl
        {
            public String url;
            public String server;
        }
        public class ServerResult
        {
            public String server;
            public Int32 pingChecks;
            public Double pingAvg;
            public Boolean pingFailed;//if failed will not use until next successful ping
        }

    }
}

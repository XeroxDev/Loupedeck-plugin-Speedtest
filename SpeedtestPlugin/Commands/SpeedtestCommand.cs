namespace Loupedeck.SpeedtestPlugin.Commands
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    public class SpeedtestCommand : PluginDynamicCommand
    {
        private Double _downloadSpeed = -1;
        private Double _uploadSpeed = -1;
        private Int64 _ping = -1;
        private Boolean _isRunning;
        private Boolean _failed;
        private readonly SpeedManager _client;
        private readonly SpeedTestDotNetService speedService;

        public SpeedtestCommand() : base("Speedtest", "Run a speedtest", "Speedtest")
        {
            this._client = new SpeedManager();
            this.speedService = new SpeedTestDotNetService();
        }
        private readonly TimeSpan badServerTimeout = TimeSpan.FromMinutes(30);
        private void HandleResultExceptions(AggregateException serverErrors, String whatTest)
        {
            if (serverErrors?.InnerExceptions?.Count > 0)
            {
                foreach (var exception in serverErrors.InnerExceptions)
                {
                    if (exception is SpeedManager.SpeedTesterManagerServerException ex)
                    {
                        Classes.BasicLog.LogEvt(exception, $"Banning server: {ex.badServer} as failed during {whatTest}");
                        this._client.AddBadServer(ex.badServer, this.badServerTimeout);
                    }
                    else
                    {
                        Classes.BasicLog.LogEvt(exception, $"Should have only returned SpeedTestManager Exceptions not sure why this got returned");
                        throw new ApplicationException($"Should not be getting here invalid exception type: {exception.GetType()} returned");
                    }
                    
                }

            }

            
        }
        protected override async void RunCommand(String actionParameter)
        {
            if (this._isRunning)
            {
                return;
            }

            // Run in another thread
            await Task.Run(async () =>
            {
                this.Reset();
                this._failed = false;
                this._isRunning = true;
                this.ActionImageChanged();//Immediately inform user test is started to seem responsive

                for (var x = 0; x < 2; x++)//try twice incase of an exception
                {
                    try
                    {
                        // Test ping
                        var ping_result = await this._client.RefreshServerPings(this.speedService);
                        this.HandleResultExceptions(ping_result.serverErrors,"Ping Test");
                        this._ping = (Int32)Math.Round(ping_result.minPing != 0 ? ping_result.minPing : -1);
                        this.ActionImageChanged();

                        var dl_result = await this._client.DoRationalPreTestAndTest(this.speedService, false, true);
                        this.HandleResultExceptions(dl_result.serverErrors, "Download Test");
                        // Test download speed
                        this._downloadSpeed = dl_result.bytesPerSecond;
                        this.ActionImageChanged();

                        // Test upload speed
                        var up_result = await this._client.DoRationalPreTestAndTest(this.speedService, true, true);
                        this.HandleResultExceptions(up_result.serverErrors, "Upload Test");
                        this._uploadSpeed = up_result.bytesPerSecond;
                        this.ActionImageChanged();
                        return;
                    }
                    catch (AggregateException ae)
                    {
                        this.HandleResultExceptions(ae, "Global catch all");
                    }
                    catch (SpeedManager.SpeedTesterManagerServerException ex)
                    {
                        this._client.AddBadServer(ex.badServer, this.badServerTimeout);
                        Classes.BasicLog.LogEvt(ex, $"Error doing speedtest on try: {x}");
                        if (x == 1)
                        {
                            this._failed = true;
                            this.ActionImageChanged();
                        }
                    }
                }

            });
            this._isRunning = false;
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var sb = new StringBuilder();
            var bmpBuilder = new BitmapBuilder(imageSize);
            if (!this._isRunning && this._failed)
            {
                bmpBuilder.DrawText("Test Failed\nTry Again");
                return bmpBuilder.ToImage();
            }

            if (this._ping <= -1)
            {
                bmpBuilder.DrawText(this._isRunning ? "Speedtest started" : "Start speedtest");
                return bmpBuilder.ToImage();
            }

            sb.AppendLine($"Ping: {this._ping} ms");
            sb.AppendLine($"↓ {(this._downloadSpeed <= -1 ? "N/A" : $"{ByteSize.HumanReadable(this._downloadSpeed, 1)}/s")}");
            sb.AppendLine($"↑ {(this._uploadSpeed <= -1 ? "N/A" : $"{ByteSize.HumanReadable(this._uploadSpeed, 1)}/s")}");

            bmpBuilder.DrawText(sb.ToString(), fontSize: 12);
            return bmpBuilder.ToImage();
        }


        private void Reset()
        {
            this._downloadSpeed = -1;
            this._uploadSpeed = -1;
            this._ping = -1;
        }
    }
}
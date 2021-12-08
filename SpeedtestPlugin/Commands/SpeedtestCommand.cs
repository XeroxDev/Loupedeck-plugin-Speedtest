namespace Loupedeck.SpeedtestPlugin.Commands
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Classes;

    using Extensions;


    public class SpeedtestCommand : PluginDynamicCommand
    {
        private Double _downloadSpeed = -1;
        private Double _uploadSpeed = -1;
        private Int64 _ping = -1;
        private Boolean _isRunning;
        private readonly SpeedtestClient _client;

        public SpeedtestCommand() : base("Speedtest", "Run a speedtest", "Speedtest") =>
            this._client = new SpeedtestClient();

        protected override void RunCommand(String actionParameter)
        {
            if (this._isRunning)
            {
                return;
            }
            
            // Run in another thread
            Task.Run(() =>
            {
                this.Reset();

                this._isRunning = true;

                // Test ping
                this._ping = this._client.PingServer();
                this.ActionImageChanged();

                // Test download speed
                this._downloadSpeed = this._client.TestDownloadSpeed();
                this.ActionImageChanged();

                // Test upload speed
                this._uploadSpeed = this._client.TestUploadSpeed();
                this.ActionImageChanged();

                this._isRunning = false;
            });
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var sb = new StringBuilder();
            var bmpBuilder = new BitmapBuilder(imageSize);
            if (this._ping <= -1)
            {
                bmpBuilder.DrawText(this._isRunning ? "Speedtest started" : "Start speedtest");
                return bmpBuilder.ToImage();
            }

            sb.AppendLine($"Ping: {this._ping} ms");
            sb.AppendLine($"↓: {(this._downloadSpeed <= -1 ? "N/A" : $"{this._downloadSpeed.ToPrettySize()}/s")}");
            sb.AppendLine($"↑: {(this._uploadSpeed <= -1 ? "N/A" : $"{this._uploadSpeed.ToPrettySize()}/s")}");

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
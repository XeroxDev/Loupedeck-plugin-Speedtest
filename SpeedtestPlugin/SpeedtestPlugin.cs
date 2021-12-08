namespace Loupedeck.SpeedtestPlugin
{
    using System;

    using Loupedeck;

    public class SpeedtestPlugin : Plugin
    {
        public override Boolean HasNoApplication => true;
        public override Boolean UsesApplicationApiOnly => true;

        public override void Load()
        {
            var resourcePath = "Loupedeck.SpeedtestPlugin.Resources.Icon.";
            this.Info.Icon16x16 = EmbeddedResources.ReadImage($"{resourcePath}icon-16.png");
            this.Info.Icon32x32 = EmbeddedResources.ReadImage($"{resourcePath}icon-32.png");
            this.Info.Icon48x48 = EmbeddedResources.ReadImage($"{resourcePath}icon-48.png");
            this.Info.Icon256x256 = EmbeddedResources.ReadImage($"{resourcePath}icon-256.png");
        }

        public override void Unload()
        {
        }

        private void OnApplicationStarted(Object sender, EventArgs e)
        {
        }

        private void OnApplicationStopped(Object sender, EventArgs e)
        {
        }

        public override void RunCommand(String commandName, String parameter)
        {
        }

        public override void ApplyAdjustment(String adjustmentName, String parameter, Int32 diff)
        {
        }
    }
}

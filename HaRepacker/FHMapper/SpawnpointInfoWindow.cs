using Avalonia.Controls;
using Avalonia.Layout;
using Footholds;
using MapleLib.WzLib.WzProperties;

namespace HaRepacker.FHMapper
{
    public class SpawnpointInfoWindow : Window
    {
        public SpawnpointInfoWindow(SpawnPoint.Spawnpoint spawnpoint)
        {
            Title = "Spawnpoint Info";
            Width = 280;
            Height = 180;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            string x = ((WzIntProperty)spawnpoint.Data["x"]).Value.ToString();
            string y = ((WzIntProperty)spawnpoint.Data["y"]).Value.ToString();
            string mobId = ((WzStringProperty)spawnpoint.Data["id"]).Value;
            string fhId = ((WzIntProperty)spawnpoint.Data["fh"]).Value.ToString();

            var okBtn = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Stretch };
            okBtn.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = $"X: {x}" },
                    new TextBlock { Text = $"Y: {y}" },
                    new TextBlock { Text = $"Mob/NPC ID: {mobId}" },
                    new TextBlock { Text = $"Foothold ID: {fhId}" },
                    okBtn
                }
            };
        }
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace PlantDraw.Core
{
    /// <summary>
    /// User preferences, stored as JSON in %AppData%\PlantDraw\settings.json.
    /// All members are properties so System.Text.Json round-trips them.
    /// </summary>
    public sealed class AppSettings
    {
        public int SchemaVersion { get; set; } = 1;

        public LengthDisplay Units { get; set; } = LengthDisplay.Millimeters;

        public CompassFrame Frame { get; set; } = CompassFrame.ProjectNorth;

        public CompassDirection Direction { get; set; } = CompassDirection.North;

        public AxisLock AxisLock { get; set; } = AxisLock.None;

        public bool Ortho { get; set; }

        public bool Chain { get; set; } = true;

        public SegmentKind Kind { get; set; } = SegmentKind.ModelLine;

        public PlaneMode Plane { get; set; } = PlaneMode.Auto;

        public bool PinViewWorkPlane { get; set; }

        public bool FlipTrueNorth { get; set; }

        public int AxisSign { get; set; } = 1;

        public string Distance { get; set; } = "1000";

        /// <summary>Survey bearing captured from the bearing box; takes precedence over the compass.</summary>
        public double? BearingOverride { get; set; }

        /// <summary>Vertical angle captured from the elevation box.</summary>
        public double? ElevationOverride { get; set; }

        // Nullable: System.Text.Json refuses to serialise double.NaN, and a missing
        // position simply means "place the pad in the middle of the screen".
        public double? WindowLeft { get; set; }

        public double? WindowTop { get; set; }

        public double WindowWidth { get; set; } = 940.0;

        public double WindowHeight { get; set; } = 620.0;

        public bool TopMost { get; set; }

        public static string FolderPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "PlantDraw");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.json"); }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    AppSettings loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable settings must never block the add-in.
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, options));
            }
            catch
            {
                // Preferences are nice to have; ignore IO problems (read-only profile, etc).
            }
        }
    }
}

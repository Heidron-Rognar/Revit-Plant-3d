namespace PlantDraw
{
    /// <summary>Names and identifiers shared by the ribbon, the manifest and the docs.</summary>
    public static class Globals
    {
        public const string TabName = "Plant Draw";
        public const string PanelName = "3D Routing";
        public const string ProductName = "Plant Draw";
        public const string Version = "1.0.0";

        public const string PadCommandClass = "PlantDraw.Commands.ShowDirectionPadCommand";
        public const string PickStartCommandClass = "PlantDraw.Commands.PickStartPointCommand";
        public const string UndoCommandClass = "PlantDraw.Commands.UndoLastSegmentCommand";
        public const string ClearCommandClass = "PlantDraw.Commands.ClearSegmentsCommand";
    }
}

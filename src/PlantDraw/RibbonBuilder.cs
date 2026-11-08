using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.UI;
using PlantDraw.Core;
using PlantDraw.UI;

namespace PlantDraw
{
    /// <summary>Builds the ribbon tab and buttons.</summary>
    internal static class RibbonBuilder
    {
        public static void Build(UIControlledApplication application)
        {
            if (application == null)
            {
                return;
            }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            try
            {
                application.CreateRibbonTab(Globals.TabName);
            }
            catch (ArgumentException)
            {
                // The tab already exists (Revit keeps tabs alive between add-in reloads).
            }

            RibbonPanel panel = FindOrCreatePanel(application, Globals.TabName, Globals.PanelName);
            if (panel == null)
            {
                return;
            }

            PushButtonData padButtonData = new PushButtonData(
                "PlantDraw_DirectionPad",
                "3D Direction\nPad",
                assemblyPath,
                Globals.PadCommandClass)
            {
                ToolTip = "Open the Plant Draw direction pad.",
                LongDescription =
                    "Plant 3D style drawing in the Revit 3D view: compass bearings (N, NE, E, SE, S, SW, W, NW), " +
                    "up and down, axis locks for X / Y / Z, ortho, exact bearing and rise angles, and a distance box. " +
                    "Segments are created as model lines or pipes."
            };

            PushButtonData pickButtonData = new PushButtonData(
                "PlantDraw_PickStart",
                "Pick\nStart",
                assemblyPath,
                Globals.PickStartCommandClass)
            {
                ToolTip = "Pick the start point of the next run in the active 3D view.",
                LongDescription = "Uses Revit snapping to place the start point exactly on model geometry."
            };

            PushButtonData undoButtonData = new PushButtonData(
                "PlantDraw_Undo",
                "Undo\nLast",
                assemblyPath,
                Globals.UndoCommandClass)
            {
                ToolTip = "Delete the last segment created from the pad."
            };

            PushButtonData clearButtonData = new PushButtonData(
                "PlantDraw_Clear",
                "Clear\nAll",
                assemblyPath,
                Globals.ClearCommandClass)
            {
                ToolTip = "Delete every segment created from the pad in this document."
            };

            try
            {
                PushButton padButton = panel.AddItem(padButtonData) as PushButton;
                if (padButton != null)
                {
                    padButton.LargeImage = IconFactory.CompassIcon(32);
                    padButton.Image = IconFactory.CompassIcon(16);
                }

                IList<RibbonItem> stacked = panel.AddStackedItems(pickButtonData, undoButtonData);
                if (stacked != null && stacked.Count >= 2)
                {
                    PushButton pickButton = stacked[0] as PushButton;
                    if (pickButton != null)
                    {
                        pickButton.Image = IconFactory.RouteIcon(16);
                    }

                    PushButton undoButton = stacked[1] as PushButton;
                    if (undoButton != null)
                    {
                        undoButton.Image = IconFactory.UndoIcon(16);
                    }
                }

                PushButton clearButton = panel.AddItem(clearButtonData) as PushButton;
                if (clearButton != null)
                {
                    clearButton.Image = IconFactory.ClearIcon(16);
                }
            }
            catch (Exception exception)
            {
                PlantDrawRuntime.SetStatus("Could not build the Plant Draw ribbon: " + exception.Message, true);
            }
        }

        private static RibbonPanel FindOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            try
            {
                List<RibbonPanel> panels = application.GetRibbonPanels(tabName);
                if (panels != null)
                {
                    foreach (RibbonPanel panel in panels)
                    {
                        if (string.Equals(panel.Name, panelName, StringComparison.Ordinal))
                        {
                            return panel;
                        }
                    }
                }
            }
            catch
            {
                // Fall through and try to create the panel.
            }

            try
            {
                return application.CreateRibbonPanel(tabName, panelName);
            }
            catch (Exception exception)
            {
                PlantDrawRuntime.SetStatus("Could not create the Plant Draw ribbon panel: " + exception.Message, true);
                return null;
            }
        }
    }
}

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlantDraw.Core;
using PlantDraw.UI;

namespace PlantDraw.Commands
{
    internal static class CommandSupport
    {
        public const ObjectSnapTypes SnapTypes =
            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest |
            ObjectSnapTypes.Intersections | ObjectSnapTypes.WorkPlaneGrid;

        public static bool TryGetDocument(ExternalCommandData commandData, ref string message, out UIDocument uiDocument)
        {
            uiDocument = null;

            if (commandData == null || commandData.Application == null)
            {
                message = "Revit is not available.";
                return false;
            }

            uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
            {
                message = "Open a document first.";
                return false;
            }

            return true;
        }

        public static DrawSession GetSession(UIDocument uiDocument)
        {
            DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
            if (session != null)
            {
                session.FlipTrueNorth = PlantDrawRuntime.Settings.FlipTrueNorth;
            }

            return session;
        }
    }

    /// <summary>Opens (or focuses) the modeless 3D direction pad.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ShowDirectionPadCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null || commandData.Application == null)
                {
                    message = "Revit is not available.";
                    return Result.Failed;
                }

                if (!PlantDrawRuntime.IsInitialized)
                {
                    PlantDrawRuntime.Initialize();
                }

                DirectionPadHost.Show(commandData.Application);

                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    PlantDrawRuntime.SetStatus("Open a document, then open a 3D view to start drawing.", true);
                }
                else if (!ViewChecks.TryValidate(uiDocument.ActiveView, out string problem))
                {
                    PlantDrawRuntime.SetStatus(problem, true);
                }
                else
                {
                    PlantDrawRuntime.SetStatus("Pick a start point, choose a direction and press Enter to draw.", false);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Sets the anchor from a point picked in the 3D view, then opens the pad.</summary>
    [Transaction(TransactionMode.Manual)]
    public class PickStartPointCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!PlantDrawRuntime.IsInitialized)
            {
                PlantDrawRuntime.Initialize();
            }

            UIDocument uiDocument;
            if (!CommandSupport.TryGetDocument(commandData, ref message, out uiDocument))
            {
                return Result.Failed;
            }

            if (!ViewChecks.TryValidate(uiDocument.ActiveView, out string problem))
            {
                message = problem;
                return Result.Failed;
            }

            DirectionPadHost.Show(commandData.Application);

            try
            {
                XYZ point = uiDocument.Selection.PickPoint(CommandSupport.SnapTypes, "Plant Draw: pick the start point");
                DrawSession session = CommandSupport.GetSession(uiDocument);
                if (session != null)
                {
                    session.SetAnchor(point);
                    DirectionPadHost.Refresh();
                    PlantDrawRuntime.SetStatus("Start point set. Pick a direction and press Enter to draw.", false);
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = "Could not pick a point: " + exception.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Deletes the last segment drawn with the pad.</summary>
    [Transaction(TransactionMode.Manual)]
    public class UndoLastSegmentCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument;
            if (!CommandSupport.TryGetDocument(commandData, ref message, out uiDocument))
            {
                return Result.Failed;
            }

            DrawSession session = CommandSupport.GetSession(uiDocument);
            if (session == null)
            {
                message = "Nothing to undo.";
                return Result.Failed;
            }

            try
            {
                string result = session.UndoLast();
                DirectionPadHost.Refresh();
                PlantDrawRuntime.SetStatus(result, false);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Deletes every segment created by the pad in this document.</summary>
    [Transaction(TransactionMode.Manual)]
    public class ClearSegmentsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument;
            if (!CommandSupport.TryGetDocument(commandData, ref message, out uiDocument))
            {
                return Result.Failed;
            }

            DrawSession session = CommandSupport.GetSession(uiDocument);
            if (session == null)
            {
                message = "Nothing to clear.";
                return Result.Failed;
            }

            try
            {
                string result = session.ClearAll();
                DirectionPadHost.Refresh();
                PlantDrawRuntime.SetStatus(result, false);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return Result.Failed;
            }
        }
    }
}

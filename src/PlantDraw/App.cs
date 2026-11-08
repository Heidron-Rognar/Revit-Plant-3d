using System;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using PlantDraw.Core;
using PlantDraw.UI;

namespace PlantDraw
{
    /// <summary>Add-in entry point: ribbon, runtime state and housekeeping.</summary>
    public class App : IExternalApplication
    {
        private DateTime _lastIdleRefresh = DateTime.MinValue;
        private bool _eventsAttached;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                PlantDrawRuntime.Initialize();
                RibbonBuilder.Build(application);
                AttachEvents(application);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                TaskDialog.Show(
                    Globals.ProductName,
                    "The Plant Draw add-in failed to start." + Environment.NewLine + Environment.NewLine + exception);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                DetachEvents(application);
                DirectionPadHost.Close();
                PlantDrawRuntime.SaveSettings();
            }
            catch
            {
                // Shutting down - nothing useful to report.
            }

            return Result.Succeeded;
        }

        private void AttachEvents(UIControlledApplication application)
        {
            if (application == null || _eventsAttached)
            {
                return;
            }

            application.ControlledApplication.DocumentClosed += OnDocumentClosed;
            application.Idling += OnIdling;
            _eventsAttached = true;
        }

        private void DetachEvents(UIControlledApplication application)
        {
            if (application == null || !_eventsAttached)
            {
                return;
            }

            application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            application.Idling -= OnIdling;
            _eventsAttached = false;
        }

        private void OnDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            PlantDrawRuntime.ForgetDocument(e.Document);
        }

        /// <summary>
        /// Keeps the pad read-outs in sync while the user orbits or switches views.
        /// Throttled so Revit stays responsive.
        /// </summary>
        private void OnIdling(object sender, IdlingEventArgs e)
        {
            if (!DirectionPadHost.IsOpen)
            {
                return;
            }

            if ((DateTime.UtcNow - _lastIdleRefresh).TotalMilliseconds < 800.0)
            {
                return;
            }

            _lastIdleRefresh = DateTime.UtcNow;
            DirectionPadHost.Refresh();
        }
    }
}

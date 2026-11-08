using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PlantDraw.Core
{
    /// <summary>Add-in wide state: settings, sessions per document, and the task bridge.</summary>
    public static class PlantDrawRuntime
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Document, DrawSession> Sessions = new Dictionary<Document, DrawSession>();

        public static AppSettings Settings { get; private set; } = new AppSettings();

        public static RevitTaskBridge Bridge { get; private set; }

        public static ExternalEvent TaskEvent { get; private set; }

        public static bool IsInitialized
        {
            get { return Bridge != null && TaskEvent != null; }
        }

        /// <summary>Status messages raised from the Revit thread; the pad window listens to this.</summary>
        public static event Action<string, bool> StatusChanged;

        public static void Initialize()
        {
            Settings = AppSettings.Load();
            Bridge = new RevitTaskBridge();
            TaskEvent = ExternalEvent.Create(Bridge);
        }

        /// <summary>Queues work for the Revit thread and wakes the external event.</summary>
        public static void Post(Action<UIApplication> action, string description)
        {
            if (action == null)
            {
                return;
            }

            if (!IsInitialized)
            {
                SetStatus("Plant Draw is not initialised. Restart Revit.", true);
                return;
            }

            Bridge.Post(action, description);

            try
            {
                TaskEvent.Raise();
            }
            catch (Exception exception)
            {
                SetStatus("Could not reach Revit: " + exception.Message, true);
            }
        }

        public static DrawSession GetSession(Document document)
        {
            if (document == null)
            {
                return null;
            }

            lock (Gate)
            {
                DrawSession session;
                if (!Sessions.TryGetValue(document, out session) || session == null)
                {
                    session = new DrawSession(document);
                    Sessions[document] = session;
                }

                session.FlipTrueNorth = Settings.FlipTrueNorth;
                return session;
            }
        }

        public static void ForgetDocument(Document document)
        {
            if (document == null)
            {
                return;
            }

            lock (Gate)
            {
                Sessions.Remove(document);
            }
        }

        public static void SetStatus(string message, bool isError = false)
        {
            Action<string, bool> handler = StatusChanged;
            if (handler != null)
            {
                try
                {
                    handler(message ?? string.Empty, isError);
                }
                catch
                {
                    // A broken listener must not take Revit down.
                }
            }
        }

        public static void SaveSettings()
        {
            if (Settings != null)
            {
                Settings.Save();
            }
        }
    }
}

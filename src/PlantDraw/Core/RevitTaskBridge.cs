using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace PlantDraw.Core
{
    /// <summary>
    /// The only safe way for a modeless window to touch the Revit API: queue the work,
    /// then raise an <see cref="ExternalEvent"/>. Everything in the queue runs inside a
    /// valid API context, on Revit's main thread.
    /// </summary>
    public sealed class RevitTaskBridge : IExternalEventHandler
    {
        private readonly ConcurrentQueue<QueuedTask> _queue = new ConcurrentQueue<QueuedTask>();

        public string GetName()
        {
            return "Plant Draw task bridge";
        }

        public void Execute(UIApplication application)
        {
            QueuedTask task;
            while (_queue.TryDequeue(out task))
            {
                try
                {
                    task.Action(application);
                }
                catch (Exception exception)
                {
                    PlantDrawRuntime.SetStatus(task.Description + " failed: " + exception.Message, true);
                }
            }
        }

        public void Post(Action<UIApplication> action, string description)
        {
            if (action == null)
            {
                return;
            }

            _queue.Enqueue(new QueuedTask(action, description));
        }

        public int PendingCount
        {
            get { return _queue.Count; }
        }

        private sealed class QueuedTask
        {
            public QueuedTask(Action<UIApplication> action, string description)
            {
                Action = action;
                Description = string.IsNullOrWhiteSpace(description) ? "Command" : description;
            }

            public Action<UIApplication> Action { get; }

            public string Description { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>One committed segment, remembered so it can be undone again.</summary>
    public sealed class CreatedSegment
    {
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public ElementId SketchPlaneId { get; set; } = ElementId.InvalidElementId;
        public double Length { get; set; }
        public string Kind { get; set; }
    }

    /// <summary>
    /// Per-document drawing state: where the pen currently is, what has been drawn, and how
    /// the compass maps onto this document's north.
    /// </summary>
    public sealed class DrawSession
    {
        private readonly List<CreatedSegment> _created = new List<CreatedSegment>();
        private readonly Dictionary<string, ElementId> _planeCache = new Dictionary<string, ElementId>();

        public DrawSession(Document document)
        {
            Document = document;
        }

        public Document Document { get; }

        public XYZ Anchor { get; private set; } = XYZ.Zero;

        public bool HasAnchor { get; private set; }

        public double TotalLength { get; private set; }

        public int SegmentCount
        {
            get { return _created.Count; }
        }

        public bool FlipTrueNorth { get; set; }

        public IDictionary<string, ElementId> PlaneCache
        {
            get { return _planeCache; }
        }

        public void SetAnchor(XYZ point)
        {
            Anchor = new XYZ(point.X, point.Y, point.Z);
            HasAnchor = true;
        }

        public void ClearAnchor()
        {
            HasAnchor = false;
        }

        public void RegisterSegment(SegmentResult result)
        {
            if (result == null || result.ElementId == ElementId.InvalidElementId)
            {
                return;
            }

            _created.Add(new CreatedSegment
            {
                ElementId = result.ElementId,
                SketchPlaneId = result.SketchPlaneId,
                Length = result.Length,
                Kind = result.Kind
            });

            TotalLength += result.Length;
        }

        /// <summary>Removes the last segment. Returns a message describing what happened.</summary>
        public string UndoLast()
        {
            if (_created.Count == 0)
            {
                return "Nothing to undo yet.";
            }

            CreatedSegment last = _created[_created.Count - 1];
            _created.RemoveAt(_created.Count - 1);

            TransactionGuard(() =>
            {
                if (last.ElementId != ElementId.InvalidElementId && Document.GetElement(last.ElementId) != null)
                {
                    Document.Delete(last.ElementId);
                }
            }, "Plant Draw - undo segment");

            TotalLength = Math.Max(0.0, TotalLength - last.Length);

            if (_created.Count == 0)
            {
                HasAnchor = false;
            }

            return string.Format("Removed the last {0}.", last.Kind ?? "segment");
        }

        /// <summary>Deletes everything this session created.</summary>
        public string ClearAll()
        {
            if (_created.Count == 0 && _planeCache.Count == 0)
            {
                HasAnchor = false;
                return "Nothing to clear.";
            }

            int count = _created.Count;
            List<CreatedSegment> segments = new List<CreatedSegment>(_created);

            TransactionGuard(() =>
            {
                foreach (CreatedSegment segment in segments)
                {
                    if (segment.ElementId != ElementId.InvalidElementId && Document.GetElement(segment.ElementId) != null)
                    {
                        Document.Delete(segment.ElementId);
                    }
                }

                // Helper sketch planes are removed last: deleting them earlier can take hosted curves with them.
                foreach (KeyValuePair<string, ElementId> entry in _planeCache.ToList())
                {
                    if (entry.Value != ElementId.InvalidElementId && Document.GetElement(entry.Value) is SketchPlane)
                    {
                        Document.Delete(entry.Value);
                    }
                }
            }, "Plant Draw - clear");

            _created.Clear();
            _planeCache.Clear();
            TotalLength = 0.0;
            HasAnchor = false;

            return string.Format("Cleared {0} segment(s).", count);
        }

        private void TransactionGuard(Action body, string name)
        {
            if (Document == null || !Document.IsValidObject)
            {
                return;
            }

            Tx.Run(Document, name, body);
        }

        /// <summary>Rotation of project north from true north, in radians (0 when unavailable).</summary>
        public double GetProjectAngleRadians()
        {
            try
            {
                ProjectLocation location = Document.ActiveProjectLocation;
                if (location == null)
                {
                    return 0.0;
                }

                ProjectPosition position = location.GetProjectPosition(XYZ.Zero);
                return position == null ? 0.0 : position.Angle;
            }
            catch
            {
                return 0.0;
            }
        }

        public FrameBasis BuildFrame(CompassFrame frame, View view)
        {
            switch (frame)
            {
                case CompassFrame.TrueNorth:
                    return FrameBasis.FromTrueNorth(GetProjectAngleRadians(), FlipTrueNorth);

                case CompassFrame.View:
                    View3D view3D = view as View3D;
                    if (view3D != null)
                    {
                        try
                        {
                            ViewOrientation3D orientation = view3D.GetOrientation();
                            XYZ right = Vec.Cross(orientation.ForwardDirection, orientation.UpDirection);
                            return FrameBasis.FromView(right, orientation.UpDirection, orientation.ForwardDirection);
                        }
                        catch
                        {
                            return FrameBasis.World;
                        }
                    }

                    return FrameBasis.World;

                default:
                    return FrameBasis.World;
            }
        }

        /// <summary>Best guess for a start point before the user picks one.</summary>
        public static XYZ DefaultAnchor(Document document, View view, ICollection<ElementId> selection)
        {
            XYZ fromSelection = AnchorFromSelection(document, view, selection);
            if (fromSelection != null)
            {
                return fromSelection;
            }

            Level level = SegmentFactory.FindLevel(document, XYZ.Zero);
            if (level != null)
            {
                return new XYZ(0.0, 0.0, level.ProjectElevation);
            }

            return XYZ.Zero;
        }

        /// <summary>Start point taken from the current selection (element location or bounding box centre).</summary>
        public static XYZ AnchorFromSelection(Document document, View view, ICollection<ElementId> selection)
        {
            if (document == null || !document.IsValidObject || selection == null || selection.Count == 0)
            {
                return null;
            }

            foreach (ElementId id in selection)
            {
                Element element = document.GetElement(id);
                XYZ point = AnchorFromElement(element, view);
                if (point != null)
                {
                    return point;
                }
            }

            return null;
        }

        public static XYZ AnchorFromElement(Element element, View view)
        {
            if (element == null)
            {
                return null;
            }

            Location location = element.Location;
            LocationPoint point = location as LocationPoint;
            if (point != null)
            {
                return point.Point;
            }

            LocationCurve curve = location as LocationCurve;
            if (curve != null && curve.Curve != null)
            {
                return curve.Curve.GetEndPoint(1);
            }

            if (view != null)
            {
                try
                {
                    BoundingBoxXYZ box = element.get_BoundingBox(view);
                    if (box != null)
                    {
                        return new XYZ(
                            (box.Min.X + box.Max.X) * 0.5,
                            (box.Min.Y + box.Max.Y) * 0.5,
                            (box.Min.Z + box.Max.Z) * 0.5);
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}

using System;
using Autodesk.Revit.DB;

namespace PlantDraw.Core
{
    /// <summary>Guards against views that cannot host 3D model geometry.</summary>
    public static class ViewChecks
    {
        public static bool TryValidate(View view, out string reason)
        {
            reason = null;

            if (view == null)
            {
                reason = "No active view.";
                return false;
            }

            if (view is ViewSheet)
            {
                reason = "A sheet cannot host model lines. Open a 3D view first.";
                return false;
            }

            if (view is ViewSchedule || view is ViewDrafting)
            {
                reason = "This view type cannot host 3D model geometry. Open a 3D view first.";
                return false;
            }

            View3D view3D = view as View3D;
            if (view3D != null && view3D.IsPerspective)
            {
                reason = "Perspective 3D views cannot host model geometry. Switch the view to orthographic (View Properties > Camera > Projection = Orthographic).";
                return false;
            }

            if (view.IsTemplate)
            {
                reason = "View templates cannot be drawn in. Pick a normal 3D view.";
                return false;
            }

            return true;
        }

        public static bool IsThreeDimensional(View view)
        {
            return view is View3D;
        }

        public static bool IsPerspective(View view)
        {
            View3D view3D = view as View3D;
            return view3D != null && view3D.IsPerspective;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlantDraw.Core;

namespace PlantDraw.UI
{
    /// <summary>
    /// The modeless direction pad. It never touches the Revit API directly: every call is
    /// queued through <see cref="PlantDrawRuntime.Post"/> and executed on Revit's thread.
    /// </summary>
    public partial class DirectionPadWindow : Window
    {
        private const ObjectSnapTypes SnapTypes =
            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest |
            ObjectSnapTypes.Intersections | ObjectSnapTypes.WorkPlaneGrid;

        private static readonly Brush NormalStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x40, 0x4D));
        private static readonly Brush ErrorStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0x2A, 0x2A));

        private FrameBasis _frame = FrameBasis.World;
        private XYZ _anchor = XYZ.Zero;
        private bool _hasAnchor;
        private string _viewName = "(none)";
        private bool _viewIs3D;
        private bool _viewIsPerspective;
        private int _segmentCount;
        private double _totalLength;
        private ResolvedDirection _resolved = ResolvedDirection.Invalid;
        private bool _suspend;
        private bool _refreshQueued;

        public DirectionPadWindow()
        {
            InitializeComponent();

            Compass.DirectionPicked += OnCompassDirectionPicked;
            Compass.AxisLockRequested += OnCompassAxisLockRequested;

            Loaded += HandleLoaded;
            Activated += HandleActivated;
            Closed += HandleClosed;
            KeyDown += OnWindowKeyDown;

            PlantDrawRuntime.StatusChanged += OnStatusChanged;

            ApplySettingsToUi();
            UpdateDirectionPreview();
        }

        private static AppSettings Settings
        {
            get { return PlantDrawRuntime.Settings; }
        }

        // ---------------------------------------------------------------- lifecycle

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            AppSettings settings = Settings;

            if (settings.WindowWidth >= 800.0)
            {
                Width = settings.WindowWidth;
            }

            if (settings.WindowHeight >= 520.0)
            {
                Height = settings.WindowHeight;
            }

            if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Math.Max(0.0, Math.Min(settings.WindowLeft.Value, SystemParameters.VirtualScreenWidth - 320.0));
                Top = Math.Max(0.0, Math.Min(settings.WindowTop.Value, SystemParameters.VirtualScreenHeight - 200.0));
            }

            RequestRefresh();
        }

        private void HandleActivated(object sender, EventArgs e)
        {
            RequestRefresh();
        }

        private void HandleClosed(object sender, EventArgs e)
        {
            PlantDrawRuntime.StatusChanged -= OnStatusChanged;

            AppSettings settings = Settings;
            if (WindowState == WindowState.Normal)
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }

            settings.Distance = TxtDistance.Text;
            PlantDrawRuntime.SaveSettings();
        }

        // ---------------------------------------------------------------- refresh

        /// <summary>Pulls view, anchor and session state from Revit into the pad.</summary>
        public void RequestRefresh()
        {
            if (_refreshQueued)
            {
                return;
            }

            _refreshQueued = true;

            PlantDrawRuntime.Post(application =>
            {
                _refreshQueued = false;

                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    _viewName = "(no document)";
                    _viewIs3D = false;
                    _viewIsPerspective = false;
                    _hasAnchor = false;
                    _segmentCount = 0;
                    _totalLength = 0.0;
                    ApplyReadouts();
                    UpdateDirectionPreview();
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
                if (session == null)
                {
                    return;
                }

                session.FlipTrueNorth = Settings.FlipTrueNorth;
                RefreshCaches(uiDocument, session, uiDocument.ActiveView);
            }, "Refresh pad");
        }

        private void RefreshCaches(UIDocument uiDocument, DrawSession session, View view)
        {
            _frame = session.BuildFrame(Settings.Frame, view);
            _anchor = session.Anchor;
            _hasAnchor = session.HasAnchor;
            _segmentCount = session.SegmentCount;
            _totalLength = session.TotalLength;
            _viewName = view == null ? "(none)" : view.Name;
            _viewIs3D = ViewChecks.IsThreeDimensional(view);
            _viewIsPerspective = ViewChecks.IsPerspective(view);

            ApplyReadouts();
            UpdateDirectionPreview();
        }

        private void ApplyReadouts()
        {
            LengthDisplay units = Settings.Units;
            string suffix = LengthUnits.UnitSuffix(units);

            TxtAnchor.Text = _hasAnchor
                ? SegmentFactory.DescribePoint(_anchor, units) + " " + suffix
                : "(not set)";

            TxtTotal.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} segment(s), {1}",
                _segmentCount,
                LengthUnits.Format(_totalLength, units));

            string viewText = _viewName;
            if (_viewIsPerspective)
            {
                viewText += " - perspective (use an orthographic 3D view)";
            }
            else if (!_viewIs3D)
            {
                viewText += " - not a 3D view";
            }

            TxtView.Text = viewText;
        }

        // ---------------------------------------------------------------- direction preview

        private DirectionRequest BuildRequest()
        {
            AppSettings settings = Settings;
            return new DirectionRequest
            {
                Direction = settings.Direction,
                Frame = settings.Frame,
                AxisLock = settings.AxisLock,
                Ortho = settings.Ortho,
                BearingDegrees = settings.BearingOverride,
                ElevationDegrees = settings.ElevationOverride,
                AxisSign = settings.AxisSign
            };
        }

        private void UpdateDirectionPreview()
        {
            if (_suspend)
            {
                return;
            }

            AppSettings settings = Settings;
            ResolvedDirection resolved = DirectionResolver.Resolve(BuildRequest(), _frame);
            _resolved = resolved;

            double east = 0.0;
            double north = 0.0;
            double up = 0.0;
            if (resolved.IsValid)
            {
                FrameBasis frame = _frame;
                frame.FromWorld(resolved.Vector, out east, out north, out up);
            }

            Compass.Direction = settings.Direction;
            Compass.AxisLock = settings.AxisLock;
            Compass.East = east;
            Compass.North = north;
            Compass.Up = up;

            TxtDirection.Text = resolved.IsValid ? resolved.Label : "-";
            TxtBearingValue.Text = resolved.IsValid
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} deg  /  {1:+0.0;-0.0;0.0} deg",
                    resolved.BearingDegrees,
                    resolved.ElevationDegrees)
                : "-";

            double distance;
            bool hasDistance = TryReadDistance(out distance);

            if (resolved.IsValid && hasDistance)
            {
                XYZ delta = Vec.Mul(resolved.Vector, distance);
                TxtDelta.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "E {0}  N {1}  Z {2} {3}",
                    LengthUnits.FormatNumber(delta.X, settings.Units),
                    LengthUnits.FormatNumber(delta.Y, settings.Units),
                    LengthUnits.FormatNumber(delta.Z, settings.Units),
                    LengthUnits.UnitSuffix(settings.Units));

                TxtNext.Text = _hasAnchor
                    ? SegmentFactory.DescribePoint(Vec.Add(_anchor, delta), settings.Units)
                    : "(pick a start point first)";
            }
            else
            {
                TxtDelta.Text = "-";
                TxtNext.Text = _hasAnchor ? "(no direction)" : "(pick a start point first)";
            }
        }

        private bool TryReadDistance(out double feet)
        {
            feet = 0.0;
            if (!LengthUnits.TryParse(TxtDistance == null ? string.Empty : TxtDistance.Text, Settings.Units, out feet))
            {
                return false;
            }

            return feet > 1e-6;
        }

        // ---------------------------------------------------------------- settings <-> ui

        private void ApplySettingsToUi()
        {
            AppSettings settings = Settings;
            _suspend = true;
            try
            {
                switch (settings.Frame)
                {
                    case CompassFrame.TrueNorth:
                        RbFrameTrue.IsChecked = true;
                        break;
                    case CompassFrame.View:
                        RbFrameView.IsChecked = true;
                        break;
                    default:
                        RbFrameProject.IsChecked = true;
                        break;
                }

                ApplyAxisLockToUi(settings.AxisLock);

                RbModelLine.IsChecked = settings.Kind != SegmentKind.Pipe;
                RbPipe.IsChecked = settings.Kind == SegmentKind.Pipe;

                ChkOrtho.IsChecked = settings.Ortho;
                ChkChain.IsChecked = settings.Chain;
                ChkPinPlane.IsChecked = settings.PinViewWorkPlane;
                ChkFlipNorth.IsChecked = settings.FlipTrueNorth;
                ChkTopMost.IsChecked = settings.TopMost;
                TopMost = settings.TopMost;

                CmbPlane.SelectedIndex = (int)settings.Plane;
                CmbUnits.SelectedIndex = (int)settings.Units;

                TxtDistance.Text = settings.Distance ?? "1000";
                TxtBearing.Text = settings.BearingOverride.HasValue
                    ? settings.BearingOverride.Value.ToString("0.#", CultureInfo.InvariantCulture)
                    : string.Empty;
                TxtElevation.Text = settings.ElevationOverride.HasValue
                    ? settings.ElevationOverride.Value.ToString("0.#", CultureInfo.InvariantCulture)
                    : string.Empty;

                ApplyUnitLabels();
            }
            finally
            {
                _suspend = false;
            }
        }

        /// <summary>Runs UI updates without letting the controls' change handlers write back into settings.</summary>
        private void WithSuspended(Action action)
        {
            bool previous = _suspend;
            _suspend = true;
            try
            {
                action();
            }
            finally
            {
                _suspend = previous;
            }
        }

        private void ApplyAxisLockToUi(AxisLock axisLock)
        {
            bool previous = _suspend;
            _suspend = true;
            try
            {
                switch (axisLock)
                {
                    case AxisLock.X:
                        RbLockX.IsChecked = true;
                        break;
                    case AxisLock.Y:
                        RbLockY.IsChecked = true;
                        break;
                    case AxisLock.Z:
                        RbLockZ.IsChecked = true;
                        break;
                    default:
                        RbLockNone.IsChecked = true;
                        break;
                }
            }
            finally
            {
                _suspend = previous;
            }
        }

        private void ApplyUnitLabels()
        {
            LengthDisplay units = Settings.Units;

            bool previous = _suspend;
            _suspend = true;
            try
            {
                TxtUnit.Text = LengthUnits.UnitSuffix(units);
                double[] quick = LengthUnits.QuickValues(units);
                BtnQuick1.Content = string.Format(CultureInfo.InvariantCulture, "{0:0.###}", quick[0]);
                BtnQuick2.Content = string.Format(CultureInfo.InvariantCulture, "{0:0.###}", quick[1]);
                BtnQuick3.Content = string.Format(CultureInfo.InvariantCulture, "{0:0.###}", quick[2]);
            }
            finally
            {
                _suspend = previous;
            }
        }

        private void SetStatus(string message, bool isError = false)
        {
            TxtStatus.Text = message ?? string.Empty;
            TxtStatus.Foreground = isError ? ErrorStatusBrush : NormalStatusBrush;
        }

        private void OnStatusChanged(string message, bool isError)
        {
            if (!IsLoaded)
            {
                return;
            }

            Dispatcher.InvokeAsync(new Action(() => SetStatus(message, isError)));
        }

        // ---------------------------------------------------------------- compass handlers

        private void OnCompassDirectionPicked(object sender, CompassDirection direction)
        {
            Settings.Direction = direction;
            Settings.BearingOverride = null;
            TxtBearing.Text = string.Empty;
            UpdateDirectionPreview();
        }

        private void OnCompassAxisLockRequested(object sender, AxisLock axis)
        {
            AxisLock next = Settings.AxisLock == axis ? AxisLock.None : axis;
            Settings.AxisLock = next;
            ApplyAxisLockToUi(next);
            UpdateDirectionPreview();
        }

        private void OnUpClick(object sender, RoutedEventArgs e)
        {
            Settings.Direction = CompassDirection.Up;
            Settings.BearingOverride = null;
            TxtBearing.Text = string.Empty;
            UpdateDirectionPreview();
        }

        private void OnDownClick(object sender, RoutedEventArgs e)
        {
            Settings.Direction = CompassDirection.Down;
            Settings.BearingOverride = null;
            TxtBearing.Text = string.Empty;
            UpdateDirectionPreview();
        }

        private void OnFlipClick(object sender, RoutedEventArgs e)
        {
            double current = _resolved.IsValid ? _resolved.BearingDegrees : 0.0;
            Settings.BearingOverride = DirectionResolver.Norm360(current + 180.0);
            Settings.Direction = CompassDirection.None;
            Settings.AxisSign = -Settings.AxisSign;
            TxtBearing.Text = Settings.BearingOverride.Value.ToString("0.#", CultureInfo.InvariantCulture);
            UpdateDirectionPreview();
        }

        private void OnLevelClick(object sender, RoutedEventArgs e)
        {
            Settings.ElevationOverride = 0.0;
            TxtElevation.Text = "0";
            if (Settings.Direction == CompassDirection.Up || Settings.Direction == CompassDirection.Down)
            {
                Settings.Direction = CompassDirection.North;
            }

            UpdateDirectionPreview();
        }

        private void OnAxisLockChecked(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            AxisLock axis = AxisLock.None;
            if (RbLockX.IsChecked == true)
            {
                axis = AxisLock.X;
            }
            else if (RbLockY.IsChecked == true)
            {
                axis = AxisLock.Y;
            }
            else if (RbLockZ.IsChecked == true)
            {
                axis = AxisLock.Z;
            }

            Settings.AxisLock = axis;
            if (axis == AxisLock.None)
            {
                Settings.AxisSign = 1;
            }

            UpdateDirectionPreview();
        }

        private void OnFrameChecked(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.Frame = RbFrameTrue.IsChecked == true
                ? CompassFrame.TrueNorth
                : RbFrameView.IsChecked == true
                    ? CompassFrame.View
                    : CompassFrame.ProjectNorth;

            // The frame needs data from the model (base point angle / camera), so ask Revit.
            RequestRefresh();
            UpdateDirectionPreview();
        }

        private void OnOrthoChanged(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.Ortho = ChkOrtho.IsChecked == true;
            UpdateDirectionPreview();
        }

        // ---------------------------------------------------------------- bearing / elevation

        private void OnBearingKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyBearing();
                e.Handled = true;
            }
        }

        private void OnApplyBearingClick(object sender, RoutedEventArgs e)
        {
            ApplyBearing();
        }

        private void ApplyBearing()
        {
            if (!double.TryParse(TxtBearing.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bearing))
            {
                SetStatus("Type a bearing in degrees, for example 45.", true);
                return;
            }

            Settings.BearingOverride = DirectionResolver.Norm360(bearing);
            Settings.Direction = CompassDirection.None;
            Settings.Ortho = false;
            WithSuspended(() => ChkOrtho.IsChecked = false);

            TxtBearing.Text = Settings.BearingOverride.Value.ToString("0.#", CultureInfo.InvariantCulture);
            UpdateDirectionPreview();
            SetStatus(string.Format(CultureInfo.InvariantCulture, "Bearing set to {0:0.#} deg (ortho switched off so the angle is exact).", Settings.BearingOverride.Value), false);
        }

        private void OnClearBearingClick(object sender, RoutedEventArgs e)
        {
            Settings.BearingOverride = null;
            TxtBearing.Text = string.Empty;
            if (Settings.Direction == CompassDirection.None)
            {
                Settings.Direction = CompassDirection.North;
            }

            UpdateDirectionPreview();
        }

        private void OnElevationKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyElevation();
                e.Handled = true;
            }
        }

        private void OnApplyElevationClick(object sender, RoutedEventArgs e)
        {
            ApplyElevation();
        }

        private void ApplyElevation()
        {
            if (!double.TryParse(TxtElevation.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double elevation))
            {
                SetStatus("Type an elevation angle in degrees, for example 30 (or -90 for straight down).", true);
                return;
            }

            Settings.ElevationOverride = Math.Max(-90.0, Math.Min(90.0, elevation));
            Settings.Ortho = false;
            WithSuspended(() => ChkOrtho.IsChecked = false);

            if (Settings.Direction == CompassDirection.Up || Settings.Direction == CompassDirection.Down)
            {
                Settings.Direction = CompassDirection.North;
            }

            TxtElevation.Text = Settings.ElevationOverride.Value.ToString("0.#", CultureInfo.InvariantCulture);
            UpdateDirectionPreview();
        }

        private void OnClearElevationClick(object sender, RoutedEventArgs e)
        {
            Settings.ElevationOverride = null;
            TxtElevation.Text = string.Empty;
            UpdateDirectionPreview();
        }

        // ---------------------------------------------------------------- segment options

        private void OnDistanceKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DrawUsingToggles();
                e.Handled = true;
            }
        }

        private void OnQuickClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Content != null)
            {
                TxtDistance.Text = button.Content.ToString();
                UpdateDirectionPreview();
            }
        }

        private void OnChainChanged(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.Chain = ChkChain.IsChecked == true;
        }

        private void OnModeChecked(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.Kind = RbPipe.IsChecked == true ? SegmentKind.Pipe : SegmentKind.ModelLine;
        }

        private void OnPlaneChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            int index = CmbPlane.SelectedIndex < 0 ? 0 : CmbPlane.SelectedIndex;
            Settings.Plane = (PlaneMode)index;
        }

        private void OnPinPlaneChanged(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.PinViewWorkPlane = ChkPinPlane.IsChecked == true;
        }

        private void OnUnitsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            LengthDisplay previous = Settings.Units;
            LengthDisplay next = (LengthDisplay)Math.Max(0, CmbUnits.SelectedIndex);
            if (next == previous)
            {
                return;
            }

            if (LengthUnits.TryParse(TxtDistance.Text, previous, out double feet))
            {
                TxtDistance.Text = LengthUnits.FormatNumber(feet, next);
            }

            Settings.Units = next;
            ApplyUnitLabels();
            ApplyReadouts();
            UpdateDirectionPreview();
        }

        private void OnFlipNorthChanged(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.FlipTrueNorth = ChkFlipNorth.IsChecked == true;
            RequestRefresh();
        }

        private void OnTopMostChanged(object sender, RoutedEventArgs e)
        {
            if (_suspend)
            {
                return;
            }

            Settings.TopMost = ChkTopMost.IsChecked == true;
            TopMost = Settings.TopMost;
        }

        // ---------------------------------------------------------------- drawing

        private void OnDrawClick(object sender, RoutedEventArgs e)
        {
            DrawUsingToggles();
        }

        private void DrawUsingToggles()
        {
            if (!TryReadDistance(out double distance))
            {
                SetStatus("Enter a distance in " + LengthUnits.UnitSuffix(Settings.Units) + ", for example " +
                          string.Format(CultureInfo.InvariantCulture, "{0:0.###}", LengthUnits.QuickValues(Settings.Units)[1]) + ".", true);
                return;
            }

            Settings.Distance = TxtDistance.Text;

            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    SetStatus("Open a document first.", true);
                    return;
                }

                Document document = uiDocument.Document;
                View view = uiDocument.ActiveView;
                if (!ViewChecks.TryValidate(view, out string viewProblem))
                {
                    SetStatus(viewProblem, true);
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(document);
                session.FlipTrueNorth = Settings.FlipTrueNorth;

                FrameBasis frame = session.BuildFrame(Settings.Frame, view);
                ResolvedDirection resolved = DirectionResolver.Resolve(BuildRequest(), frame);
                if (!resolved.IsValid)
                {
                    SetStatus(resolved.Note ?? "Pick a direction first.", true);
                    return;
                }

                XYZ start = EnsureAnchor(uiDocument, session, view);
                XYZ end = Vec.Add(start, Vec.Mul(resolved.Vector, distance));

                if (!Commit(document, session, start, end, view, out string message))
                {
                    SetStatus(message, true);
                    return;
                }

                if (Settings.Chain)
                {
                    session.SetAnchor(end);
                }
                else
                {
                    session.ClearAnchor();
                }

                RefreshCaches(uiDocument, session, view);

                SetStatus(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} drawn: {1} -> {2}  ({3}, heading {4}, bearing {5:0.#} deg, rise {6:+0.#;-0.#;0} deg)",
                        Settings.Kind == SegmentKind.Pipe ? "Pipe" : "Model line",
                        SegmentFactory.DescribePoint(start, Settings.Units),
                        SegmentFactory.DescribePoint(end, Settings.Units),
                        LengthUnits.Format(distance, Settings.Units),
                        resolved.Label,
                        resolved.BearingDegrees,
                        resolved.ElevationDegrees),
                    false);
            }, "Draw segment");
        }

        private XYZ EnsureAnchor(UIDocument uiDocument, DrawSession session, View view)
        {
            if (session.HasAnchor)
            {
                return session.Anchor;
            }

            XYZ anchor = DrawSession.DefaultAnchor(uiDocument.Document, view, uiDocument.Selection.GetElementIds());
            session.SetAnchor(anchor);
            return anchor;
        }

        private bool Commit(Document document, DrawSession session, XYZ start, XYZ end, View view, out string message)
        {
            message = null;
            SegmentResult result = null;

            try
            {
                Tx.Run(document, "Plant Draw - segment", () =>
                {
                    result = SegmentFactory.Create(
                        document,
                        start,
                        end,
                        Settings.Kind,
                        Settings.Plane,
                        session.PlaneCache,
                        Settings.PinViewWorkPlane,
                        view);
                });
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }

            if (result == null)
            {
                message = "Revit did not create any geometry.";
                return false;
            }

            session.RegisterSegment(result);
            return true;
        }

        private void OnPickStartClick(object sender, RoutedEventArgs e)
        {
            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    SetStatus("Open a document first.", true);
                    return;
                }

                if (!ViewChecks.TryValidate(uiDocument.ActiveView, out string viewProblem))
                {
                    SetStatus(viewProblem, true);
                    return;
                }

                try
                {
                    XYZ point = uiDocument.Selection.PickPoint(SnapTypes, "Plant Draw: pick the start point");
                    DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
                    session.SetAnchor(point);
                    RefreshCaches(uiDocument, session, uiDocument.ActiveView);
                    SetStatus("Start point set to " + SegmentFactory.DescribePoint(point, Settings.Units) +
                              " " + LengthUnits.UnitSuffix(Settings.Units), false);
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Pick cancelled.", false);
                }
                catch (Exception exception)
                {
                    SetStatus("Could not pick a point: " + exception.Message, true);
                }
            }, "Pick start point");
        }

        private void OnUseSelectionClick(object sender, RoutedEventArgs e)
        {
            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    SetStatus("Open a document first.", true);
                    return;
                }

                ICollection<ElementId> selection = uiDocument.Selection.GetElementIds();
                XYZ point = DrawSession.AnchorFromSelection(uiDocument.Document, uiDocument.ActiveView, selection);
                if (point == null)
                {
                    SetStatus("Select an element first - the anchor is taken from its location or bounding box.", true);
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
                session.SetAnchor(point);
                RefreshCaches(uiDocument, session, uiDocument.ActiveView);
                SetStatus("Anchor taken from the selection: " + SegmentFactory.DescribePoint(point, Settings.Units), false);
            }, "Anchor from selection");
        }

        private void OnPickEndClick(object sender, RoutedEventArgs e)
        {
            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    SetStatus("Open a document first.", true);
                    return;
                }

                Document document = uiDocument.Document;
                View view = uiDocument.ActiveView;
                if (!ViewChecks.TryValidate(view, out string viewProblem))
                {
                    SetStatus(viewProblem, true);
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(document);
                session.FlipTrueNorth = Settings.FlipTrueNorth;

                XYZ start = EnsureAnchor(uiDocument, session, view);

                XYZ end;
                try
                {
                    end = uiDocument.Selection.PickPoint(SnapTypes, "Plant Draw: pick the end point");
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Pick cancelled.", false);
                    return;
                }

                if (!Commit(document, session, start, end, view, out string message))
                {
                    SetStatus(message, true);
                    return;
                }

                if (Settings.Chain)
                {
                    session.SetAnchor(end);
                }
                else
                {
                    session.ClearAnchor();
                }

                double length = Vec.Distance(start, end);
                RefreshCaches(uiDocument, session, view);
                SetStatus(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Segment by pick: {0} -> {1}  ({2})",
                        SegmentFactory.DescribePoint(start, Settings.Units),
                        SegmentFactory.DescribePoint(end, Settings.Units),
                        LengthUnits.Format(length, Settings.Units)),
                    false);
            }, "Pick end point");
        }

        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
                string message;
                try
                {
                    message = session.UndoLast();
                }
                catch (Exception exception)
                {
                    SetStatus("Undo failed: " + exception.Message, true);
                    return;
                }

                RefreshCaches(uiDocument, session, uiDocument.ActiveView);
                SetStatus(message, false);
            }, "Undo segment");
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            PlantDrawRuntime.Post(application =>
            {
                UIDocument uiDocument = application == null ? null : application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    return;
                }

                DrawSession session = PlantDrawRuntime.GetSession(uiDocument.Document);
                string message;
                try
                {
                    message = session.ClearAll();
                }
                catch (Exception exception)
                {
                    SetStatus("Clear failed: " + exception.Message, true);
                    return;
                }

                RefreshCaches(uiDocument, session, uiDocument.ActiveView);
                SetStatus(message, false);
            }, "Clear segments");
        }

        // ---------------------------------------------------------------- keyboard

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.N:
                    PickDirection(CompassDirection.North);
                    e.Handled = true;
                    break;
                case Key.E:
                    PickDirection(CompassDirection.East);
                    e.Handled = true;
                    break;
                case Key.S:
                    PickDirection(CompassDirection.South);
                    e.Handled = true;
                    break;
                case Key.W:
                    PickDirection(CompassDirection.West);
                    e.Handled = true;
                    break;
                case Key.U:
                    PickDirection(CompassDirection.Up);
                    e.Handled = true;
                    break;
                case Key.D:
                    PickDirection(CompassDirection.Down);
                    e.Handled = true;
                    break;
                case Key.X:
                    ToggleAxis(AxisLock.X);
                    e.Handled = true;
                    break;
                case Key.Y:
                    ToggleAxis(AxisLock.Y);
                    e.Handled = true;
                    break;
                case Key.Z:
                    ToggleAxis(AxisLock.Z);
                    e.Handled = true;
                    break;
                case Key.F8:
                    Settings.Ortho = !Settings.Ortho;
                    WithSuspended(() => ChkOrtho.IsChecked = Settings.Ortho);
                    UpdateDirectionPreview();
                    SetStatus(Settings.Ortho ? "Ortho on." : "Ortho off.", false);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    DrawUsingToggles();
                    e.Handled = true;
                    break;
            }
        }

        private void PickDirection(CompassDirection direction)
        {
            Settings.Direction = direction;
            Settings.BearingOverride = null;
            TxtBearing.Text = string.Empty;
            UpdateDirectionPreview();
        }

        private void ToggleAxis(AxisLock axis)
        {
            Settings.AxisLock = Settings.AxisLock == axis ? AxisLock.None : axis;
            ApplyAxisLockToUi(Settings.AxisLock);
            UpdateDirectionPreview();
        }
    }
}

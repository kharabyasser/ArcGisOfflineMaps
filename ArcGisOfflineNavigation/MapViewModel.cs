using ArcGisMapOfflineRouting;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Navigation;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Device.Location;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ArcGisOfflineNavigation
{
    public class MapViewModel : INotifyPropertyChanged
    {
        //Testing
        int i = 0;

        // Window UI
        private string _heading = "0.0";
        public string HeadingTB
        {
            get
            {
                return _heading;
            }
            set
            {
                _heading = value;
                OnPropertyChanged("HeadingTB");
            }
        }
        private string _speedTB = "0.0";
        public string SpeedTB
        {
            get
            {
                return _speedTB;
            }
            set
            {
                _speedTB = value;
                OnPropertyChanged("SpeedTB");
            }
        }
        private string _distanceTB = "0.0";
        public string DistanceTB
        {
            get
            {
                return _distanceTB;
            }
            set
            {
                _distanceTB = value;
                OnPropertyChanged("DistanceTB");
            }
        }
        private string _distanceLbl = "Kilometers";
        public string DistanceLbl
        {
            get
            {
                return _distanceLbl;
            }
            set
            {
                _distanceLbl = value;
                OnPropertyChanged("DistanceLbl");
            }
        }
        private string _timeTB = "00:00:00";
        public string TimeTB
        {
            get
            {
                return _timeTB;
            }
            set
            {
                _timeTB = value;
                OnPropertyChanged("TimeTB");
            }
        }
        private string _x = "0.0";
        public string X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
                OnPropertyChanged("X");
            }
        }
        private string _y = "0.0";
        public string Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
                OnPropertyChanged("Y");
            }
        }
        public Button AcceptNavigation;
        public Button RefuseNavigation;
        public Button CancelNavigation;
        public System.Windows.Controls.Image DirectionImg;
        public TextBlock DirectionLbl;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        public TextBlock TripRequestTB;

        // Map 
        public Map Map;
        private MapView _mapView;
        public MapView MapView
        {
            get
            {
                return _mapView;
            }

            set
            {
                if (_mapView != value) _mapView = value;
            }
        }
        private double _latitude = 45.475975;
        private double _longitude = -73.601380;
        private double _scale = 180000;
        private GraphicsOverlay _graphicsOverlay;
        private MapPoint _startPoint;
        private MapPoint _middlePoint;
        private MapPoint _endPoint;
        private TransportationNetworkDataset _transportationNetwork;
        private RouteParameters _routeParameters;

        // Variables for navigation
        private Route _route;
        private Route _fakeroute;
        public IReadOnlyList<DirectionManeuver> _directionsList;
        private RouteTracker _tracker;
        private RouteResult _routeResult;
        public bool isNavigating;
        public FakeLocationProvider fakeLocationProvider;
        private RouteTask _solveRouteTask;

        // Traffic
        //...

        // String Array to store the different device location options.
        private string[] _navigationTypes = Enum.GetNames(typeof(LocationDisplayAutoPanMode));

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }

        private void SetGraphicsOverlay()
        {
            if (_mapView != null && _graphicsOverlay == null)
            {
                _graphicsOverlay = new GraphicsOverlay();
                _mapView.GraphicsOverlays.Add(_graphicsOverlay);
            }
        }

        public async Task ConfigureStreetMapPremium()
        {
        // Open the mmpk file
            string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Canada_Quebec.mmpk");

            try
            {
                // Load the package.
                MobileMapPackage streetMapPremiumPackage = await MobileMapPackage.OpenAsync(path);
                // Get the first Map.
                Map caliMap = streetMapPremiumPackage.Maps.First();

                // Load the map; transportation networks aren't available until the map is loaded
                await caliMap.LoadAsync();

                // Get the first network
                _transportationNetwork = caliMap.TransportationNetworks.First();

                // Set the initial viewPoint
                caliMap.InitialViewpoint = new Viewpoint(_latitude, _longitude, _scale);

                // Update the map in use
                this.Map = caliMap;
                _mapView.Map = Map;

                MapView.LocationDisplay.IsEnabled = true;
                MapView.LocationDisplay.LocationChanged += OnLocationChanged;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error opening package");
                return;
            }
        }

        public void InitViewPoint()
        {
            //MapView.SetViewpointCenterAsync(MapView.LocationDisplay.Location.Position);
            if (isNavigating)
                MapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Navigation;
            else
                MapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Recenter;
        }

        public void ClearGraphics()
        {
            SetGraphicsOverlay();
            _graphicsOverlay.Graphics.Clear();
            AcceptNavigation.Visibility = Visibility.Hidden;
            RefuseNavigation.Visibility = Visibility.Hidden;
        }

        public async Task MapClicked(MapPoint location)
        {
            SetStartMarker(location);
        }

        private void SetMapMarker(MapPoint location, SimpleMarkerSymbolStyle pointStyle, Color markerColor, Color markerOutlineColor)
        {
            float markerSize = 8.0f;
            float markerOutlineThickness = 2.0f;
            SimpleMarkerSymbol pointSymbol = new SimpleMarkerSymbol(pointStyle, markerColor, markerSize);
            pointSymbol.Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, markerOutlineColor, markerOutlineThickness);
            Graphic pointGraphic = new Graphic(location, pointSymbol);
            _graphicsOverlay.Graphics.Add(pointGraphic);
        }

        private async void SetStartMarker(MapPoint location)
        {
            SetGraphicsOverlay();
            _graphicsOverlay.Graphics.Clear();
            SetMapMarker(location, SimpleMarkerSymbolStyle.Diamond, Color.FromArgb(226, 119, 40), Color.FromArgb(0, 226, 0));
            _startPoint = location;
            _middlePoint = null;
            _endPoint = null;
            await FindRoute();
        }

        private void SetMiddlePoint(MapPoint location)
        {
            SetMapMarker(location, SimpleMarkerSymbolStyle.Square, Color.FromArgb(40, 119, 226), Color.FromArgb(226, 0, 0));
            _middlePoint = location;
        }

        private async Task SetEndMarker(MapPoint location)
        {
            SetMapMarker(location, SimpleMarkerSymbolStyle.Square, Color.FromArgb(40, 119, 226), Color.FromArgb(226, 0, 0));
            _endPoint = location;
        }

        private async Task FindRoute()
        {

            // Manage Buttons 
            AcceptNavigation.Visibility = Visibility.Visible;
            AcceptNavigation.IsEnabled = true;
            RefuseNavigation.Visibility = Visibility.Visible;
            RefuseNavigation.IsEnabled = true;
            CancelNavigation.Visibility = Visibility.Hidden;
            CancelNavigation.IsEnabled = false;
            TripRequestTB.Visibility = Visibility.Visible;

            try
            {
                _solveRouteTask = await RouteTask.CreateAsync(_transportationNetwork);
                _routeParameters = await _solveRouteTask.CreateDefaultParametersAsync();
                var truckMode = from travelmode in _solveRouteTask.RouteTaskInfo.TravelModes
                                where travelmode.Type == "TRUCK"
                                select travelmode;
                _routeParameters.TravelMode = truckMode.Last();
                _routeParameters.ReturnDirections = true;
                _routeParameters.ReturnStops = true;
                _routeParameters.ReturnRoutes = true;
                _routeParameters.OutputSpatialReference = SpatialReferences.Wgs84;

                List<Stop> stops = new List<Stop> {new Stop(_mapView.LocationDisplay.MapLocation),
                                                   new Stop(_startPoint)};
                _routeParameters.SetStops(stops);

                _routeResult = await _solveRouteTask.SolveRouteAsync(_routeParameters);
                _route = _routeResult.Routes.FirstOrDefault();

                // Display UI info
                TimeTB = _route.TotalTime.ToString(@"hh\:mm\:ss");
                DistanceTB = Math.Round(_route.TotalLength / 1000, 2).ToString();

                // Diplay the route 
                Polyline routePolyline = _route.RouteGeometry;
                SimpleLineSymbol routeSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.BlueViolet, 4.0f);
                Graphic routeGraphic = new Graphic(routePolyline, routeSymbol);
                _graphicsOverlay.Graphics.Add(routeGraphic);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            setExtent(_mapView.LocationDisplay.MapLocation, _startPoint);
            //StartNavigation();
        }

        private async Task setExtent(MapPoint point1, MapPoint point2)
        {
            SpatialReference mySpatialReference = Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84;
            Esri.ArcGISRuntime.Geometry.Envelope myEnvelope = new Esri.ArcGISRuntime.Geometry.Envelope(point1.X, point1.Y, point2.X, point2.Y, mySpatialReference);
            MapPoint point = new MapPoint((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
            await MapView.SetViewpointCenterAsync(point);
            AcceptNavigation.Visibility = Visibility.Visible;
            RefuseNavigation.Visibility = Visibility.Visible;
        }

        public async Task StartNavigation()
        {
            //Set navigation mode to true
            isNavigating = true;

            // Manage UI 
            AcceptNavigation.Visibility = Visibility.Hidden;
            AcceptNavigation.IsEnabled = false;
            RefuseNavigation.Visibility = Visibility.Hidden;
            RefuseNavigation.IsEnabled = false;
            CancelNavigation.Visibility = Visibility.Visible;
            CancelNavigation.IsEnabled = true;
            TripRequestTB.Visibility = Visibility.Hidden;

            // Get the directions for the route.
            _directionsList = _route.DirectionManeuvers;

            // Create route tracker
            _tracker = new RouteTracker(_routeResult, 0);

            // Handle route tracking status changes
            _tracker.TrackingStatusChanged += TrackingStatusUpdated;

            // Check if this route task supports rerouting
            if (_solveRouteTask.RouteTaskInfo.SupportsRerouting)
            {
                // Enable automatic re-routing
                await _tracker.EnableReroutingAsync(_solveRouteTask, _routeParameters, ReroutingStrategy.ToNextWaypoint, false);

                // Handle re-routing completion to display updated route graphic and report new status
                _tracker.RerouteStarted += RerouteStarted;
                _tracker.RerouteCompleted += RerouteCompleted;
            }

            // Turn on navigation mode for the map view
            MapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Navigation;

            // Add a data source for the location display
            // Use Fake-Location
            //Fake Rerouting.
            //if (i == 0)
            //{
            //    _fakeroute = _route;
            //    fakeLocationProvider = new FakeLocationProvider(this, _route.RouteGeometry);
            //}
            //else if (i == 1)
            //{
            //    fakeLocationProvider = new FakeLocationProvider(this, _fakeroute.RouteGeometry);
            //}
            //i++;

            fakeLocationProvider = new FakeLocationProvider(this, _route.RouteGeometry);
            MapView.LocationDisplay.DataSource = new RouteTrackerDisplayLocationDataSource(fakeLocationProvider, _tracker);

            // Use Real-location
            //MapView.LocationDisplay.DataSource = new RouteTrackerDisplayLocationDataSource(new SystemLocationDataSource(), _tracker);

            // Enable location display (this will start the location data source).
            MapView.LocationDisplay.IsEnabled = true;
        }

        private void RerouteStarted(object sender, EventArgs e)
        {
            // Remove the event listeners for tracking status changes while the route tracker recalculates
            // _tracker.NewVoiceGuidance -= SpeakDirection;
            _tracker.TrackingStatusChanged -= TrackingStatusUpdated;
        }

        private void RerouteCompleted(object sender, RouteTrackerRerouteCompletedEventArgs e)
        {
            // Get the new directions
            _directionsList = e.TrackingStatus.RouteResult.Routes[0].DirectionManeuvers;

            // Re-add the event listener for tracking status changes
            // _tracker.NewVoiceGuidance += SpeakDirection
            _tracker.TrackingStatusChanged += TrackingStatusUpdated;


            // Update UI
            TimeTB = _route.TotalTime.ToString(@"hh\:mm\:ss");
            DistanceTB = Math.Round(_route.TotalLength / 1000, 2).ToString();

            // Display new route
            Polyline routePolyline = _route.RouteGeometry;
            SimpleLineSymbol routeSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.BlueViolet, 4.0f);
            Graphic routeGraphic = new Graphic(routePolyline, routeSymbol);
            _graphicsOverlay.Graphics.Add(routeGraphic);
        }

        public void StopNavigation()
        {
            isNavigating = false;
            ClearGraphics();
            CancelNavigation.IsEnabled = false;
            CancelNavigation.Visibility = Visibility.Hidden;
            fakeLocationProvider.StopAsync();
            _directionsList = null;
            _route = null;
            _routeResult = null;
            _tracker.TrackingStatusChanged -= TrackingStatusUpdated;
            DirectionImg.Source = null;
            DistanceTB = "0.0";
            DirectionLbl.Text = "";
            TimeTB = "00:00:00";
        }

        private void SetDirectionIcon(string directiontype)
        {
            if (directiontype != "Depart" && directiontype != "Stop")
            {
                BitmapImage BitImg = new BitmapImage();

                if (directiontype.Contains("Left"))
                {
                    BitImg.BeginInit();
                    BitImg.UriSource = new Uri(
            "C:\\Users\\Yasser\\source\\repos\\ArcGisOfflineNavigation\\ArcGisOfflineNavigation\\Icons\\left.png");
                    BitImg.EndInit();
                }
                else if (directiontype.Contains("Right"))
                {
                    BitImg.BeginInit();
                    BitImg.UriSource = new Uri(
            "C:\\Users\\Yasser\\source\\repos\\ArcGisOfflineNavigation\\ArcGisOfflineNavigation\\Icons\\Right.png");
                    BitImg.EndInit();
                }
                else if (directiontype.Contains("Straight"))
                {
                    BitImg.BeginInit();
                    BitImg.UriSource = new Uri(
            "C:\\Users\\Yasser\\source\\repos\\ArcGisOfflineNavigation\\ArcGisOfflineNavigation\\Icons\\up.png");
                    BitImg.EndInit();
                }

                this.DirectionImg.Source = BitImg;
            }
        }

        private void OnLocationChanged(object sender, Location e)
        {
            HeadingTB = e.Course.ToString();
            SpeedTB = Math.Round(e.Velocity, 2).ToString();
            X = e.Position.X.ToString();
            Y = e.Position.Y.ToString();
        }

        private void TrackingStatusUpdated(object sender, RouteTrackerTrackingStatusChangedEventArgs e)
        {
            TrackingStatus status = e.TrackingStatus;

            if (status.DestinationStatus == DestinationStatus.NotReached || status.DestinationStatus == DestinationStatus.Approaching)
            {
                DistanceTB = status.RouteProgress.RemainingDistance.DisplayText;
                DistanceLbl = status.RouteProgress.RemainingDistance.DisplayTextUnits.PluralDisplayName;
                TimeTB = status.RouteProgress.RemainingTime.ToString(@"hh\:mm\:ss");
            }
            else if (status.DestinationStatus == DestinationStatus.Reached)
            {
                this._dispatcher.Invoke(() =>
                {
                    isNavigating = false;
                    ClearGraphics();
                });
            }
            if (_directionsList != null && status.CurrentManeuverIndex + 1 < _directionsList.Count)
            {
                DirectionManeuver dm = _directionsList[status.CurrentManeuverIndex + 1];

                if (status.CurrentManeuverIndex + 1 < _directionsList.Count)
                {
                    this._dispatcher.Invoke(() =>
                    {
                        DirectionLbl.Text = dm.DirectionText;
                        SetDirectionIcon(dm.ManeuverType.ToString());
                    });
                }
            }
        }
    }

    public class RouteTrackerDisplayLocationDataSource : LocationDataSource
    {
        private LocationDataSource _inputDataSource;
        private RouteTracker _routeTracker;

        public RouteTrackerDisplayLocationDataSource(LocationDataSource dataSource, RouteTracker routeTracker)
        {
            // set the data source 
            _inputDataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

            // set the route tracker
            _routeTracker = routeTracker ?? throw new ArgumentNullException(nameof(RouteTracker));

            // Change the tracker location when the source location changes
            _inputDataSource.LocationChanged += InputLocationChanged;

            // Update the location output when the tracker location updates
            _routeTracker.TrackingStatusChanged += TrackingStatusChanged;
        }

        private void InputLocationChanged(object sender, Location e)
        {
            // Update the tracker location with the new location from the source (Simulation or GPS)
            _routeTracker.TrackLocationAsync(e);
        }

        private void TrackingStatusChanged(object sender, RouteTrackerTrackingStatusChangedEventArgs e)
        {
            // Check if the tracking Status has a location 
            if (e.TrackingStatus.DisplayLocation != null)
            {
                // Call the base method for locationDataSource to update the location with tracker (snapped to route) location
                UpdateLocation(e.TrackingStatus.DisplayLocation);
            }
        }
        protected override Task OnStartAsync() => _inputDataSource.StartAsync();

        protected override Task OnStopAsync() => _inputDataSource.StartAsync();
    }
}

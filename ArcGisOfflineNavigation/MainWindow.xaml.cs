using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ArcGisOfflineNavigation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Task completion source to track a login attempt.
        private TaskCompletionSource<Credential> _loginTaskCompletionSource;

        //Traffic Server
        private string trafficLayerURL = "https://traffic.arcgis.com/arcgis/rest/services/World/Traffic/MapServer";

        private MapViewModel _mapViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _mapViewModel = new MapViewModel();
            _mapViewModel.MapView = EsriMapView;
            _mapViewModel.AcceptNavigation = AcceptNavBtn;
            _mapViewModel.RefuseNavigation = RefuseNavBtn;
            _mapViewModel.CancelNavigation = CancelNavBtn;
            _mapViewModel.DirectionImg = DirectionImg;
            _mapViewModel.DirectionLbl = DirectionLbl;
            _mapViewModel.TripRequestTB = TripRequestTB;

            DataContext = _mapViewModel;

            // Registering the app
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(Challange);

            // Set up streetmap data once the WPF window has loaded
            this.Loaded += async (o, e) => await _mapViewModel.ConfigureStreetMapPremium();

            //Set the traffic
            ArcGISMapImageLayer traffic = new ArcGISMapImageLayer(new Uri(trafficLayerURL));
            _mapViewModel.Map.OperationalLayers.Add(traffic);

            EsriMapView.GeoViewTapped += EsriMapView_GeoViewTapped;
        }

        private async void EsriMapView_GeoViewTapped(object sender, Esri.ArcGISRuntime.UI.Controls.GeoViewInputEventArgs geoViewInputEvent)
        {
            await _mapViewModel.MapClicked(geoViewInputEvent.Location);
        }

        private void AcceptNavBtn_Click(object sender, RoutedEventArgs e)
        {
            _mapViewModel.StartNavigation();
        }

        private void RefuseNavBtn_Click(object sender, RoutedEventArgs e)
        {
            _mapViewModel.InitViewPoint();
            _mapViewModel.ClearGraphics();
        }

        private void LocateBtn_Click(object sender, RoutedEventArgs e)
        {
            _mapViewModel.InitViewPoint();
        }

        private void CancelNavBtn_Click(object sender, RoutedEventArgs e)
        {
            _mapViewModel.StopNavigation();
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            var scale = _mapViewModel.MapView.MapScale - 3 * 10000;
            var viewPoint = _mapViewModel.MapView.GetCurrentViewpoint(ViewpointType.CenterAndScale);
            if (scale > 0)
                _mapViewModel.MapView.SetViewpointCenterAsync(_mapViewModel.MapView.LocationDisplay.Location.Position, scale);
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            var scale = _mapViewModel.MapView.MapScale + 3 * 10000;
            var viewPoint = _mapViewModel.MapView.GetCurrentViewpoint(ViewpointType.CenterAndScale);
            if (scale < 10 * 100000)
                _mapViewModel.MapView.SetViewpointCenterAsync(_mapViewModel.MapView.LocationDisplay.Location.Position, scale);
        }

        private async Task<Credential> Challange(CredentialRequestInfo info)
        {
            _loginTaskCompletionSource = new TaskCompletionSource<Credential>();

            // Get the login info from the task completion source.
            var loginEntry = _loginTaskCompletionSource.Task.AsyncState;

            try
            {
                TokenCredential tokenCredentials = await AuthenticationManager.Current.GenerateCredentialAsync(
                                                   new Uri("https://traffic.arcgis.com/arcgis/rest/services/World/Traffic/MapServer"),
                                                   "YOUR USERNAME",
                                                   "YOUR PASSWORD"
                                                   );
                _loginTaskCompletionSource.TrySetResult(tokenCredentials);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return await _loginTaskCompletionSource.Task;
        }
    }
}

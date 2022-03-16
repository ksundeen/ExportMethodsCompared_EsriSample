using Xamarin.Forms;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Colors = System.Drawing.Color;
using System.Diagnostics;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Tasks;

namespace ExportVectorTiles
{
    public partial class MapPage : ContentPage
    {
        // URL to the service tiles will be exported from.
        private static Uri _rasterTileServiceUri = new Uri("https://tiledbasemaps.arcgis.com/arcgis/rest/services/World_Street_Map/MapServer");
        private static Uri _vectorTileServiceUri = new Uri("https://basemaps.arcgis.com/arcgis/rest/services/World_Basemap_Export_v2/VectorTileServer");
        private static string _vectorWebMapId = "55ebf90799fa4a3fa57562700a68c405"; // https://www.arcgis.com/home/item.html?id=55ebf90799fa4a3fa57562700a68c405

        // Path to exported tile caches.
        private string _rasterTilePath;
        private string _vectorTilePath;
        private string _offlineMapPath;
        private string _useExistingOfflineMapPath = "C:\\Users\\kimberly.sundeen\\AppData\\Local\\Packages\\b4686218-b283-4f11-86af-e37d325ad535_eym0c1r99ygm4\\AC\\Temp\\OfflineMap_expanded_0.8_1";

        // Flag to indicate whether an exported map/cache is being previewed.
        private bool _vectorTilesPreviewOpen = false;
        private bool _rasterTilesPreviewOpen = false;
        private bool _offlineMapPreviewOpen = false;
        private bool _useExistingOfflineMapPreviewOpen = false;

        // Percentage increase/decrease of envelope to test sizes
        double _percentageIncrease = 0.80;

        // Max and min levels of download scale details
        int _minLevelOfDetails = 2;
        int _maxLevelOfDetails = 20;
        int _maxLevelScale = 100;

        // Reference to the original basemap.
        private Map _basemap;

        // Reference to the original viewpoint (when previewing).
        private Viewpoint _originalView;

        // Holder for the Graphics Overlay (so that it can be hidden and re-added for preview/non-preview state).
        private GraphicsOverlay _overlay;

        private Envelope _areaOfInterest;

        public MapPage()
        {
            Esri.ArcGISRuntime.ArcGISRuntimeEnvironment.ApiKey = "AAPK7074c02698a04da889a8f1a82cf5db3ewXp6U8-vIef5maXJT8W7AYgw4BGi_KjSdKq2L2f2uR-JFFeH1j0ZMWBg9qmNjsws";
            InitializeComponent();
            InitializeMap();
        }

        private async void InitializeMap()
        {
            // Create the vector tile layer as basemap.
            try
            {
                //ArcGISTiledLayer myLayer = new ArcGISTiledLayer(_vectorTileServiceUri);
                //ArcGISVectorTiledLayer myLayer = new ArcGISVectorTiledLayer(_vectorTileServiceUri);

                //// Load the layer.
                //await myLayer.LoadAsync();

                // Create the basemap with the layer.
                //_basemap = new Map(new Basemap(myLayer));
                _basemap = new Map(BasemapStyle.ArcGISStreets) //Needs an API Key
                {
                    // Set the min and max scale - export task fails if the scale is too big or small.
                    MaxScale = 2000,
                    MinScale = 10000000
                };
                // Assign the map to the mapview.
                MyMapView.Map = _basemap;

                // Create a new symbol for the extent graphic.
                //     This is the red box that visualizes the extent for which tiles will be exported.
                SimpleLineSymbol myExtentSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Colors.Red, 2);

                // Create graphics overlay for the extent graphic and apply a renderer.
                GraphicsOverlay extentOverlay = new GraphicsOverlay
                {
                    Renderer = new SimpleRenderer(myExtentSymbol)
                };

                // Add graphics overlay to the map view.
                MyMapView.GraphicsOverlays.Add(extentOverlay);

                // Subscribe to changes in the mapview's viewpoint so the preview box can be kept in position.
                MyMapView.ViewpointChanged += MyMapView_ViewpointChanged;

                // Update the graphic - needed in case the user decides not to interact before pressing the button.
                UpdateMapExtentGraphic();

                // Enable the export button once the sample is ready.
                RasterTilesCachePreviewButton.IsEnabled = true;
                VectorTilesCachePreviewButton.IsEnabled = true;
                OfflineMapPreviewButton.IsEnabled = true;
                UseExistingOfflineMapPreviewButton.IsEnabled = true;

                // Set viewpoint of the map.
                //MyMapView.SetViewpoint(new Viewpoint(-4.853791, 140.983598, _basemap.MinScale));
                //Envelope envelope = new Envelope(-13046715.275838645, 4036267.505171629, -13045835.055684676, 4036788.2324268934, SpatialReferences.WebMercator);
                //_areaOfInterest = envelope;

                EnvelopeBuilder envelopeBldr = new EnvelopeBuilder(SpatialReferences.Wgs84)
                {
                    XMin = -88.1526,
                    XMax = -88.1490,
                    YMin = 41.7694,
                    YMax = 41.7714
                };
                _areaOfInterest = envelopeBldr.ToGeometry();


                MyMapView.SetViewpoint(new Viewpoint(_areaOfInterest));

            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Initialization", ex.ToString(), "OK");
            }
        }

        private void MyMapView_ViewpointChanged(object sender, EventArgs e)
        {
            UpdateMapExtentGraphic();
        }

        /// <summary>
        /// Function used to keep the overlaid preview area marker in position
        /// This is called by MyMapView_ViewpointChanged every time the user pans/zooms
        ///     and updates the red box graphic to outline 80% of the current view
        /// </summary>
        private void UpdateMapExtentGraphic()
        {
            // Return if mapview is null.
            if (MyMapView == null) { return; }

            // Get the new viewpoint.
            Viewpoint myViewPoint = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

            // Return if viewpoint is null.
            if (myViewPoint == null) { return; }

            // Get the updated extent for the new viewpoint.
            Envelope extent = myViewPoint.TargetGeometry as Envelope;

            // Return if extent is null.
            if (extent == null) { return; }

            // Create an envelope that is a bit smaller than the extent.
            EnvelopeBuilder envelopeBldr = new EnvelopeBuilder(extent);
            envelopeBldr.Expand(_percentageIncrease);

            // Get the (only) graphics overlay in the map view.
            GraphicsOverlay extentOverlay = MyMapView.GraphicsOverlays.FirstOrDefault();

            // Return if there is none (in preview, the overlay shouldn't be there).
            if (extentOverlay == null) { return; }

            // Get the extent graphic.
            Graphic extentGraphic = extentOverlay.Graphics.FirstOrDefault();

            // Create the extent graphic and add it to the overlay if it doesn't exist.
            if (extentGraphic == null)
            {
                extentGraphic = new Graphic(envelopeBldr.ToGeometry());
                extentOverlay.Graphics.Add(extentGraphic);
            }
            else
            {
                // Otherwise, update the graphic's geometry.
                extentGraphic.Geometry = envelopeBldr.ToGeometry();
            }
        }
        private async Task StartRasterTilesExport()
        {
            try
            {
                ArcGISTiledLayer imageLayer = new ArcGISTiledLayer(_rasterTileServiceUri);
                MyMapView.Map.Basemap.BaseLayers.Add(imageLayer);
                await Application.Current.MainPage.DisplayAlert("Status", "Raster download started...", "OK");


                // Update the tile cache path.
                string packagePath = CreateDownloadPackagePath(DownloadMapType.RasterTile);
                _rasterTilePath = Path.Combine(packagePath, "RasterTiles.tpk");
                Debug.WriteLine("Export located: " + _rasterTilePath);

                // Get the parameters for the job.
                ExportTileCacheParameters exportTileCacheParameters = GetExportTileCacheParameters();

                // Create the task.
                ExportTileCacheTask exportTileCacheTask = await ExportTileCacheTask.CreateAsync(_rasterTileServiceUri);

                // Create the export job.
                ExportTileCacheJob exportTileCacheJob = exportTileCacheTask.ExportTileCache(exportTileCacheParameters, _rasterTilePath);

                // Show the progress bar.
                RasterTilesProgressBar.IsVisible = true;

                // Start the export job.
                exportTileCacheJob.Start();

                // Get the tile cache result.
                TileCache resultTileCache = await exportTileCacheJob.GetResultAsync();

                // Hide the progress bar.
                RasterTilesProgressBar.IsVisible = false;

                // Do the rest of the work.
                await HandleExportRasterTilesJobCompletion(exportTileCacheJob, resultTileCache);
                await Application.Current.MainPage.DisplayAlert("Export Complete", $"See raster tiles downloaded package in {_rasterTilePath}", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Export Raster Tile Package Task", ex.ToString(), "OK");
            }
        }

        private async Task StartVectorTilesExport()
        {
            try
            {
                // Update the tile cache path.
                //_vectorTilePath = $"{Path.GetTempFileName()}.vtpk";
                string packagePath = CreateDownloadPackagePath(DownloadMapType.VectorTile);
                _vectorTilePath = Path.Combine(packagePath, "VectorTiles.vtpk");
                Debug.WriteLine("Export located: " + _vectorTilePath);

                // Get the parameters for the job.
                ExportVectorTilesParameters exportVectorTilesParameters = GetExportVectorTilesParameters();

                // Create the task.
                ExportVectorTilesTask exportVectorTilesTask = await ExportVectorTilesTask.CreateAsync(_vectorTileServiceUri);

                // Create the export job.
                ExportVectorTilesJob exportVectorTilesJob = exportVectorTilesTask.ExportVectorTiles(exportVectorTilesParameters, _vectorTilePath);

                // Show the progress bar.
                VectorTilesProgressBar.IsVisible = true;

                // Start the export job.
                exportVectorTilesJob.Start();

                // Get the tile cache result.
                ExportVectorTilesResult exportVectorTilesResult = await exportVectorTilesJob.GetResultAsync();

                // Hide the progress bar.
                VectorTilesProgressBar.IsVisible = false;

                // Do the rest of the work.
                await HandleExportVectorTilesJobCompletion(exportVectorTilesJob, exportVectorTilesResult);
                await Application.Current.MainPage.DisplayAlert("Export Complete", $"See vector tiles downloaded package in {_vectorTilePath}", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Export Vector Tiles Task", ex.ToString(), "OK");
            }
        }

        private ExportTileCacheParameters GetExportTileCacheParameters()
        {
            // Create a new parameters instance.
            ExportTileCacheParameters parameters = new ExportTileCacheParameters();

            // Get the (only) graphics overlay in the map view.
            GraphicsOverlay extentOverlay = MyMapView.GraphicsOverlays.First();

            // Get the area selection graphic's extent.
            Graphic extentGraphic = extentOverlay.Graphics.First();

            parameters.AreaOfInterest = extentGraphic.Geometry;

            // Set the area for the export.
            parameters.CompressionQuality = 100;

            // Re-add selected scales.
            for (int i = _minLevelOfDetails; i < _maxLevelOfDetails; i++)
            {
                parameters.LevelIds.Add(i);
            }

            // Return the parameters.
            return parameters;
        }
        private ExportVectorTilesParameters GetExportVectorTilesParameters()
        {
            // Create a new parameters instance.
            ExportVectorTilesParameters parameters = new ExportVectorTilesParameters();

            // Get the (only) graphics overlay in the map view.
            GraphicsOverlay extentOverlay = MyMapView.GraphicsOverlays.First();

            // Get the area selection graphic's extent.
            Graphic extentGraphic = extentOverlay.Graphics.First();

            // Set the area for the export.
            parameters.AreaOfInterest = extentGraphic.Geometry;
            parameters.MaxLevel = _maxLevelScale; // 100;

            // Return the parameters.
            return parameters;
        }
        public async Task StartOfflineMapExport()
        {
            try
            {
                ArcGISPortal Portal = await ArcGISPortal.CreateAsync();

                // Get a web map item using its ID.
                PortalItem webmapItem = await PortalItem.CreateAsync(Portal, _vectorWebMapId);

                // Create a map from the web map item & set current map
                //Map onlineMap = new Map(webmapItem);
                //MyMapView.Map = onlineMap;

                await Application.Current.MainPage.DisplayAlert("Offline Map", "Download started...", "OK");

                // Create a new folder for the output mobile map.
                _offlineMapPath = CreateDownloadPackagePath(DownloadMapType.OfflineMap);
                Debug.WriteLine("Export located: " + _offlineMapPath);

                try
                {
                    // Create an offline map task with the current (online) map.
                    OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(webmapItem);

                    GenerateOfflineMapParameters parameters = await takeMapOfflineTask.CreateDefaultGenerateOfflineMapParametersAsync(_areaOfInterest);
                    parameters.EsriVectorTilesDownloadOption = EsriVectorTilesDownloadOption.UseReducedFontsService;
                    parameters.MaxScale = 500;
                    parameters.MinScale = 10000000;
                    parameters.IncludeBasemap = true;
                    parameters.AreaOfInterest = _areaOfInterest;
                    //parameters.ReferenceBasemapDirectory = _offlineMapPath;
                    //parameters.ReferenceBasemapFilename = 
                    /*************************/
                    /*************************/
                    /********** TODO *********/
                    // Look into how this directory is set on iOS and Android.
                    // Can we used this to set the font directory?
                    //parameters.ReferenceBasemapDirectory = Set location of existing data on device
                    //parameters.ReferenceBasemapFilename = Set filename of basemap to use on device
                    /*************************/
                    /*************************/

                    // Check offline capabilities
                    CheckOfflineCapabilities(takeMapOfflineTask, parameters);
                    #region overrides

                    // Generate parameter overrides for more in-depth control of the job.
                    GenerateOfflineMapParameterOverrides overrides = await takeMapOfflineTask.CreateGenerateOfflineMapParameterOverridesAsync(parameters);

                    // Configure the overrides using helper methods.
                    ConfigureOfflineTileLayerOverrides(overrides);

                    // Create the job with the parameters and output location.
                    GenerateOfflineMapJob generateOfflineMapJob = takeMapOfflineTask.GenerateOfflineMap(parameters, _offlineMapPath, overrides);

                    #endregion overrides

                    GenerateOfflineMapResult offlineMapResult = await generateOfflineMapJob.GetResultAsync();

                    // Handle the progress changed event for the job.
                    HandleExportOfflineMapJobCompletion(generateOfflineMapJob, offlineMapResult);
                }
                catch (TaskCanceledException)
                {
                    // Generate offline map task was canceled.
                    await Application.Current.MainPage.DisplayAlert("Cancelled", "Taking map offline was canceled", "OK");
                }
                catch (Exception ex)
                {
                    // Exception while taking the map offline.
                    await Application.Current.MainPage.DisplayAlert("Offline Map Error", ex.Message.ToString(), "OK");

                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Offline Map Error", ex.Message.ToString(), "OK");
            }
        }

        public async Task StartUseExistingOfflineMapExport()
        {
            try
            {
                ArcGISPortal Portal = await ArcGISPortal.CreateAsync();

                // Get a web map item using its ID.
                PortalItem webmapItem = await PortalItem.CreateAsync(Portal, _vectorWebMapId);

                // Create a map from the web map item & set current map
                //Map onlineMap = new Map(webmapItem);
                //MyMapView.Map = onlineMap;

                await Application.Current.MainPage.DisplayAlert("Using Existing Offline Map", "Download started...", "OK");

                // Create a new folder for the output mobile map.
                Debug.WriteLine("Export located: " + _offlineMapPath);

                try
                {
                    // Create an offline map task with the current (online) map.
                    OfflineMapTask takeMapOfflineTask = await OfflineMapTask.CreateAsync(webmapItem);

                    GenerateOfflineMapParameters parameters = await takeMapOfflineTask.CreateDefaultGenerateOfflineMapParametersAsync(_areaOfInterest);
                    parameters.EsriVectorTilesDownloadOption = EsriVectorTilesDownloadOption.UseReducedFontsService;
                    parameters.MaxScale = 500;
                    parameters.MinScale = 10000000;
                    parameters.IncludeBasemap = true;
                    parameters.AreaOfInterest = _areaOfInterest;

                    // Configure basemap settings for the job.
                    ConfigureOfflineJobForBasemap(parameters);

                    parameters.ReferenceBasemapDirectory = _useExistingOfflineMapPath;
                    parameters.ReferenceBasemapFilename = "OfflineMap_expanded_0.8_";
                    /*************************/
                    /*************************/
                    /********** TODO *********/
                    // Look into how this directory is set on iOS and Android.
                    // Can we used this to set the font directory?
                    //parameters.ReferenceBasemapDirectory = Set location of existing data on device
                    //parameters.ReferenceBasemapFilename = Set filename of basemap to use on device
                    /*************************/
                    /*************************/

                    // Check offline capabilities
                    CheckOfflineCapabilities(takeMapOfflineTask, parameters);
                    #region overrides

                    // Generate parameter overrides for more in-depth control of the job.
                    GenerateOfflineMapParameterOverrides overrides = await takeMapOfflineTask.CreateGenerateOfflineMapParameterOverridesAsync(parameters);

                    // Configure the overrides using helper methods.
                    ConfigureOfflineTileLayerOverrides(overrides);

                    // Create the job with the parameters and output location.
                    GenerateOfflineMapJob generateOfflineMapJob = takeMapOfflineTask.GenerateOfflineMap(parameters, _offlineMapPath, overrides);

                    #endregion overrides

                    GenerateOfflineMapResult offlineMapResult = await generateOfflineMapJob.GetResultAsync();

                    // Handle the progress changed event for the job.
                    HandleExportUseExistingOfflineMapJobCompletion(generateOfflineMapJob, offlineMapResult);
                }
                catch (TaskCanceledException)
                {
                    // Generate offline map task was canceled.
                    await Application.Current.MainPage.DisplayAlert("Cancelled", "Taking map offline was canceled", "OK");
                }
                catch (Exception ex)
                {
                    // Exception while taking the map offline.
                    await Application.Current.MainPage.DisplayAlert("Offline Map Error", ex.Message.ToString(), "OK");

                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Offline Map Error", ex.Message.ToString(), "OK");
            }
        }

        private void ConfigureOfflineJobForBasemap(GenerateOfflineMapParameters parameters)
        {
            // Get the path to the basemap directory.
            string basemapBasePath = GetDataFolder();

            // Don't give the user a choice if there is no basemap specified.
            if (String.IsNullOrWhiteSpace(parameters.ReferenceBasemapFilename))
            {
                return;
            }
            // Get the full path to the basemap by combining the name specified in the web map (ReferenceBasemapFilename)
            //  with the offline basemap directory.
            string basemapFullPath = Path.Combine(basemapBasePath, parameters.ReferenceBasemapFilename);


            // If the offline basemap doesn't exist, proceed without it.
            if (!File.Exists(basemapFullPath))
            {
                return;
            }

            // Configure the offline basemap if the user said yes.
            parameters.ReferenceBasemapDirectory = basemapBasePath;
        }

        private void ConfigureOfflineTileLayerOverrides(GenerateOfflineMapParameterOverrides overrides)
        {
            // Create a parameter key for the first basemap layer. Type is Layer (can be FeatureLayer, ArcGISTiledLayer, or ArcGISVectorTiledLayer
            ArcGISVectorTiledLayer vectorLayer = new ArcGISVectorTiledLayer(_vectorTileServiceUri);
            ArcGISTiledLayer rasterLayer = new ArcGISTiledLayer(_rasterTileServiceUri);
            // Add basemap to layers
            MyMapView.Map.Basemap.BaseLayers.Add(vectorLayer);
            MyMapView.Map.Basemap.BaseLayers.Add(rasterLayer);

            OfflineMapParametersKey basemapTileCacheKey = new OfflineMapParametersKey(MyMapView.Map.Basemap.BaseLayers.ElementAt(1));
            ExportTileCacheParameters basemapTileCacheParameters = new ExportTileCacheParameters();
            // Get the export tile cache parameters for the layer key.
            //ExportTileCacheParameters basemapTileCacheParams = overrides.ExportTileCacheParameters[basemapTileCacheKey];

            // Set the highest possible export quality.
            basemapTileCacheParameters.CompressionQuality = 100;

            // Clear the existing level IDs.
            basemapTileCacheParameters.LevelIds.Clear();

            // Re-add selected scales.
            for (int i = _minLevelOfDetails; i < _maxLevelOfDetails; i++)
            {
                basemapTileCacheParameters.LevelIds.Add(i);
            }

            // Add new overides associated with tile cache basemaps
            overrides.ExportTileCacheParameters.Add(basemapTileCacheKey, basemapTileCacheParameters);

            // Configure VectorTile overrides
            OfflineMapParametersKey basemapVectorTileKey = new OfflineMapParametersKey(MyMapView.Map.Basemap.BaseLayers.ElementAt(0));
            //ExportVectorTilesParameters basemapVectorTileParameters = overrides.ExportVectorTilesParameters[basemapTileCacheKey];
            ExportVectorTilesParameters basemapVectorTileParameters = new ExportVectorTilesParameters();

            basemapVectorTileParameters.MaxLevel = _maxLevelOfDetails;

            // Expand the area of interest based on the specified buffer distance.
            basemapVectorTileParameters.AreaOfInterest = _areaOfInterest;
        }

        private async void CheckOfflineCapabilities(OfflineMapTask task, GenerateOfflineMapParameters parameters)
        {
            OfflineMapCapabilities results = await task.GetOfflineMapCapabilitiesAsync(parameters);
            if (results.HasErrors)
            {
                // Handle possible errors with layers
                foreach (var layerCapability in results.LayerCapabilities)
                {
                    if (!layerCapability.Value.SupportsOffline)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Offline Map Error", 
                            layerCapability.Key.Name + " cannot be taken offline. Error : " + layerCapability.Value.Error.Message, 
                            "OK");
                    }
                }

                // Handle possible errors with tables
                foreach (var tableCapability in results.TableCapabilities)
                {
                    if (!tableCapability.Value.SupportsOffline)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Offline Map Table Error Error",
                            tableCapability.Key.TableName + " cannot be taken offline. Error : " + tableCapability.Value.Error.Message,
                            "OK");
                    }
                }
            }
            else
            {
                // All layers and tables can be taken offline!
                        await Application.Current.MainPage.DisplayAlert("Offline Status", "All layers can be exported", "OK");
            }
        }

        private async Task HandleExportVectorTilesJobCompletion(ExportVectorTilesJob job, ExportVectorTilesResult cache)
        {
            // Update the view if the job is complete.
            if (job.Status == JobStatus.Succeeded)
            {
                // Show the exported tiles on the preview map.
                await UpdateVectorTilePreviewMap(cache);

                // Change the export button text.
                VectorTilesCachePreviewButton.Text = "Close Vector Tile Preview";

                // Re-enable the button.
                VectorTilesCachePreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _vectorTilesPreviewOpen = true;

                // Store the overlay for later.
                _overlay = MyMapView.GraphicsOverlays.FirstOrDefault();

                // Then hide it.
                MyMapView.GraphicsOverlays.Clear();
            }
            else if (job.Status == JobStatus.Failed)
            {
                // Notify the user.
                await Application.Current.MainPage.DisplayAlert("Error Job", "Vector Tile Job Failed", "OK");

                // Change the export button text.
                VectorTilesCachePreviewButton.Text = "Export Vector Tiles";

                // Re-enable the export button.
                VectorTilesCachePreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _vectorTilesPreviewOpen = false;
            }
        }

        private async Task HandleExportRasterTilesJobCompletion(ExportTileCacheJob job, TileCache cache)
        {
            // Update the view if the job is complete.
            if (job.Status == JobStatus.Succeeded)
            {
                // Show the exported tiles on the preview map.
                await UpdateRasterTileCachePreviewMap(cache);

                // Change the export button text.
                RasterTilesCachePreviewButton.Text = "Close Raster Tile Preview";

                // Re-enable the button.
                RasterTilesCachePreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _rasterTilesPreviewOpen = true;

                // Store the overlay for later.
                _overlay = MyMapView.GraphicsOverlays.FirstOrDefault();

                // Then hide it.
                MyMapView.GraphicsOverlays.Clear();
            }
            else if (job.Status == JobStatus.Failed)
            {
                // Notify the user.
                await Application.Current.MainPage.DisplayAlert("Error Job", "Raster Tile Job Failed", "OK");

                // Change the export button text.
                RasterTilesCachePreviewButton.Text = "Export Raster Tiles";

                // Re-enable the export button.
                RasterTilesCachePreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _rasterTilesPreviewOpen = false;
            }
        }

        private async void HandleExportOfflineMapJobCompletion(GenerateOfflineMapJob job, GenerateOfflineMapResult mapResult)
        {
            // Update the view if the job is complete.
            if (job.Status == JobStatus.Succeeded)
            {
                // Show the exported tiles on the preview map.
                UpdateOfflineMapPreviewMap(mapResult);

                // Change the export button text.
                OfflineMapPreviewButton.Text = "Close Offline Map Preview";

                // Re-enable the button.
                OfflineMapPreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _offlineMapPreviewOpen = true;

                // Store the overlay for later.
                _overlay = MyMapView.GraphicsOverlays.FirstOrDefault();

                // Then hide it.
                MyMapView.GraphicsOverlays.Clear();
            }
            else if (job.Status == JobStatus.Failed)
            {
                // Notify the user.
                await Application.Current.MainPage.DisplayAlert("Error Job", "Offline Map Job Failed", "OK");

                // Change the export button text.
                OfflineMapPreviewButton.Text = "Export Offline Map Tiles";

                // Re-enable the export button.
                OfflineMapPreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _offlineMapPreviewOpen = false;
            }
        }

        private async void HandleExportUseExistingOfflineMapJobCompletion(GenerateOfflineMapJob job, GenerateOfflineMapResult mapResult)
        {
            // Update the view if the job is complete.
            if (job.Status == JobStatus.Succeeded)
            {
                // Show the exported tiles on the preview map.
                UpdateOfflineMapPreviewMap(mapResult);

                // Change the export button text.
                OfflineMapPreviewButton.Text = "Close Offline Map Preview";

                // Re-enable the button.
                OfflineMapPreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _offlineMapPreviewOpen = true;

                // Store the overlay for later.
                _overlay = MyMapView.GraphicsOverlays.FirstOrDefault();

                // Then hide it.
                MyMapView.GraphicsOverlays.Clear();
            }
            else if (job.Status == JobStatus.Failed)
            {
                // Notify the user.
                await Application.Current.MainPage.DisplayAlert("Error Job", "Offline Map Job Failed", "OK");

                // Change the export button text.
                OfflineMapPreviewButton.Text = "Export Offline Map Tiles";

                // Re-enable the export button.
                OfflineMapPreviewButton.IsEnabled = true;

                // Set the preview open flag.
                _offlineMapPreviewOpen = false;
            }
        }

        private async Task UpdateRasterTileCachePreviewMap(TileCache cache)
        {
            // Load the cache.
            await cache.LoadAsync();

            // Create a tile layer with the cache.
            //ArcGISTiledLayer myLayer = new ArcGISTiledLayer(cache);
            ArcGISTiledLayer myLayer = new ArcGISTiledLayer(cache);

            // Show the layer in a new map.
            MyMapView.Map = new Map(new Basemap(myLayer));

            // Re-size the mapview.
            MyMapView.Margin = new Thickness(40);
        }
        private async Task UpdateVectorTilePreviewMap(ExportVectorTilesResult cache)
        {
            // Load the cache.
            await cache.VectorTileCache.LoadAsync();

            // Create a tile layer with the cache.
            ArcGISVectorTiledLayer myLayer = new ArcGISVectorTiledLayer(cache.VectorTileCache);

            // Show the layer in a new map.
            MyMapView.Map = new Map(new Basemap(myLayer));

            // Re-size the mapview.
            MyMapView.Margin = new Thickness(40);
        }
        private void UpdateOfflineMapPreviewMap(GenerateOfflineMapResult mapResult)
        {
            // Load the offline map
            MyMapView.Map = mapResult.OfflineMap;

            // Re-size the mapview.
            MyMapView.Margin = new Thickness(40);
        }
        private async void VectorTilesCachePreviewButton_Clicked(object sender, EventArgs e)
        {
            // If preview isn't open, start an export.
            try
            {
                if (!_vectorTilesPreviewOpen)
                {
                    // Disable the export button.
                    VectorTilesCachePreviewButton.IsEnabled = false;

                    // Show the progress bar.
                    VectorTilesProgressBar.IsVisible = true;

                    // Save the map viewpoint.
                    _originalView = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                    // Start the exports.
                    await StartVectorTilesExport();
                }
                else // Otherwise, close the preview.
                {
                    // Change the button text.
                    VectorTilesCachePreviewButton.Text = "Export Vector Tiles";

                    // Clear the preview open flag.
                    _vectorTilesPreviewOpen = false;

                    // Re-size the mapview.
                    MyMapView.Margin = new Thickness(0);

                    // Re-apply the original map.
                    MyMapView.Map = _basemap;

                    // Re-apply the original viewpoint.
                    MyMapView.SetViewpoint(_originalView);

                    // Re-show the overlay.
                    MyMapView.GraphicsOverlays.Add(_overlay);

                    // Update the graphic.
                    UpdateMapExtentGraphic();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Preview", ex.ToString(), "OK");
            }
        }

        private async void RasterTilesCachePreviewButton_Clicked(object sender, EventArgs e)
        {
            // If preview isn't open, start an export.
            try
            {
                if (!_rasterTilesPreviewOpen)
                {
                    // Disable the export button.
                    RasterTilesCachePreviewButton.IsEnabled = false;

                    // Show the progress bar.
                    RasterTilesProgressBar.IsVisible = true;

                    // Save the map viewpoint.
                    _originalView = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                    // Start the exports.
                    await StartRasterTilesExport();
                }
                else // Otherwise, close the preview.
                {
                    // Change the button text.
                    RasterTilesCachePreviewButton.Text = "Export Raster Tiles";

                    // Clear the preview open flag.
                    _rasterTilesPreviewOpen = false;

                    // Re-size the mapview.
                    MyMapView.Margin = new Thickness(0);

                    // Re-apply the original map.
                    MyMapView.Map = _basemap;

                    // Re-apply the original viewpoint.
                    MyMapView.SetViewpoint(_originalView);

                    // Re-show the overlay.
                    MyMapView.GraphicsOverlays.Add(_overlay);

                    // Update the graphic.
                    UpdateMapExtentGraphic();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Preview", ex.ToString(), "OK");
            }
        }

        private async void OfflineMapPreviewButton_Clicked(object sender, EventArgs e)
        {
            // If preview isn't open, start an export.
            try
            {
                if (!_offlineMapPreviewOpen)
                {
                    // Disable the export button.
                    OfflineMapPreviewButton.IsEnabled = false;

                    // Show the progress bar.
                    OfflineMapProgressBar.IsVisible = true;

                    // Save the map viewpoint.
                    _originalView = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                    // Start the export.
                    await StartOfflineMapExport();
                }
                else // Otherwise, close the preview.
                {
                    // Change the button text.
                    OfflineMapPreviewButton.Text = "Export Offline Map";

                    // Clear the preview open flag.
                    _offlineMapPreviewOpen = false;

                    // Re-size the mapview.
                    MyMapView.Margin = new Thickness(0);

                    // Re-apply the original map.
                    MyMapView.Map = _basemap;

                    // Re-apply the original viewpoint.
                    MyMapView.SetViewpoint(_originalView);

                    // Re-show the overlay.
                    MyMapView.GraphicsOverlays.Add(_overlay);

                    // Update the graphic.
                    UpdateMapExtentGraphic();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Offline Map Preview", ex.ToString(), "OK");
            }
        }
        private async void UseExistingOfflineMap_Click(object sender, EventArgs e)
        {
            // If preview isn't open, start an export.
            try
            {
                if (!_useExistingOfflineMapPreviewOpen)
                {
                    // Disable the export button.
                    UseExistingOfflineMapPreviewButton.IsEnabled = false;

                    // Show the progress bar.
                    UseExistingOfflineMapProgressBar.IsVisible = true;

                    // Save the map viewpoint.
                    _originalView = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                    // Start the export.
                    await StartUseExistingOfflineMapExport();
                }
                else // Otherwise, close the preview.
                {
                    // Change the button text.
                    UseExistingOfflineMapPreviewButton.Text = "Using Existing Offline Map";

                    // Clear the preview open flag.
                    _useExistingOfflineMapPreviewOpen = false;

                    // Re-size the mapview.
                    MyMapView.Margin = new Thickness(0);

                    // Re-apply the original map.
                    MyMapView.Map = _basemap;

                    // Re-apply the original viewpoint.
                    MyMapView.SetViewpoint(_originalView);

                    // Re-show the overlay.
                    MyMapView.GraphicsOverlays.Add(_overlay);

                    // Update the graphic.
                    UpdateMapExtentGraphic();
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error Offline Map Preview", ex.ToString(), "OK");
                // Change the button text.
                UseExistingOfflineMapPreviewButton.Text = "Using Existing Offline Map";

                // Clear the preview open flag.
                _useExistingOfflineMapPreviewOpen = false;
            }
        }
        public string CreateDownloadPackagePath(DownloadMapType downloadMapType)
        {
            string folderName = $"{downloadMapType.ToString()}_expanded_{_percentageIncrease.ToString()}_";
            // Create a new folder for the output mobile map.
            string packagePath = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), folderName);
            int num = 1;
            while (Directory.Exists(packagePath))
            {
                packagePath = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), folderName + num.ToString());
                num++;
            }

            // Create the output directory.
            Directory.CreateDirectory(packagePath);
            return packagePath;
        }

        public enum DownloadMapType
        {
            VectorTile,
            RasterTile,
            OfflineMap
        }


        internal static string GetDataFolder()
        {
        #if NETFX_CORE
            string appDataFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        #elif XAMARIN
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        #else
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        #endif
            string dataFolder = Path.Combine(appDataFolder, "MapAppData");

            if (!Directory.Exists(dataFolder)) { Directory.CreateDirectory(dataFolder); }

            return dataFolder;
        }

        
        /// <summary>
        /// Gets the path to an item on disk. 
        /// The item must have already been downloaded for the path to be valid.
        /// </summary>
        /// <param name="itemId">ID of the portal item.</param>
        internal static string GetDataFolder(string itemId)
        {
            return Path.Combine(GetDataFolder(), itemId);
        }

        /// <summary>
        /// Gets the path to an item on disk. 
        /// The item must have already been downloaded for the path to be valid.
        /// </summary>
        /// <param name="itemId">ID of the portal item.</param>
        /// <param name="pathParts">Components of the path.</param>
        internal static string GetDataFolder(string itemId, params string[] pathParts)
        {
            return Path.Combine(GetDataFolder(itemId), Path.Combine(pathParts));
        }

        // Map initialization logic is contained in MapViewModel.cs
    }
}

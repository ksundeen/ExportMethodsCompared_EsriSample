using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ExportTiles
{
    public class App : Xamarin.Forms.Application
    {
        public App()
        {
            //****************
            //
            // Authentication:
            // Use of Esri location services, including basemaps and geocoding, requires either an ArcGIS identity or an API key. 
            // For more information see https://links.esri.com/arcgis-runtime-security-auth.
            //
            // Licensing:
            // Production deployment of applications built with ArcGIS Runtime requires you to license ArcGIS Runtime functionality.
            // For more information see https://links.esri.com/arcgis-runtime-license-and-deploy.
            //
            //****************

            // Initialize the ArcGIS Runtime before any components are created.
            ArcGISRuntimeEnvironment.Initialize();

            // The root page of your application
            MainPage = new MapPage();
        }
    }
}

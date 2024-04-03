
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IO;

namespace ChangeAnnotationFCLabelEngine
{
  internal class Program
  {
    static void Main(string[] args)
    {
      ESRI.ArcGIS.esriSystem.AoInitialize aoInit = null;
      try
      {
        ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.Desktop);
        aoInit = new AoInitializeClass();
        esriLicenseStatus licStatus = aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeAdvanced);
        Console.WriteLine("License Checkout successful.");
      }
      catch (Exception exc)
      {
        // If it fails at this point, shutdown the test and ignore any subsequent errors.
        Console.WriteLine("Exception: " + exc.Message);
      }

      if (args.Count() != 1) // there can be only one
      {
        WriteUsage();
        return;
      }

      string path;
      var possiblePath = args[0].Trim();
      possiblePath = possiblePath.Trim('"');
      if (possiblePath.EndsWith(@"\"))
        possiblePath = possiblePath.Substring(0, possiblePath.Length - 1);
      if (Directory.Exists(possiblePath))
      {
        path = possiblePath;
      }
      else
      {
        WriteUsage();
        return;
      }

      string annoExtCLSID = "{24429589-D711-11d2-9F41-00C04F6BC6A5}"; //10.x extension class id

      IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
      IWorkspace workspace = workspaceFactory.OpenFromFile(path, 0);
      IEnumDataset enumDataset = workspace.get_Datasets(esriDatasetType.esriDTFeatureClass);

      enumDataset.Reset();
      IFeatureClass featureClass = enumDataset.Next() as IFeatureClass;
      while (featureClass != null)
      {
        var featureType = featureClass.FeatureType;
        var extCLSID = featureClass.EXTCLSID;

        if ((featureType == esriFeatureType.esriFTAnnotation) && (String.Compare(extCLSID.Value.ToString(), annoExtCLSID, true) == 0))
        {
          ChangeLabelEngine(featureClass);
        }

        featureClass = enumDataset.Next() as IFeatureClass;
      }

      enumDataset = workspace.get_Datasets(esriDatasetType.esriDTFeatureDataset);
      enumDataset.Reset();
      IFeatureDataset featureDS = enumDataset.Next() as IFeatureDataset;
      while (featureDS != null)
      {
        IFeatureClassContainer fcContainer = featureDS as IFeatureClassContainer;
        IEnumFeatureClass enumFC = fcContainer.Classes;
        enumFC.Reset();

        featureClass = enumFC.Next() as IFeatureClass;
        while (featureClass != null)
        {
          var featureType = featureClass.FeatureType;
          var extCLSID = featureClass.EXTCLSID;
          string strExtCLSID = "";
          if (extCLSID != null)
          {
            strExtCLSID = extCLSID.Value.ToString();
          }
          if ((featureType == esriFeatureType.esriFTAnnotation) && (String.Compare(strExtCLSID, annoExtCLSID, true) == 0))
          {
            ChangeLabelEngine(featureClass);
          }

          featureClass = enumFC.Next() as IFeatureClass;
        }
        featureDS = enumDataset.Next() as IFeatureDataset;
      }

      Console.WriteLine("Done");

    }

    private static void WriteUsage()
    {
      Console.WriteLine("Usage: ChangeAnnotationFCLabelEngine.exe <inputfileGDB>");
      Console.WriteLine("Example: ChangeAnnotationFCLabelEngine.exe c:\\temp\\myData.gdb");
    }

    private static void ChangeLabelEngine(IFeatureClass featureClass)
    {
      LabelEngine newLabelEngine = LabelEngine.Standard;
      //open FC
      IAnnotationClassExtension annoClass = featureClass.Extension as IAnnotationClassExtension;
      if (annoClass != null) 
      {
        IOverposterProperties overposterProperties = annoClass.OverposterProperties;
        IMaplexOverposterProperties maplexOverposterProperties = overposterProperties as IMaplexOverposterProperties;
        if (maplexOverposterProperties == null)
        {
          //existing engine isn't Maplex, so let's make the new one Maplex.
          newLabelEngine = LabelEngine.Maplex;
        }
      }

      IAnnoClassAdmin3 annoClassAdmin;
      IOverposterOptions overposterOptionsOld;
      IOverposterOptions overposterOptionsNew;
      IAnnotationPropertiesConverter annotationPropertiesConverter;
      IAnnotateLayerPropertiesCollection annotateLayerPropertiesCollectionOld;
      IAnnotateLayerPropertiesCollection annotateLayerPropertiesCollectionNew;
      esriGeometryType geomType;
      IUID uidOld;
      IUID uidNew;
      IDisplayTransformation displayTrans;

      overposterOptionsOld = (IOverposterOptions)annoClass.OverposterProperties;
      uidOld = new UIDClass();
      uidNew = new UIDClass();

      if (newLabelEngine == LabelEngine.Maplex)
      {
        overposterOptionsNew = new MaplexOverposterPropertiesClass();
        uidOld.Value = "{01004145-0D1C-11D2-A26F-080009B6F22B}"; //ESRI Standard Label Engine
        uidNew.Value = "{20664808-0D1C-11D2-A26F-080009B6F22B}"; //ESRI Maplex Label Engine
      }
      else
      {
        overposterOptionsNew = new BasicOverposterPropertiesClass();
        uidOld.Value = "{20664808-0D1C-11D2-A26F-080009B6F22B}"; //ESRI Maplex Label Engine
        uidNew.Value = "{01004145-0D1C-11D2-A26F-080009B6F22B}"; //ESRI Standard Label Engine
      }

      //exchange properties from the old overposter properties to the new object
      overposterOptionsNew.EnableDrawUnplaced = overposterOptionsOld.EnableDrawUnplaced;
      overposterOptionsNew.EnableLabelCache = overposterOptionsOld.EnableLabelCache;
      overposterOptionsNew.InvertedLabelTolerance = overposterOptionsOld.InvertedLabelTolerance;
      overposterOptionsNew.RotateLabelWithDataFrame = overposterOptionsOld.RotateLabelWithDataFrame;
      overposterOptionsNew.UnplacedLabelColor = overposterOptionsOld.UnplacedLabelColor;

      annotationPropertiesConverter = new MaplexAnnotationPropertiesConverterClass();
      annotateLayerPropertiesCollectionOld = annoClass.AnnoProperties;

      if (annotateLayerPropertiesCollectionOld != null)
      {

        featureClass = annoClass.LinkedFeatureClass;
        if (featureClass != null)
        {
          geomType = featureClass.ShapeType;
        }
        else
        {
          //not feature linked, so we'll choose polygon as default
          geomType = esriGeometryType.esriGeometryPolygon;
        }


        if (annotationPropertiesConverter.CanConvert((UID)uidOld, (UID)uidNew))
        {
          //update the feature class here

          displayTrans = annoClass.Display.DisplayTransformation; //get the display from the feature class extension

          //Actually convert the properties collection to a collection for the new label engine
          annotateLayerPropertiesCollectionNew = annotationPropertiesConverter.Convert(geomType, displayTrans, (UID)uidOld, annotateLayerPropertiesCollectionOld, (UID)uidNew);


          annoClassAdmin = annoClass as IAnnoClassAdmin3;
          //set the new properties into the Annotation Feature Class Extension and Update it
          annoClassAdmin.OverposterProperties = overposterOptionsNew as IOverposterProperties;
          annoClassAdmin.AnnoProperties = annotateLayerPropertiesCollectionNew;
          annoClassAdmin.UpdateProperties(); //does the actual FC update
          IDataset dataset = featureClass as IDataset;
          Console.WriteLine("Upgraded :" + dataset.Name);
        }
        else
        {
          Console.WriteLine("The annotation feature class contains properties that cannot be converted.");
        }
      }
      else
      {
        Console.WriteLine("Annotation feature class does not have a properties collection, please update it with the \"Update Annotation Feature Class\" tool.");
      }
    }
    enum LabelEngine
    {
      Standard,
      Maplex,
    }
  }
}

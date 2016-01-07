﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WIA;
using KaupischITC.ScanWIA;
using System.Drawing;
using System.Windows.Forms;

namespace PaperCapture
{
    class ScannerControlException : ApplicationException
    {
        internal ScannerControlException(string message)
            : base(message)
        {
        }
    }

    class ScannerControl
    {
        private ScannerDevice device;
        private bool useFlatbed;
        private int numPages;
        private bool twoSided;

        private const int RESOLUTION = 180;
        private const bool MONOCHROME = true;
        private SizeF PAGESIZE = new SizeF(8.27f, 11.7f);
        private const int THRESHOLD = 180;

        internal ScannerControl(int pNumPages, bool pTwoSided, bool pUseFlatbed, string pPaperSize)
        {
            if (pPaperSize == "A3")
            {
                this.PAGESIZE = new SizeF(11.67f, 16.47f);
            }
            this.numPages = pNumPages;
            this.twoSided = pTwoSided;
            this.useFlatbed = pUseFlatbed; //if not useflatbed then use doc feeder
            setupScanner();
            setupPicture();
        }

        internal List<Image> Scan()
        {
            return device.PerformScan().ToList();
        }

        //This doesnt work
        //public bool HasPages()
        //    ///check that the document feeder has a page in it
        //{
        //    Property documentHandlingSelect = null;
        //    Property documentHandlingStatus = null;

        //    foreach (Property prop in device.Device.Properties)
        //    {
                
        //        MessageBox.Show(prop.Name + prop.ToString());
        //       //if (prop.PropertyID == WIA_PROPERTIES.WIA_DPS_DOCUMENT_HANDLING_SELECT)
        //     //       documentHandlingSelect = prop;
        //    //    if (prop.PropertyID == WIA_PROPERTIES.WIA_DPS_DOCUMENT_HANDLING_STATUS)
        //    //        documentHandlingStatus = prop;
        //    }
        //    bool hasMorePages = false;
        //  //  if ((Convert.ToUInt32(documentHandlingSelect.get_Value()) & WIA_DPS_DOCUMENT_HANDLING_SELECT.FEEDER) != 0)
        //  //  {
        //  //      hasMorePages = ((Convert.ToUInt32(documentHandlingStatus.get_Value()) & WIA_DPS_DOCUMENT_HANDLING_STATUS.FEED_READY) != 0);
        //  //  }
        //    return hasMorePages;
        //}

     

        private void setupScanner()
        {
            device = WiaDevice.GetFirstScannerDevice().AsScannerDevice();
            // Not checking the device's capabilities: for now, we're working on the assumption that we
            // control the exact model of scanner.
            if (this.useFlatbed)
            {
                device.DeviceSettings.DocumentHandlingSelect = this.twoSided ?
                                    DocumentHandlingSelect.Duplex : DocumentHandlingSelect.Flatbed;
            }
            else
            {
                device.DeviceSettings.DocumentHandlingSelect = this.twoSided ?
                    DocumentHandlingSelect.Duplex : DocumentHandlingSelect.Feeder;
            }
            device.DeviceSettings.Pages = numPages;// *(twoSided ? 2 : 1);
        }

        private void setupPicture()
        {
            device.PictureSettings.CurrentIntent = MONOCHROME ? CurrentIntent.ImageTypeText : CurrentIntent.ImageTypeGrayscale;
            device.PictureSettings.VerticalResolution = RESOLUTION;
            device.PictureSettings.HorizontalResolution = RESOLUTION;
            device.PictureSettings.HorizontalExtent = (int)(PAGESIZE.Width * RESOLUTION);
            device.PictureSettings.VerticalExtent = (int)(PAGESIZE.Height * RESOLUTION);
            device.PictureSettings.Threshold = THRESHOLD;
        }

        public string GetScannerName()
        {         
            ScannerDevice scnr = WiaDevice.GetFirstScannerDevice().AsScannerDevice();
            string scnrNam = scnr.DeviceSettings.DeviceName;
            return scnrNam;
        }


    }
}

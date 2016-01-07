﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Media.Imaging;
using WIA;
using Microsoft.Win32;

namespace PaperCapture
{
    public partial class ScanOptions : Form
    {
        public ScanOptions()
        {
            InitializeComponent();
            cmbxWorkType.SelectedIndex = 0;
            string scnrNam = "";
            try
            {
                ScannerControl scnCtrl = new ScannerControl(0, false, false, "A4");
                scnrNam = scnCtrl.GetScannerName();
                LogMsg("Scanner Detected: " + scnrNam);
            }
            catch (Exception exp)
            {
                string msg = MsgTranslate(exp.Message);
                tbxOutput.AppendText(msg);
            }
            cmbxSource.Items.Add("Scanner Document Feeder: " + scnrNam);
            cmbxSource.Items.Add("Scanner Flatbed: " + scnrNam);
            cmbxSource.Items.Add("File Import");
            cmbxSource.SelectedIndex = 0;
            string val = readRegistry("ScanAndSend");
            if (val != "")
            {
                try
                {
                    cbxScanAndSend.Checked = Convert.ToBoolean(val);
                }
                catch
                {
                    LogMsg("ScanAndSend value is invalid in the registry. Should be true or false");
                }
            }
        }

        private List<LCDoc> DocBatch; //holds the batch of documents

        /// <summary>
        /// Kick off the scanning process according to the user inputs.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void scanButton_Click(object sender, EventArgs e)
        {
            try
            {
                int vPagesPerAppn = (int)numPagesInput.Value;
                int vTotalAppns = -1; //-1 means scan everything.
                int vTotalPages = -1;
                if (!cbxScanAll.Checked)
                {
                    vTotalAppns = (int)numDocsInput.Value;
                    vTotalPages = vTotalAppns * vPagesPerAppn;
                }
                List<Image> images = new List<Image>();
                if (cmbxSource.SelectedIndex == 2) //import from files
                {
                    try
                    {
                        images = importFromFiles(vTotalPages);
                    }
                    catch (System.ArgumentException exp)
                    {
                        MessageBox.Show(exp.Message);
                        return;
                    }
                }
                else
                {
                    string vPaperSize = "A4";
                    if (cbxA3.Checked) {vPaperSize = "A3";}                    
                    ScannerControl control = new ScannerControl(vPagesPerAppn, cbxTwoSides.Checked, cmbxSource.SelectedIndex == 1, vPaperSize);
                    images = scanDocs(control, vTotalAppns);
                }

                int vImagesScanned = images.Count;
                if (vImagesScanned <= 0)
                {
                    MessageBox.Show("No images were scanned");
                    return;
                }
                //check that the amount of documents scanned is correct
                //for the amount of pages per document                                            
                bool vValidBatch = ((vImagesScanned % vPagesPerAppn) == 0);
                if (!vValidBatch)//
                {
                    showAndLog("The amount of pages scanned is incorrect for the amount of documents. Please check the 'Pages per document' setting");
                    return;
                }
                int vAmountOfAppns = vImagesScanned / vPagesPerAppn;
                LogMsg(vAmountOfAppns.ToString() + "x " + vPagesPerAppn.ToString() + " page documents scanned" + Environment.NewLine);

                foreach (Image vImg in images)
                {
                    if (vImg.PhysicalDimension.Width > vImg.PhysicalDimension.Height)
                    {
                        vImg.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    }
                }
                //build batch
                DocBatch = new List<LCDoc>();
                int imgLstCtr = 0;
                tsProg.Maximum = vAmountOfAppns;
                tsProg.Step = 1;
                for (int i = 1; i <= vAmountOfAppns; i++)
                {
                    //for each document, create a Doc object and add the correct amount of page images
                    LCDoc vDoc = new LCDoc();
                    vDoc.SeqNo = i;
                    vDoc.PaperSize = "A4";
                    if (cbxA3.Checked) { vDoc.PaperSize = "A3"; }
                    for (int x = 0; x < vPagesPerAppn; x++)
                    {
                        vDoc.ImgLst.Add(images[imgLstCtr]);
                        imgLstCtr++;
                    }
                    DocBatch.Add(vDoc);                    
                }
                if (cbxScanAndSend.Checked) //scan and send 
                {
                    foreach (LCDoc doc in DocBatch)
                    {
                        //first call returns the doc id, pass this in to subsequent pages
                        int id = 0;
                        dynamic vDocInfo;
                        string formType = "";
                        foreach (Image vImage in doc.ImgLst)
                        {
                            //pass id of 0 to indicate first page of a new document
                            vDocInfo = Requests.AddImageToDocument(id, vImage, doc.PaperSize);
                            id = vDocInfo.id;
                            formType = vDocInfo.form_type;
                        }
                        Requests.CreateWorklistItem(id, formType, getWorkType(cmbxWorkType.Text));
                        LogMsg("Worklist item created: ID " + id.ToString() + " Type: " + formType);
                        tsProg.PerformStep();
                        Application.DoEvents();
                    }
                }
                else //display the batch
                {
                    frmScannedDocs frm = new frmScannedDocs(this);
                    frm.buildTree(DocBatch, getWorkType(cmbxWorkType.Text));
                    frm.ShowDialog();
                }

            }
            catch (Exception exp)
            {
                try
                {
                    string msg = MsgTranslate(exp.Message);
                    showAndLog("An Error Occured: " + msg);
                }
                catch
                {
                    showAndLog("An Error Occured: " + exp.Message);
                }
            }
            finally
            {
                tsProg.Value = 0;
            }
        }

        private void sendBatch()
        {
            try
            {
                foreach (LCDoc doc in DocBatch)
                {
                    int id = 0;
                    dynamic vDoc;
                    string formType = "";
                    foreach (Image vImage in doc.ImgLst)
                    {                        
                        vDoc = Requests.AddImageToDocument(id, vImage, doc.PaperSize);
                        id = vDoc.id;
                        formType = vDoc.form_type;
                    }                    
                    Requests.CreateWorklistItem(id, formType, getWorkType(cmbxWorkType.Text));
                    tbxOutput.AppendText("Worklist item created: ID " + id.ToString() + " Type: " + formType + Environment.NewLine);
                }
            }
            catch (Exception exp)
            {
                try
                {
                    string msg = MsgTranslate(exp.Message);
                    showAndLog("An Error Occured: " + msg);
                }
                catch
                {
                    showAndLog("An Error Occured: " + exp.Message);
                }
            }
        }
        
        /// <summary>
        /// Scan the documents using a physical scanning device. 
        /// </summary>
        /// <param name="pScanner">ScannerControl object</param>
        /// <param name="pDocsRequested">Amount of documents we need to scan (-1 = scan all docs in the feeder) </param>
        /// <returns>List<image> contains all the scanned images for the batch</image></returns>
        private List<Image> scanDocs(ScannerControl pScanner, int pDocsRequested)
        {            
            List<Image> scans = new List<Image>();
            List<Image> tmpScans = new List<Image>();
            bool hasPages = true;
            int docsScanned = 0;
            while (hasPages)
            {
                try
                {
                    tmpScans = pScanner.Scan();
                    foreach (Image img in tmpScans)
                    {
                        scans.Add(img);
                        if (cbxPreview.Checked)
                        {
                            previewImage(img);
                        }
                    }
                    docsScanned++;
                    if (docsScanned == pDocsRequested)
                    {
                        hasPages = false; //we have scanned the number of documents requested
                    }
                }
                catch (Exception exp)
                {
                    //if docs requested = -1 then scan all until we get an out of paper exception.
                    if ((exp.Message.ToUpper() == "EXCEPTION FROM HRESULT: 0X80210003") && (pDocsRequested == -1))
                    {
                        hasPages = false;
                    }
                    else
                    {
                        throw exp;
                    }
                }
            }
            return scans;
        }

        private void previewImage(Image pImg)
        {
            frmPreview prvw = new frmPreview();
            prvw.SetImg(pImg);
            prvw.ShowDialog();
        }

        public void showAndLog(string pMsg)
        {
            LogMsg(pMsg + Environment.NewLine);
            MessageBox.Show(pMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void LogMsg(string pMsg)
        {
            tbxOutput.AppendText(DateTime.Now.ToString("T") + ": " + pMsg + Environment.NewLine);
        }

        /// <summary>
        /// The user is prompted for a folder to import images from. 
        /// </summary>
        /// <returns>An image list of the imported images</returns>
        private List<Image> importFromFiles(int pTotalScans)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = readRegistry("FolderPath");
            DialogResult dr = fbd.ShowDialog();
            //If the cancel the open dialog then throw an exception
            if (dr != DialogResult.OK) { throw new System.ArgumentException("Import files cancelled"); }
            string directory = fbd.SelectedPath;
            writeRegistry("FolderPath", directory);
            List<Image> LCForms = new List<Image>();
            int i = 0;
            foreach (string myFile in Directory.GetFiles(directory))
            {
                if (i == pTotalScans) { break; }
                Image myImg = Image.FromFile(myFile);
                LCForms.Add(myImg); //add image to list
                i++;
            }
            return LCForms;
        }

        /// <summary>
        /// Some messages come back from the scanning dll in German, or soemtime we get an error code from
        /// HRESULT. This method will return the English version of a message or the error message referred to
        /// in https://msdn.microsoft.com/en-us/library/windows/desktop/ms630183(v=vs.85).aspx
        /// </summary>
        /// <param name="pMsg">The incoming message</param>
        /// <returns>Translated version of the message or the same message passed in if no translation is available</returns>
        public string MsgTranslate(string pMsg)
        {
            string outMsg = pMsg; //default to the message passed in.
            pMsg = pMsg.ToUpper();
            if (pMsg == "KEINEN SCANNER GEFUNDEN.") { outMsg = "Scanner not found"; }
            if (pMsg == "PROPERTY WIRD NICT UNTERSTÜTZT") { outMsg = "Property is not supported"; }
            if (pMsg == "EXCEPTION FROM HRESULT: 0X80210003") { outMsg = "There are no documents in the document feeder"; }
            if (pMsg == "EXCEPTION FROM HRESULT: 0X80210004") { outMsg = "An unspecified problem occurred with the scanner's document feeder."; }
            if (pMsg == "EXCEPTION FROM HRESULT: 0X80210006") { outMsg = "The device is busy. Close any apps that are using this device or wait for it to finish and then try again."; }
            if (pMsg == "EXCEPTION FROM HRESULT: 0x80210002") { outMsg = "Paper is jammed in the scanner's document feeder."; }
            if (pMsg == "EXCEPTION FROM HRESULT: 0x8021000D") { outMsg = "The device is locked. Close any apps that are using this device or wait for it to finish and then try again."; }
            return outMsg;
        }

        private void cbxScanAll_CheckedChanged(object sender, EventArgs e)
        {
            numDocsInput.Enabled = !cbxScanAll.Checked;
        }

        /// <summary>
        /// Depending on the selected source adjust the input fields
        /// Feeder/Folder: Enable numPages and numDocs and check the scan all box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmbxSource_SelectedIndexChanged(object sender, EventArgs e)
        {

            switch (cmbxSource.SelectedIndex)
            {

                case 0: //Doc Feeder
                    cbxScanAll.Enabled = true;
                    cbxScanAll.Checked = true;
                    numPagesInput.Enabled = true;
                    numDocsInput.Enabled = true;
                    numDocsInput.Value = 1;
                    numDocsInput.Enabled = false;
                    cbxTwoSides.Checked = false;
                    cbxTwoSides.Enabled = true;
                    break;
                case 1: //flatbed selected                
                    cbxScanAll.Checked = false;
                    cbxScanAll.Enabled = false;
                    cbxTwoSides.Checked = false;
                    cbxTwoSides.Enabled = false;
                    numPagesInput.Value = 1;
                    numPagesInput.Enabled = false;
                    numDocsInput.Value = 1;
                    numDocsInput.Enabled = false;
                    break;
                case 2: //Folder Import
                    cbxScanAll.Enabled = true;
                    cbxScanAll.Checked = true;
                    numPagesInput.Enabled = true;
                    numDocsInput.Enabled = true;
                    numDocsInput.Value = 1;
                    numDocsInput.Enabled = false;
                    cbxTwoSides.Checked = false;
                    cbxTwoSides.Enabled = false;
                    break;
                default:
                    break;
            }
        }

        private void cbxTwoSides_CheckedChanged(object sender, EventArgs e)
        {
            if ((int)numPagesInput.Value < 2) { numPagesInput.Value = 2; }
        }

        /// <summary>
        /// iterate through the batch and send each document to the API
        /// </summary>
        /// <param name="pBatch">A batch of LCDoc objects</param>
        private void sendBatch(List<LCDoc> pBatch)
        {
            foreach (LCDoc doc in pBatch)
            {
                int id = 0;
                foreach (Image img in doc.ImgLst)
                {
                    //pass 0 in to indicate the first page of a new document
                    id = Requests.AddImageToDocument(id, img, doc.PaperSize);
                }
                string formType = Requests.GetFormType(id);
                Requests.CreateWorklistItem(id, formType, getWorkType(cmbxWorkType.Text));
                LogMsg("Worklist item created: ID " + id.ToString() + " Type: " + formType);
            }
        }
  
        private void cbxScanAndSend_CheckedChanged(object sender, EventArgs e)
        {
            if (cbxScanAndSend.Checked) { scanButton.Text = "Scan and Send"; } else { scanButton.Text = "Scan Now"; }
            writeRegistry("ScanAndSend", cbxScanAndSend.Checked.ToString());
        }

        private void writeRegistry(string pName, string pValue)
        {
            //FolderPath - store the last path a folder import took place from.        
            RegistryKey newKey = Registry.LocalMachine.CreateSubKey(@"Software\HMLR\LC");          
            newKey.SetValue(pName, (string)pValue);
        }

        private string readRegistry(string pName)
        {

            //FolderPath - store the last path a folder import took place from.        
            string vVal = "";
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\HMLR\LC", true);
            try
            {
                vVal = (string)regKey.GetValue(pName);
            }
            catch
            {
                vVal = "";
            }
            return vVal;
        }

        //private string getPaperSize(Image pImage)
        //{
        //    //work out whether this is an A4 or A3 document.            
        //    MessageBox.Show("Width: " + pImage.PhysicalDimension.Width + " height: " + pImage.PhysicalDimension.Height);
        //    return "A4";
        //}

        private string getWorkType(string pWrkType)
        {
            string res = pWrkType;
            if (pWrkType == "Bankruptcy Registration") { res = "bank_regn"; }
            if (pWrkType == "Land Charges Registration") { res = "lc_regn"; }
            if (pWrkType == "Amendment") { res = "amend"; }
            if (pWrkType == "Cancellation") { res = "cancel"; }
            if (pWrkType == "Portal Search") { res = "prt_search"; }
            if (pWrkType == "Search") { res = "search"; }
            if (pWrkType == "Official Copy") { res = "oc"; }
            return res;
        }

    }
}

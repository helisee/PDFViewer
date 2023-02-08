/*
    почти готово
    1. провести процедуру перемещения распечатанных документов 
    2.* не печатать уже распечатанное / не вностить в список распечатки (напечатанное будет в другой папке)
    3. подумать над механизмом передачи документов с телефона
 */

using System.Windows;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Media.Imaging;
using PdfiumViewer;
using MessageBox = System.Windows.MessageBox;
using PdfDocument = PdfiumViewer.PdfDocument;
using System.Drawing.Printing;
using System.Windows.Input;

namespace Xewter
{
    public partial class MainWindow : Window
    {
        //private const string CHEQUE_FOLDER = "Чеки";
        //private const string PRINTED_FILE_FOLDER = "Распечатанные чеки";
        //private const string IMAGES_FOLDER = "Изображения";
        //private const string NOTFOUND_PDF = "Нет документов выбранного типа PDF";
        //private const string CHOOSE_FOLDER_WITH_CHEQUE = "Выбери папку с чеками";
        //private const string ERROR_MOVE_FILE = "Ошибка перемещения файла";
        private const string FILETYPE_PDF = "*.pdf";
        private const int PRINT_COLUMN_OFFSET = 15; // промежутки между документами (избавление от белых пробелов между чеками)
        private const int A4_WIDTH = 210;
        private const int A4_HEIGHT = 297;
        private FolderBrowserDialog folderBrowserDialog;
        private string rootPath;
        private string defaultChequesPath;
        private string defaultPrintedChequesPath;
        private string defaultChequeImagesPath;
        private string path2PDFs;
        private string[] foundFiles;
        private List<Image> bitmaps;
        private List<Image> printedImages;
        private List<ImageSource> imgList;
        private PdfiumViewer.PdfDocument pdfdoc;
        private int pdfsOnOneSheet = 5;
        private int pdfCount;
        private int sheetsCount;
        private int pdfWidth = 320;

        public MainWindow()
        {
            InitializeComponent();
            rootPath = AppDomain.CurrentDomain.BaseDirectory;
            defaultChequesPath = rootPath + $@"{ChequePrinter.Properties.Resources.ChequeFolder}\";
            defaultPrintedChequesPath = rootPath + $@"{ChequePrinter.Properties.Resources.PrintedFileFolder}\";
            defaultChequeImagesPath = rootPath + $@"{ChequePrinter.Properties.Resources.ImagesFolder}\";
            if (!Directory.Exists(defaultChequesPath)) Directory.CreateDirectory(defaultChequesPath);
            if (!Directory.Exists(defaultPrintedChequesPath)) Directory.CreateDirectory(defaultPrintedChequesPath);
            if (!Directory.Exists(defaultChequeImagesPath)) Directory.CreateDirectory(defaultChequeImagesPath);
            bitmaps = new List<Image>();
            printedImages = new List<Image>();
            imgList = new List<ImageSource>();
        }
        
        private void ChoseFolder_Click(object sender, RoutedEventArgs e)
        {
            InitializeDefaultValues();
            ScanPdfDocsWithFBD();
            ViewPDF(sender, e);
        }

        private int CalcImageHeightFromWidth(int width)
        {
            return (int)((double)width / A4_WIDTH * A4_HEIGHT); // высота сгенерированного изображения (пропорции А4 210x297)
        }

        private void ViewPDF(object sender, RoutedEventArgs args)
        {
            if (foundFiles.Length == 0)
            {
                MessageBox.Show(ChequePrinter.Properties.Resources.NotFoundPDF);
                return;
            }

            pdfCount = foundFiles.Length;
            sheetsCount = (int)Math.Ceiling((double)pdfCount / pdfsOnOneSheet);
            int imageWidth = pdfWidth * pdfsOnOneSheet;
            int imageHeight = CalcImageHeightFromWidth(imageWidth);


            ImageSource imageSource = null;
            Image pdfRendererImage;
            Bitmap bitmap = new Bitmap(imageWidth, imageHeight);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.FillRectangle(System.Drawing.Brushes.White, new RectangleF(0, 0, imageWidth, imageHeight));

            int parsedPdf = 0;
            int sheetNum = 0;
            foreach (string path in foundFiles)
            {
                using (var doc = PdfDocument.Load(path))
                {
                    var pS = doc.PageSizes;
                    int pWidth = (int)pS[0].Width;
                    int pHeight = (int)pS[0].Height;

                    if (pdfWidth != pWidth) { pdfWidth = pWidth; imageHeight = CalcImageHeightFromWidth(imageWidth); }

                    pdfRendererImage = doc.Render(0, pWidth, pHeight, 96, 96, PdfRotation.Rotate0, PdfRenderFlags.Grayscale);

                    int columnNumber = parsedPdf % pdfsOnOneSheet;
                    int columnOffset = columnNumber * PRINT_COLUMN_OFFSET;
                    int x = columnNumber * pdfWidth - columnOffset;
                    int y = 0;
                    graphics.DrawImage(pdfRendererImage, x, y);
                }
                MoveFileToPrintedFolder(path);

                parsedPdf++;
                if (parsedPdf % pdfsOnOneSheet == 0 || parsedPdf == pdfCount)
                {
                    imageSource = imageToImgSource(bitmap);
                    BitmapPlace.Source = imageSource;
                    int pos = imgBox.Items.Add(imageSource);

                    //imgBox.Items[pos].GetType() = $"Изображение{sheetNum}";
                    
                    bitmaps.Add((Image)bitmap.Clone());
                    SaveBitmap(ref imageSource, ref bitmap, sheetNum);
                    if (parsedPdf == pdfCount)
                    {
                        graphics.Dispose();
                        break;
                    }
                    sheetNum++;
                    bitmap = new Bitmap(imageWidth, imageHeight);
                    graphics = Graphics.FromImage(bitmap);
                    graphics.FillRectangle(System.Drawing.Brushes.White, new RectangleF(0, 0, imageWidth, imageHeight));
                }
            }
        }

        private void MoveFileToPrintedFolder(string file)
        {
            string[] str = file.Split('\\'); // парсим путь по обратным слешам
            try
            {
                string fileName = str[str.Length - 1];
                File.Move(file, defaultPrintedChequesPath + fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ChequePrinter.Properties.Resources.ErrorFileMove + "\n" + ex.Message);
            }
        }

        private void SaveBitmap(ref ImageSource imageSource, ref Bitmap bitmap, int sheetNum)
        {
            bitmap.Save($@"{defaultChequeImagesPath}\чек{DateTime.UtcNow.ToString("ddMMyyyy")}_{sheetNum}.png", ImageFormat.Png);
            bitmap.Dispose();
            BitmapPlace.Source = imageSource;
        }

        private void InitializeDefaultValues()
        {
            SelectedPath.Items.Clear();
            imgBox.Items.Clear();
            bitmaps.Clear();
            printedImages.Clear();
            imgList.Clear();
            BitmapPlace.Source = null;
            
            foundFiles = new string[0];
            
            if (folderBrowserDialog == null)
            {
                folderBrowserDialog = new FolderBrowserDialog()
                {
                    ShowNewFolderButton = false,
                    SelectedPath = defaultChequesPath,
                    Description = ChequePrinter.Properties.Resources.ChooseFolderWithCheque
                };
            }
            BitmapPlace.UpdateLayout();
        }

        private void ScanPdfDocsWithFBD()
        {
            DialogResult result = folderBrowserDialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                path2PDFs = folderBrowserDialog.SelectedPath;

                SelectedPath.Items.Add(path2PDFs);
                foundFiles = System.IO.Directory.GetFiles(path2PDFs, FILETYPE_PDF, System.IO.SearchOption.TopDirectoryOnly);

                foreach (var file in foundFiles)
                {
                    SelectedPath.Items.Add(file);
                }
            }
        }

        public static ImageSource imageToImgSource(Image image)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                var imgSource = new BitmapImage();
                imgSource.BeginInit();
                imgSource.UriSource = null;
                imgSource.CacheOption = BitmapCacheOption.OnLoad;
                imgSource.StreamSource = ms;
                imgSource.EndInit();

                return imgSource;
            }
        }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            ReplaceValuePdfOnOneSheet(4);
        }

        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            ReplaceValuePdfOnOneSheet(5);
        }

        private void ReplaceValuePdfOnOneSheet(int value)
        {
            ReplaceLabel.Content = value.ToString();
            pdfsOnOneSheet = value;
            UpdateLayout();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();

            PrintDocument pd = new PrintDocument();

            try
            {
                pd.PrintPage += new PrintPageEventHandler(pd_PrintPage);
                pd.Print();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Блок PrintButton_Click\n" + ex.Message);
            }
            finally
            {
                MessageBox.Show("Печать началась");
            }
        }

        private void pd_PrintPage(object sender, PrintPageEventArgs ev)
        {
            try
            { 
                var bitmap = GetNext(ref bitmaps, ref printedImages);
                printedImages.Add(bitmap);
                ev.Graphics.DrawImage(bitmap, ev.PageBounds);
                if (printedImages.Count == bitmaps.Count)
                {
                    ev.HasMorePages = false;
                }
                else
                {
                    ev.HasMorePages = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Блок pd_PrintPage\n{ex.Message}", "Ошибка");
            }
            
        }

        private Image GetNext(ref List<Image> img, ref List<Image> printedImages)
        {
            return img[printedImages.Count];
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null && item.Selected)
            {
                BitmapPlace.Source = item.Clone() as ImageSource;
            }
        }
    }
}


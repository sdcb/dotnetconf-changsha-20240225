<Query Kind="Program">
  <NuGetReference>OpenCvSharp4</NuGetReference>
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference.runtime.win64.mkl</NuGetReference>
  <NuGetReference Version="2.7.0.1">Sdcb.PaddleOCR</NuGetReference>
  <NuGetReference>Sdcb.PaddleOCR.Models.Local</NuGetReference>
  <NuGetReference>Vortice.Direct2D1</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>Sdcb.PaddleInference</Namespace>
  <Namespace>Sdcb.PaddleOCR</Namespace>
  <Namespace>Sdcb.PaddleOCR.Models</Namespace>
  <Namespace>Sdcb.PaddleOCR.Models.Local</Namespace>
  <Namespace>Sdcb.PaddleOCR.Models.LocalV3</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DirectWrite</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Vortice.WIC</Namespace>
</Query>

void Main()
{
	using PaddleOcrTableRecognizer tableRec = new(LocalTableRecognitionModel.ChineseMobileV2_SLANET);
	using Mat src = Cv2.ImRead(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "table.jpg"));
	//using Mat src = Cv2.ImDecode(GetClipboardImage(), ImreadModes.Color);
	// Table detection
	TableDetectionResult tableResult = tableRec.Run(src);

	ImageN(tableResult.Visualize(src, Scalar.Red)).Dump();

	// Normal OCR
	using PaddleOcrAll all = new(LocalFullModels.ChineseV3, PaddleDevice.Openblas());
	all.Detector.UnclipRatio = 1.2f;
	PaddleOcrResult ocrResult = all.Run(src);

	// Rebuild table
	string html = tableResult.RebuildTable(ocrResult);

	Util.RawHtml(html).Dump();
}

byte[] GetClipboardImage()
{
	using var ms = new MemoryStream();
	System.Windows.Forms.Clipboard.GetImage()!.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
	return ms.ToArray();
}

private static object ImageN(Mat src) => Util.Image(src.ToBytes(), Util.ScaleMode.Unscaled);

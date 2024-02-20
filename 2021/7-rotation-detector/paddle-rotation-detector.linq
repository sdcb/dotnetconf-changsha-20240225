<Query Kind="Program">
  <NuGetReference>OpenCvSharp4</NuGetReference>
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference.runtime.win64.mkl</NuGetReference>
  <NuGetReference Version="1.0.2-preview.1">Sdcb.RotationDetector</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>Sdcb.RotationDetector</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
</Query>

void Main()
{
	using PaddleRotationDetector detector = new PaddleRotationDetector(RotationDetectionModel.EmbeddedDefault);
	//using Mat src = Cv2.ImDecode(GetClipboardImage(), ImreadModes.Color);
	using Mat src = Cv2.ImDecode(new HttpClient().GetByteArrayAsync("https://io.starworks.cc:88/cv-public/2024/1317141-180.jpg").Result, ImreadModes.Color);
	using Mat resized = src.Resize(default, 1.0, 1.0);
	RotationResult r = detector.Run(resized);
	
	Util.Image(resized.ToBytes(), Util.ScaleMode.Unscaled).Dump("检测到旋转角度：" + r.Rotation);
	Util.Image(r.RestoreRotationInPlace(resized).ToBytes(), Util.ScaleMode.Unscaled).Dump("还原图片");
}

byte[] GetClipboardImage()
{
	using var ms = new MemoryStream();
	Clipboard.GetImage().Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
	return ms.ToArray();
}
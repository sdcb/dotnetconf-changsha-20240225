<Query Kind="Program">
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <NuGetReference>Sdcb.OpenVINO</NuGetReference>
  <NuGetReference>Sdcb.OpenVINO.PaddleOCR</NuGetReference>
  <NuGetReference>Sdcb.OpenVINO.PaddleOCR.Models.Online</NuGetReference>
  <NuGetReference>Sdcb.OpenVINO.runtime.win-x64</NuGetReference>
  <NuGetReference>Vortice.Direct2D1</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>Sdcb.OpenVINO</Namespace>
  <Namespace>Sdcb.OpenVINO.PaddleOCR</Namespace>
  <Namespace>Sdcb.OpenVINO.PaddleOCR.Models</Namespace>
  <Namespace>Sdcb.OpenVINO.PaddleOCR.Models.Online</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DirectWrite</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Vortice.WIC</Namespace>
</Query>

async Task Main()
{
	FullOcrModel model = await OnlineFullModels.ChineseV4.DownloadAsync(QueryCancelToken);
	//FullOcrModel model = new FullOcrModel(
	//	await OnlineDetectionModel.ChineseV3.DownloadAsync(), 
	//	await OnlineClassificationModel.ChineseMobileV2.DownloadAsync(), 
	//	await LocalDictOnlineRecognizationModel.ChineseV4.DownloadAsync());

	//using Mat src = Cv2.ImDecode(GetClipboardImage(), ImreadModes.Color);
	//using Mat src = Cv2.ImRead(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ocr-sample.jpg"));
	using Mat src = Cv2.ImDecode(await new HttpClient().GetByteArrayAsync("https://io.starworks.cc:88/paddlesharp/ocr/samples/xdr5450.webp"), ImreadModes.Color);
	double scale = 1;
	using Mat scaled = src.Resize(default, scale, scale);

	using PaddleOcrAll all = new(model, new PaddleOcrOptions(new DeviceOptions("CPU") // 这里换成CPU或GPU或GPU.0或GPU.1等…
	{
	})
	{
		//DetectionStaticSize = new Size(1024, 1024),
		//RecognitionStaticWidth = 512,
	})
	{
		Enable180Classification = true,
		AllowRotateDetection = true,
	};

	var sw = Stopwatch.StartNew();
	PaddleOcrResult result = all.Run(scaled);
	Console.WriteLine($"耗时：{sw.ElapsedMilliseconds}ms");

	result.Text.Dump();
	//DrawTexts(scaled, result, (float)scale, withScore: false);
	//ImageN(PaddleOcrDetector
	//	.Visualize(scaled, result.Regions.Select(x => x.Rect).ToArray(), Scalar.Red, thickness: 2)
	//	.Resize(OpenCvSharp.Size.Zero, 1 / scale, 1 / scale)).Dump();

	byte[] GetClipboardImage()
	{
		using var ms = new MemoryStream();
		Clipboard.GetImage().Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
		return ms.ToArray();
	}
}

private static object ImageN(Mat src) => Util.Image(src.ToBytes(), Util.ScaleMode.Unscaled);

void DrawTexts(Mat src, PaddleOcrResult texts, float scale, bool withScore)
{
	using Mat c4 = src.CvtColor(ColorConversionCodes.BGR2BGRA);
	using Mat scaled = c4.Resize(default, 1 / scale, 1 / scale);
	using IWICImagingFactory2 wic = new();
	using ID2D1Factory d2dFac = D2D1.D2D1CreateFactory<ID2D1Factory>();
	using IWICBitmap bmp = wic.CreateBitmapFromMemory(scaled.Width, scaled.Height, PixelFormat.Format32bppPBGRA, scaled.Data);
	using ID2D1RenderTarget ctx = d2dFac.CreateWicBitmapRenderTarget(bmp, new RenderTargetProperties());
	using ID2D1SolidColorBrush color = ctx.CreateSolidColorBrush(Colors.Red);
	using ID2D1SolidColorBrush overlay = ctx.CreateSolidColorBrush(new Color4(Colors.Black.ToVector3(), 0.05f));
	using IDWriteFactory dwrite = DWrite.DWriteCreateFactory<IDWriteFactory>();
	using IDWriteTextFormat format = dwrite.CreateTextFormat("Consolas", 24);
	using IDWriteTextFormat formatSmall = dwrite.CreateTextFormat("Consolas", 16);
	ctx.BeginDraw();
	foreach (PaddleOcrResultRegion region in texts.Regions)
	{
		RotatedRect scaledRect = region.Rect switch
		{
			var r => new(new Point2f(r.Center.X / scale, r.Center.Y / scale), new Size2f(r.Size.Width / scale, r.Size.Height / scale), r.Angle)
		};
		Point2f[] points = scaledRect.Points();
		for (int i = 0; i < points.Length; ++i)
		{
			Point2f p1 = points[i];
			Point2f p2 = points[(i + 1) % points.Length];
			ctx.DrawLine(new Vector2(p1.X, p1.Y), new Vector2(p2.X, p2.Y), Color(Colors.Black));
		}
	}

	foreach (PaddleOcrResultRegion region in texts.Regions)
	{
		RotatedRect scaledRect = region.Rect switch
		{
			var r => new(new Point2f(r.Center.X / scale, r.Center.Y / scale), new Size2f(r.Size.Width / scale, r.Size.Height / scale), r.Angle)
		};
		OpenCvSharp.Rect boundingRect = scaledRect.BoundingRect();

		float width = DrawTextInOverlay(region.Text, boundingRect.X, boundingRect.Y - format.FontSize, format, Color(Colors.Red));

		if (withScore)
		{
			DrawTextInOverlay($"{region.Score:N2}", boundingRect.X + width, boundingRect.Y - format.FontSize, formatSmall, Color(Colors.Yellow));
		}

		float DrawTextInOverlay(string text, float x, float y, IDWriteTextFormat format, ID2D1SolidColorBrush brush)
		{
			using IDWriteTextLayout scoreLayout = dwrite.CreateTextLayout(text, format, float.MaxValue, float.MaxValue);
			ctx.FillRectangle(new System.Drawing.RectangleF(x, y, scoreLayout.Metrics.Width, scoreLayout.Metrics.Height), overlay);
			ctx.DrawTextLayout(new Vector2(x, y), scoreLayout, brush);
			return scoreLayout.Metrics.Width;
		}
	}
	ctx.EndDraw().CheckError();
	IWICBitmapLock l = bmp.Lock(BitmapLockFlags.Read);
	Mat result = new Mat(scaled.Height, scaled.Width, MatType.CV_8UC4, l.Data.DataPointer);

	ImageN(result)
		.Dump();

	ID2D1SolidColorBrush Color(Color4 c)
	{
		color.Color = c;
		return color;
	}
}
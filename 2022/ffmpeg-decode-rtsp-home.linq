<Query Kind="Statements">
  <NuGetReference>FlysEngine.Desktop</NuGetReference>
  <NuGetReference Version="4.4.3" Prerelease="true">Sdcb.FFmpeg</NuGetReference>
  <NuGetReference Version="4.4.3">Sdcb.FFmpeg.runtime.windows-x64</NuGetReference>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>Sdcb.FFmpeg.Codecs</Namespace>
  <Namespace>Sdcb.FFmpeg.Common</Namespace>
  <Namespace>Sdcb.FFmpeg.Devices</Namespace>
  <Namespace>Sdcb.FFmpeg.Formats</Namespace>
  <Namespace>Sdcb.FFmpeg.Raw</Namespace>
  <Namespace>Sdcb.FFmpeg.Swscales</Namespace>
  <Namespace>Sdcb.FFmpeg.Toolboxs.Extensions</Namespace>
  <Namespace>Sdcb.FFmpeg.Toolboxs.Generators</Namespace>
  <Namespace>Sdcb.FFmpeg.Utils</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DXGI</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
</Query>

#nullable enable

FFmpegBmp? ffBmp = null;
FFmpegBmp? lastFFbmp = null;
FFmpegLogger.LogWriter = (level, msg) => Util.FixedFont(msg).Dump();
CancellationTokenSource cts = new ();

using RenderWindow w = new();
Task.Run(() => DecodeRTSP(Util.GetPassword("home-rtsp-ipc"), cts.Token));
w.Draw += (_, ctx) =>
{
	if (ffBmp == null) return;
	if (lastFFbmp == ffBmp) return;

	GCHandle handle = GCHandle.Alloc(ffBmp.Data, GCHandleType.Pinned);
	try
	{
		using ID2D1Bitmap bmp = ctx.CreateBitmap(new SizeI(ffBmp.Width, ffBmp.Height), handle.AddrOfPinnedObject(), ffBmp.RowPitch, new BitmapProperties(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
		lastFFbmp = ffBmp;
		Size clientSize = ctx.Size;
		float top = (clientSize.Height - ffBmp.Height) / 2;
		ctx.Transform = Matrix3x2.CreateTranslation(0, top);
		ctx.DrawBitmap(bmp, 1.0f, InterpolationMode.Linear);
	}
	finally
	{
		handle.Free();
	}
};
w.FormClosing += delegate { cts.Cancel(); };
RenderLoop.Run(w, () => w.Render(1, Vortice.DXGI.PresentFlags.None));

void DecodeRTSP(string url, CancellationToken cancellationToken = default)
{
	using FormatContext fc = FormatContext.OpenInputUrl(url, options: new MediaDictionary
	{
		["rtsp_transport"] = "tcp", 
	});
	fc.LoadStreamInfo();
	MediaStream videoStream = fc.GetVideoStream();
	
	using CodecContext videoDecoder = new CodecContext(Codec.FindDecoderByName("hevc_cuvid"));
	videoDecoder.FillParameters(videoStream.Codecpar!);
	videoDecoder.Open();
	
	Packet pkg;
	
	var dc = new DumpContainer().Dump();
	foreach (Frame frame in fc
		.ReadPackets(videoStream.Index)
		.DecodePackets(videoDecoder)
		.ConvertVideoFrames(() => new (w.ClientSize.Width, w.ClientSize.Width * videoDecoder.Height / videoDecoder.Width), AVPixelFormat.Bgr0))
	{
		if (cancellationToken.IsCancellationRequested) break;
		
		try
		{
			byte[] data = new byte[frame.Linesize[0] * frame.Height];
			Marshal.Copy(frame.Data._0, data, 0, data.Length);
			ffBmp = new FFmpegBmp(frame.Width, frame.Height, frame.Linesize[0], data);
		}
		finally
		{
			frame.Unref();
		}
	}
}

public record FFmpegBmp(int Width, int Height, int RowPitch, byte[] Data);
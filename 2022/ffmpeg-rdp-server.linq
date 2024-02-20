<Query Kind="Program">
  <NuGetReference Version="4.4.3-preview.6" Prerelease="true">Sdcb.FFmpeg</NuGetReference>
  <NuGetReference Version="4.4.3">Sdcb.FFmpeg.runtime.windows-x64</NuGetReference>
  <NuGetReference Prerelease="true">Sdcb.ScreenCapture</NuGetReference>
  <Namespace>Sdcb</Namespace>
  <Namespace>Sdcb.FFmpeg.Codecs</Namespace>
  <Namespace>Sdcb.FFmpeg.Raw</Namespace>
  <Namespace>Sdcb.FFmpeg.Swscales</Namespace>
  <Namespace>Sdcb.FFmpeg.Toolboxs.Extensions</Namespace>
  <Namespace>Sdcb.FFmpeg.Utils</Namespace>
  <Namespace>System.Buffers.Binary</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Sockets</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
</Query>

void Main()
{
	StartService(QueryCancelToken);
}

void StartService(CancellationToken cancellationToken = default)
{
	var tcpListener = new TcpListener(IPAddress.Any, 5555);
	cancellationToken.Register(() => tcpListener.Stop());
	tcpListener.Start();

	while (!cancellationToken.IsCancellationRequested)
	{
		TcpClient client = tcpListener.AcceptTcpClient();
		Task.Run(() => ServeClient(client, cancellationToken));
	}
}

void ServeClient(TcpClient tcpClient, CancellationToken cancellationToken = default)
{
	try
	{
		using var _ = tcpClient;
		using NetworkStream stream = tcpClient.GetStream();
		using BinaryWriter writer = new(stream);
		RectI screenSize = ScreenCapture.GetScreenSize(screenId: 0);
		RdpCodecParameter rcp = new(AVCodecID.H264, screenSize.Width, screenSize.Height, AVPixelFormat.Bgr0);

		using CodecContext cc = new(Codec.CommonEncoders.Libx264RGB)
		{
			Width = rcp.Width,
			Height = rcp.Height,
			PixelFormat = rcp.PixelFormat,
			TimeBase = new AVRational(1, 20),
		};
		cc.Open(null, new MediaDictionary
		{
			["crf"] = "30",
			["tune"] = "zerolatency",
			["preset"] = "veryfast"
		});

		writer.Write(rcp.ToArray());
		using Frame source = new();
		foreach (Packet packet in ScreenCapture
			.CaptureScreenFrames(screenId: 0)
			.ToBgraFrame()
			.ConvertFrames(cc)
			.EncodeFrames(cc))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			writer.Write(packet.Data.Length);
			writer.Write(packet.Data.AsSpan());
		}
	}
	catch (IOException ex)
	{
		// Unable to write data to the transport connection: 远程主机强迫关闭了一个现有的连接。.
		// Unable to write data to the transport connection: 你的主机中的软件中止了一个已建立的连接。
		ex.Dump();
	}
}

public class Filo<T> : IDisposable
{
	private T? Item { get; set; }
	private ManualResetEventSlim Notify { get; } = new ManualResetEventSlim();

	public void Update(T item)
	{
		Item = item;
		Notify.Set();
	}

	public IEnumerable<T> Consume(CancellationToken cancellationToken = default)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			Notify.Wait(cancellationToken);
			yield return Item!;
		}
	}

	public void Dispose() => Notify.Dispose();
}

public static class BgraFrameExtensions
{
	public static IEnumerable<Frame> ToBgraFrame(this IEnumerable<LockedBgraFrame> bgras)
	{
		using Frame frame = new Frame();
		foreach (LockedBgraFrame bgra in bgras)
		{
			frame.Width = bgra.Width;
			frame.Height = bgra.Height;
			frame.Format = (int)AVPixelFormat.Bgra;
			frame.Data[0] = bgra.DataPointer;
			frame.Linesize[0] = bgra.RowPitch;
			yield return frame;
		}
	}
}

record RdpCodecParameter(AVCodecID CodecId, int Width, int Height, AVPixelFormat PixelFormat)
{
	public byte[] ToArray()
	{
		byte[] data = new byte[16];
		Span<byte> span = data.AsSpan();
		BinaryPrimitives.WriteInt32LittleEndian(span, (int)CodecId);
		BinaryPrimitives.WriteInt32LittleEndian(span[4..], Width);
		BinaryPrimitives.WriteInt32LittleEndian(span[8..], Height);
		BinaryPrimitives.WriteInt32LittleEndian(span[12..], (int)PixelFormat);
		return data;
	}
}

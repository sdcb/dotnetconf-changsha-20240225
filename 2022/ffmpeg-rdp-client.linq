<Query Kind="Program">
  <NuGetReference>FlysEngine.Desktop</NuGetReference>
  <NuGetReference Version="4.4.3-preview.6" Prerelease="true">Sdcb.FFmpeg</NuGetReference>
  <NuGetReference Version="4.4.3">Sdcb.FFmpeg.runtime.windows-x64</NuGetReference>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>Sdcb</Namespace>
  <Namespace>Sdcb.FFmpeg.Codecs</Namespace>
  <Namespace>Sdcb.FFmpeg.Raw</Namespace>
  <Namespace>Sdcb.FFmpeg.Swscales</Namespace>
  <Namespace>Sdcb.FFmpeg.Toolboxs.Extensions</Namespace>
  <Namespace>Sdcb.FFmpeg.Utils</Namespace>
  <Namespace>System.Buffers</Namespace>
  <Namespace>System.Buffers.Binary</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Sockets</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Vortice.DCommon</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DXGI</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Sdcb.FFmpeg.Common</Namespace>
</Query>

#nullable enable

ManagedBgraFrame? managedFrame = null;
bool cancel = false;

unsafe void Main()
{
	using RenderWindow w = new();
	w.FormClosed += delegate { cancel = true; };
	Task decodingTask = Task.Run(() => DecodeThread(() => (3840, 2160)));

	w.Draw += (_, ctx) =>
	{
		ctx.Clear(Colors.CornflowerBlue);
		if (managedFrame == null) return;

		ManagedBgraFrame frame = managedFrame.Value;

		fixed (byte* ptr = frame.Data)
		{
			//new System.Drawing.Bitmap(frame.Width, frame.Height, frame.RowPitch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, (IntPtr)ptr).DumpUnscaled();
			BitmapProperties1 props = new(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied));
			using ID2D1Bitmap bmp = ctx.CreateBitmap(new SizeI(frame.Width, frame.Height), (IntPtr)ptr, frame.RowPitch, props);
			ctx.UnitMode = UnitMode.Dips;
			ctx.DrawBitmap(bmp, 1.0f, InterpolationMode.NearestNeighbor);
		}
	};
	RenderLoop.Run(w, () => w.Render(1, Vortice.DXGI.PresentFlags.None));
}

async Task DecodeThread(Func<(int width, int height)> sizeAccessor)
{
	using TcpClient client = new TcpClient();
	await client.ConnectAsync(IPAddress.Loopback, 5555);
	using NetworkStream stream = client.GetStream();

	using BinaryReader reader = new(stream);
	RdpCodecParameter rcp = RdpCodecParameter.FromSpan(reader.ReadBytes(16));

	using CodecContext cc = new(Codec.FindDecoderById(rcp.CodecId))
	{
		Width = rcp.Width,
		Height = rcp.Height,
		PixelFormat = rcp.PixelFormat,
	};
	cc.Open(null);

	foreach (var frame in reader
		.ReadPackets()
		.DecodePackets(cc)
		.ConvertVideoFrames(sizeAccessor, AVPixelFormat.Bgra)
		.ToManaged()
		)
	{
		if (cancel) break;
		managedFrame = frame;
	}
}


public static class FramesExtensions
{
	public static IEnumerable<ManagedBgraFrame> ToManaged(this IEnumerable<Frame> bgraFrames, bool unref = true)
	{
		foreach (Frame frame in bgraFrames)
		{
			int rowPitch = frame.Linesize[0];
			int length = rowPitch * frame.Height;
			byte[] buffer = new byte[length];
			Marshal.Copy(frame.Data._0, buffer, 0, length);
			ManagedBgraFrame managed = new(buffer, length, length / frame.Height);
			if (unref) frame.Unref();
			yield return managed;
		}
	}
}

public record struct ManagedBgraFrame(byte[] Data, int Length, int RowPitch)
{
	public int Width => RowPitch / BytePerPixel;
	public int Height => Length / RowPitch;

	public const int BytePerPixel = 4;
}


public static class ReadPacketExtensions
{
	public static IEnumerable<Packet> ReadPackets(this BinaryReader reader)
	{
		using Packet packet = new();
		while (true)
		{
			int packetSize = reader.ReadInt32();
			if (packetSize == 0) yield break;

			byte[] data = reader.ReadBytes(packetSize);
			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
			try
			{
				packet.Data = new DataPointer(dataHandle.AddrOfPinnedObject(), packetSize);
				yield return packet;
			}
			finally
			{
				dataHandle.Free();
			}
		}
	}
}

record RdpCodecParameter(AVCodecID CodecId, int Width, int Height, AVPixelFormat PixelFormat)
{
	public static RdpCodecParameter FromSpan(ReadOnlySpan<byte> data)
	{
		return new RdpCodecParameter(
			CodecId: (AVCodecID)BinaryPrimitives.ReadInt32LittleEndian(data),
			Width: BinaryPrimitives.ReadInt32LittleEndian(data[4..]),
			Height: BinaryPrimitives.ReadInt32LittleEndian(data[8..]),
			PixelFormat: (AVPixelFormat)BinaryPrimitives.ReadInt32LittleEndian(data[12..]));
	}
}
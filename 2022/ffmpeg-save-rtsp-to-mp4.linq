<Query Kind="Statements">
  <NuGetReference Version="4.4.3-preview.5" Prerelease="true">Sdcb.FFmpeg</NuGetReference>
  <NuGetReference Version="4.4.3">Sdcb.FFmpeg.runtime.windows-x64</NuGetReference>
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
</Query>

FFmpegLogger.LogWriter = (level, msg) => Console.Write(Util.FixedFont(msg));

using FormatContext inFc = FormatContext.OpenInputUrl(Util.GetPassword("home-rtsp-ipc"));
inFc.LoadStreamInfo();
MediaStream inAudioStream = inFc.GetAudioStream();
MediaStream inVideoStream = inFc.GetVideoStream();
long gpts_v = 0, gpts_a = 0, gdts_v = 0, gdts_a = 0;

while (!QueryCancelToken.IsCancellationRequested)
{
	using FormatContext outFc = FormatContext.AllocOutput(formatName: "mov");
	string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "rtsp", DateTime.Now.ToString("yyyy-MM-dd"));
	Directory.CreateDirectory(dir);
	using IOContext io = IOContext.OpenWrite(Path.Combine(dir, $"{DateTime.Now:HHmmss}.mov"));
	outFc.Pb = io;

	MediaStream videoStream = outFc.NewStream(Codec.FindEncoderById(inVideoStream.Codecpar!.CodecId));
	videoStream.Codecpar!.CopyFrom(inVideoStream.Codecpar);
	videoStream.TimeBase = inVideoStream.RFrameRate.Inverse();
	videoStream.SampleAspectRatio = inVideoStream.SampleAspectRatio;

	MediaStream audioStream = outFc.NewStream(Codec.FindEncoderById(inAudioStream.Codecpar!.CodecId));
	audioStream.Codecpar!.CopyFrom(inAudioStream.Codecpar);
	audioStream.TimeBase = inAudioStream.TimeBase;
	audioStream.Codecpar.ChannelLayout = (ulong)ffmpeg.av_get_default_channel_layout(inAudioStream.Codecpar.Channels);

	outFc.WriteHeader();
	
	FilterPackets(inFc.ReadPackets(inAudioStream.Index, inVideoStream.Index), videoFrameCount: 60 * 20)
		.WriteAll(outFc);
	outFc.WriteTrailer();

	IEnumerable<Packet> FilterPackets(IEnumerable<Packet> packets, int videoFrameCount)
	{
		long pts_v = gpts_v, pts_a = gpts_a, dts_v = gdts_v, dts_a = gdts_a;
		long[] buffer = new long[200];
		long ithreshold = -1;
		int videoFrame = 0;

		foreach (Packet pkt in packets)
		{
			pkt.StreamIndex = pkt.StreamIndex == inAudioStream.Index ?
					audioStream.Index :
					videoStream.Index;
			if (pkt.StreamIndex == inAudioStream.Index)
			{
				// audio
				(gpts_a, gdts_a, pkt.Pts, pkt.Dts) = (pkt.Pts, pkt.Dts, pkt.Pts - pts_a, pkt.Dts - dts_a);
				pkt.RescaleTimestamp(inAudioStream.TimeBase, audioStream.TimeBase);
			}
			else
			{
				// video
				if (videoFrame < buffer.Length)
				{
					buffer[videoFrame] = pkt.Data.Length;
					ithreshold = -1;
				}
				else if (videoFrame == buffer.Length)
				{
					ithreshold = buffer.Order().ToArray()[buffer.Length / 2] * 4;
				}
				
				if (videoFrame >= videoFrameCount && pkt.Data.Length > ithreshold)
				{
					break;
				}

				(gpts_v, gdts_v, pkt.Pts, pkt.Dts) = (pkt.Pts, pkt.Dts, pkt.Pts - pts_v, pkt.Dts - dts_v);
				pkt.RescaleTimestamp(inVideoStream.TimeBase, videoStream.TimeBase);
				videoFrame++;
			}
			yield return pkt;
		}
	}
}

public static class PacketsExtensions
{
	public static IEnumerable<IEnumerable<Packet>> Partition(this IEnumerable<Packet> packets, int videoIndex, AVRational videoTimebase, double videoDuration)
	{
		long gpts_v = 0, gpts_a = 0, gdts_v = 0, gdts_a = 0;
		IEnumerator<Packet> enumerator = packets.GetEnumerator();

		Packet? last = null;
		while (enumerator.MoveNext())
		{
			yield return NextPartition(last, enumerator);
		}

		IEnumerable<Packet> NextPartition(Packet? first, IEnumerator<Packet> enumerator)
		{
			long pts_v = gpts_v, pts_a = gpts_a, dts_v = gdts_v, dts_a = gdts_a;
			if (first != null)
			{
				first.Pts = 0;
				first.Dts = 0;
				yield return first;
			}
			
			bool shouldTerminate;
			do
			{
				Packet pkt = enumerator.Current;
				shouldTerminate = ShouldPartition(pkt);
				if (shouldTerminate)
				{
					if (last != null)
					{
						last.Dispose();
					}
					last = pkt.Clone();
				}
				
				if (pkt.StreamIndex == videoIndex)
				{
					(gpts_v, gdts_v, pkt.Pts, pkt.Dts) = (pkt.Pts, pkt.Dts, pkt.Pts - pts_v, pkt.Dts - dts_v);
				}
				else
				{
					(gpts_a, gdts_a, pkt.Pts, pkt.Dts) = (pkt.Pts, pkt.Dts, pkt.Pts - pts_a, pkt.Dts - dts_a);
				}
				yield return pkt;
			}
			while (!shouldTerminate && enumerator.MoveNext());
			
			bool ShouldPartition(Packet packet) => packet.StreamIndex == videoIndex && packet.Data.Length > 40000;
		}
	}
}
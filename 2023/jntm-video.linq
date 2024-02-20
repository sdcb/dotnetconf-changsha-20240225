<Query Kind="Statements">
  <NuGetReference>OpenCvSharp4</NuGetReference>
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
</Query>

using VideoCapture vc = new("https://io.starworks.cc:88/cv-public/2024/jntm.mp4");
using Mat mat = new();
var dc = new DumpContainer().Dump();
while (vc.Grab())
{
	vc.Retrieve(mat);
	using Mat resized = mat.Resize(default, 1.0 / 7, 1.0 / 16);
	using Mat gray8 = resized.CvtColor(ColorConversionCodes.RGB2GRAY);
	dc.Content = Util.FixedFont(BmpToString(gray8));
	Thread.Sleep(15);
}

string BmpToString(Mat gray8)
{
	int width = gray8.Width;
	int height = gray8.Height;
	StringBuilder result = new((width + 2) * height);
	byte* pix = (byte*)gray8.DataPointer;

	for (int y = 0; y < height; ++y)
	{
		for (int x = 0; x < width; ++x)
		{
			byte gray = pix[y * width + x];
			char c = gray switch
			{
				> 224 => '@',
				> 208 => '#',
				> 192 => '8',
				> 176 => '&',
				> 160 => 'o',
				> 144 => ':',
				> 128 => '*',
				> 96 => '.',
				_ => ' ',
			};
			result.Append(c);
		}
		result.AppendLine();
	}
	
	return result.ToString();
}
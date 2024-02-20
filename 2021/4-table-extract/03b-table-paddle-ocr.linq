<Query Kind="Program">
  <NuGetReference>OpenCvSharp4</NuGetReference>
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference.runtime.win64.mkl</NuGetReference>
  <NuGetReference>Sdcb.PaddleOCR</NuGetReference>
  <NuGetReference>Sdcb.PaddleOCR.Models.Local</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Sdcb.PaddleOCR</Namespace>
  <Namespace>Sdcb.PaddleOCR.Models.Local</Namespace>
</Query>

#load ".\01_detect-table"

async Task Main()
{
	Environment.CurrentDirectory = Util.GetPassword("dotnet2021-cv-zhoujie-demo4");
	
	using var ocr = new TableOCR();
	foreach (string file in Directory.EnumerateFiles(@".\resources", "*.jpg").OrderBy(x => x).Take(1))
	{
		if (QueryCancelToken.IsCancellationRequested) break;
		
		using Mat src = Cv2.ImRead(file);
		using Mat resized = src.Resize(default, 2, 2);
		Mat[,] matTable = GetMatTable(resized);
		Util.HorizontalRun(false, Image(src), ocr.Process(matTable, QueryCancelToken)).Dump();
	}
}

Mat[,] Scale(Mat[,] src, double fx, double fy)
{
	var result = new Mat[src.GetLength(0), src.GetLength(1)];
	for (int y = 0; y < src.GetLength(0); ++y)
	{
		for (int x = 0; x < src.GetLength(1); ++x)
		{
			Mat cell = src[y, x];
			Mat scaled = cell.Resize(default, fx, fy);
			result[y, x] = scaled;
		}
	}
	return result;
}

public class TableOCR : IDisposable
{
	PaddleOcrAll _eng = new(LocalFullModels.EnglishV3)
	{
		AllowRotateDetection = false, 
		Enable180Classification = false
	};
	PaddleOcrAll _chs = new(LocalFullModels.ChineseV3)
	{
		AllowRotateDetection = false, 
		Enable180Classification = false, 
	};
	
	public TableOCR()
	{
		_chs.Detector.UnclipRatio = 2.0f;
	}
	
	public string[,] Process(Mat[,] src, CancellationToken cancellationToken)
	{
		int rows = src.GetLength(0);
		int cols = src.GetLength(1);
		var result = new string[rows, cols];
		
		for (int y = 0; y < rows; ++y)
		{
			for (int x = 0; x < cols; ++x)
			{
				if (cancellationToken.IsCancellationRequested) break;
				
				Mat cell = src[y, x];
				if (y > 0 && (x == 3 || x == 4))
				{
					var r = _eng.Run(cell);
					result[y, x] = r.Text;
				}
				else
				{
					var r = _chs.Run(cell);
					result[y, x] = r.Text;
				}
			}
		}
		return result;
	}

	public void Dispose()
	{
		_eng.Dispose();
	}
}
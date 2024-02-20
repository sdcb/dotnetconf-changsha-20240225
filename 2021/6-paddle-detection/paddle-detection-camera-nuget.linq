<Query Kind="Program">
  <NuGetReference>OpenCvSharp4.runtime.win</NuGetReference>
  <NuGetReference>Sdcb.PaddleDetection</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference.runtime.win64.mkl</NuGetReference>
  <Namespace>OpenCvSharp</Namespace>
  <Namespace>Sdcb.PaddleDetection</Namespace>
  <Namespace>Sdcb.PaddleInference</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>YamlDotNet.RepresentationModel</Namespace>
</Query>

void Main()
{
	DetectionLocalModel model = DetectionLocalModel.PPYolos.Mbv3_large_coco;
	using PaddleDetector detector = new(model.Directory, model.ConfigPath, PaddleDevice.Mkldnn());

	using VideoCapture vc = new VideoCapture();
	vc.Open(0);
	var dc = new DumpContainer().Dump();
	while (!QueryCancelToken.IsCancellationRequested)
	{
		Stopwatch sw = Stopwatch.StartNew();
		using Mat mat = vc.RetrieveMat();
		var retriveTime = sw.Elapsed.TotalMilliseconds;
		sw.Restart();
		DetectionResult[] results = detector.Run(mat);
		dc.Content = new { sw.Elapsed.TotalMilliseconds, retriveTime };

		using Mat dest = PaddleDetector.Visualize(mat, results.Where(x => x.Confidence > 0.5f), detector.Config.LabelList.Length);
		Cv2.ImShow("test", dest);
		Cv2.WaitKey(1);
	}
	Cv2.DestroyAllWindows();
}

public record DetectionLocalModel(string Name, string Directory)
{
	public string ParamsPath => Path.Combine(Directory, "model.pdiparams");
	public string ProgramPath => Path.Combine(Directory, "model.pdmodel");
	public string ConfigPath => Path.Combine(Directory, "infer_cfg.yml");

	public bool IsValid()
	{
		static bool Check(string path) => new FileInfo(path) switch
		{
			{ Exists: true, Length: > 0 } => true,
			_ => false
		};
		return Check(ParamsPath) && Check(ProgramPath) && Check(ConfigPath);
	}

	public static string RootDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paddle-models");
	public readonly static PicoDetModels PicoDets = new PicoDetModels(RootDirectory);
	public readonly static PPYoloModels PPYolos = new PPYoloModels(RootDirectory);
}

public record PicoDetModels(string RootDirectory)
{
	public readonly DetectionLocalModel S_320_coco = RawOnlineModel.PicoDets.S_320_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel S_416_coco = RawOnlineModel.PicoDets.S_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel M_320_coco = RawOnlineModel.PicoDets.M_320_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel M_416_coco = RawOnlineModel.PicoDets.M_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel L_320_coco = RawOnlineModel.PicoDets.L_320_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel L_416_coco = RawOnlineModel.PicoDets.L_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel L_640_coco = RawOnlineModel.PicoDets.L_640_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Shufflenetv2_1x_416_coco = RawOnlineModel.PicoDets.Shufflenetv2_1x_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Mobilenetv3_large_1x_416_coco = RawOnlineModel.PicoDets.Mobilenetv3_large_1x_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Lcnet_1_5x_416_coco = RawOnlineModel.PicoDets.Lcnet_1_5x_416_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Lcnet_1_5x_640_coco = RawOnlineModel.PicoDets.Lcnet_1_5x_640_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel R18_640_coco = RawOnlineModel.PicoDets.R18_640_coco.DryRunGetInferenceModel(RootDirectory);
}

public record PPYoloModels(string RootDirectory)
{
	public readonly DetectionLocalModel R50vd_dcn_1x_coco = RawOnlineModel.PPYolos.R50vd_dcn_1x_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel R50vd_dcn_2x_coco = RawOnlineModel.PPYolos.R50vd_dcn_2x_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel R18vd_coco = RawOnlineModel.PPYolos.R18vd_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel V2_r50vd_dcn_365e_coco = RawOnlineModel.PPYolos.V2_r50vd_dcn_365e_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel V2_r101vd_dcn_365e_coco = RawOnlineModel.PPYolos.V2_r101vd_dcn_365e_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Mbv3_large_coco = RawOnlineModel.PPYolos.Mbv3_large_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Mbv3_small_coco = RawOnlineModel.PPYolos.Mbv3_small_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel Tiny_650e_coco = RawOnlineModel.PPYolos.Tiny_650e_coco.DryRunGetInferenceModel(RootDirectory);
	public readonly DetectionLocalModel R50vd_dcn_voc = RawOnlineModel.PPYolos.R50vd_dcn_voc.DryRunGetInferenceModel(RootDirectory);
}

public record RawOnlineModel(string ParamsLink, string YmlRelativePath)
{
	public string Name => Path.GetFileNameWithoutExtension(ParamsLink);

	public static readonly PicoDetRawModels PicoDets = new PicoDetRawModels();
	public static readonly PPYoloRawModels PPYolos = new PPYoloRawModels();

	public async Task<RawLocalModel> DownloadAsync(string rootDirectory, CancellationToken cancellationToken = default)
	{
		using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(1000) };
		string dir = Path.Combine(rootDirectory, Name);
		Directory.CreateDirectory(dir);

		string localParamFile = Path.Combine(dir, new Uri(ParamsLink).Segments.Last());
		if (!File.Exists(localParamFile))
		{
			Console.WriteLine($"Download {localParamFile} from {ParamsLink}...");
			await DownloadFile(new[] { ParamsLink }, localParamFile, cancellationToken);
		}

		return new RawLocalModel(Name, localParamFile, YmlRelativePath);
	}

	public DetectionLocalModel DryRunGetInferenceModel(string outputDir)
	{
		return new DetectionLocalModel(Name, Path.Combine(outputDir, Name));
	}

	internal static async Task DownloadFile(string[] uris, string localFile, CancellationToken cancellationToken)
	{
		using HttpClient http = new();
		foreach (string uri in uris)
		{
			try
			{
				HttpResponseMessage resp = await http.GetAsync(uri, cancellationToken);
				if (!resp.IsSuccessStatusCode)
				{
					Console.WriteLine($"Failed to download: {uri}, status code: {(int)resp.StatusCode}({resp.StatusCode})");
					continue;
				}

				using (FileStream file = File.OpenWrite(localFile))
				{
					await resp.Content.CopyToAsync(file, cancellationToken);
					return;
				}
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Failed to download: {uri}, {ex}");
				continue;
			}
			catch (TaskCanceledException)
			{
				Console.WriteLine($"Failed to download: {uri}, timeout.");
				continue;
			}
		}
		throw new Exception($"Failed to download {localFile} from all uris: {string.Join(", ", uris.Select(x => x.ToString()))}");
	}
}

public class PicoDetRawModels
{
	public readonly RawOnlineModel S_320_coco = new("https://paddledet.bj.bcebos.com/models/picodet_s_320_coco.pdparams", "./configs/picodet/picodet_s_320_coco.yml");
	public readonly RawOnlineModel S_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_s_416_coco.pdparams", "./configs/picodet/picodet_s_416_coco.yml");
	public readonly RawOnlineModel M_320_coco = new("https://paddledet.bj.bcebos.com/models/picodet_m_320_coco.pdparams", "./configs/picodet/picodet_m_320_coco.yml");
	public readonly RawOnlineModel M_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_m_416_coco.pdparams", "./configs/picodet/picodet_m_416_coco.yml");
	public readonly RawOnlineModel L_320_coco = new("https://paddledet.bj.bcebos.com/models/picodet_l_320_coco.pdparams", "./configs/picodet/picodet_l_320_coco.yml");
	public readonly RawOnlineModel L_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_l_416_coco.pdparams", "./configs/picodet/picodet_l_416_coco.yml");
	public readonly RawOnlineModel L_640_coco = new("https://paddledet.bj.bcebos.com/models/picodet_l_640_coco.pdparams", "./configs/picodet/picodet_l_640_coco.yml");
	public readonly RawOnlineModel Shufflenetv2_1x_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_shufflenetv2_1x_416_coco.pdparams", "./configs/picodet/more_config/picodet_shufflenetv2_1x_416_coco.yml");
	public readonly RawOnlineModel Mobilenetv3_large_1x_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_mobilenetv3_large_1x_416_coco.pdparams", "./configs/picodet/more_config/picodet_mobilenetv3_large_1x_416_coco.yml");
	public readonly RawOnlineModel Lcnet_1_5x_416_coco = new("https://paddledet.bj.bcebos.com/models/picodet_lcnet_1_5x_416_coco.pdparams", "./configs/picodet/more_config/picodet_lcnet_1_5x_416_coco.yml");
	public readonly RawOnlineModel Lcnet_1_5x_640_coco = new("https://paddledet.bj.bcebos.com/models/picodet_lcnet_1_5x_640_coco.pdparams", "./configs/picodet/more_config/picodet_lcnet_1_5x_640_coco.yml");
	public readonly RawOnlineModel R18_640_coco = new("https://paddledet.bj.bcebos.com/models/picodet_r18_640_coco.pdparams", "./configs/picodet/more_config/picodet_r18_640_coco.yml");

	public IEnumerable<RawOnlineModel> All
	{
		get
		{
			yield return S_320_coco;
			yield return S_416_coco;
			yield return M_320_coco;
			yield return M_416_coco;
			yield return L_320_coco;
			yield return L_416_coco;
			yield return L_640_coco;
			yield return Shufflenetv2_1x_416_coco;
			yield return Mobilenetv3_large_1x_416_coco;
			yield return Lcnet_1_5x_416_coco;
			yield return Lcnet_1_5x_640_coco;
			yield return R18_640_coco;
		}
	}
}

public class PPYoloRawModels
{
	public readonly RawOnlineModel R50vd_dcn_1x_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_r50vd_dcn_1x_coco.pdparams", "./configs/ppyolo/ppyolo_r50vd_dcn_1x_coco.yml");
	public readonly RawOnlineModel R50vd_dcn_2x_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_r50vd_dcn_2x_coco.pdparams", "./configs/ppyolo/ppyolo_r50vd_dcn_2x_coco.yml");
	public readonly RawOnlineModel R18vd_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_r18vd_coco.pdparams", "./configs/ppyolo/ppyolo_r18vd_coco.yml");
	public readonly RawOnlineModel V2_r50vd_dcn_365e_coco = new("https://paddledet.bj.bcebos.com/models/ppyolov2_r50vd_dcn_365e_coco.pdparams", "./configs/ppyolo/ppyolov2_r50vd_dcn_365e_coco.yml");
	public readonly RawOnlineModel V2_r101vd_dcn_365e_coco = new("https://paddledet.bj.bcebos.com/models/ppyolov2_r101vd_dcn_365e_coco.pdparams", "./configs/ppyolo/ppyolov2_r101vd_dcn_365e_coco.yml");
	public readonly RawOnlineModel Mbv3_large_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_mbv3_large_coco.pdparams", "./configs/ppyolo/ppyolo_mbv3_large_coco.yml");
	public readonly RawOnlineModel Mbv3_small_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_mbv3_small_coco.pdparams", "./configs/ppyolo/ppyolo_mbv3_small_coco.yml");
	public readonly RawOnlineModel Tiny_650e_coco = new("https://paddledet.bj.bcebos.com/models/ppyolo_tiny_650e_coco.pdparams", "./configs/ppyolo/ppyolo_tiny_650e_coco.yml");
	public readonly RawOnlineModel R50vd_dcn_voc = new("https://paddledet.bj.bcebos.com/models/ppyolo_r50vd_dcn_voc.pdparams", "./configs/ppyolo/ppyolo_r50vd_dcn_voc.yml");

	public IEnumerable<RawOnlineModel> All
	{
		get
		{
			yield return R50vd_dcn_1x_coco;
			yield return R50vd_dcn_2x_coco;
			yield return R18vd_coco;
			yield return V2_r50vd_dcn_365e_coco;
			yield return V2_r101vd_dcn_365e_coco;
			yield return Mbv3_large_coco;
			yield return Mbv3_small_coco;
			yield return Tiny_650e_coco;
			yield return R50vd_dcn_voc;
		}
	}
}

public record RawLocalModel(string Name, string ParamsPath, string YmlRelatedPath)
{
	public DetectionLocalModel ExportToInferenceModel(string paddleDetectionPath, string outputDir)
	{
		DetectionLocalModel result = DryRunGetInferenceModel(outputDir);
		if (!result.IsValid())
		{
			string exportModelScript = Path.Combine(paddleDetectionPath, "tools", "export_model.py");
			string ymlPath = Path.Combine(paddleDetectionPath, YmlRelatedPath);
			string arguments = $"{exportModelScript} -c {ymlPath} -o weights={ParamsPath} --output_dir {outputDir}";
			Util.Metatext($"python {arguments}").Dump();
			Util.Cmd("python", arguments);
		}
		return result;
	}

	public DetectionLocalModel DryRunGetInferenceModel(string outputDir)
	{
		string destinationDir = Path.Combine(outputDir, Name);
		return new(Name, destinationDir);
	}

	public static void ChangeCodeToUseCPU(string paddleDetectionPath)
	{
		string configPath = Path.Combine(paddleDetectionPath, "configs", "runtime.yml");
		string config = File.ReadAllText(configPath);
		File.WriteAllText(configPath, config.Replace("use_gpu: true", "use_gpu: false"));
	}
}
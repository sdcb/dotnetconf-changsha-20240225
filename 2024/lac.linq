<Query Kind="Statements">
  <NuGetReference>Sdcb.PaddleInference</NuGetReference>
  <NuGetReference>Sdcb.PaddleInference.runtime.win64.mkl</NuGetReference>
  <Namespace>Sdcb.PaddleInference</Namespace>
  <Namespace>Sdcb.PaddleInference.Native</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
</Query>

string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".paddlenlp\taskflow\lac");
Dictionary<string, long> tokenMap = File.ReadLines(Path.Combine(baseFolder, "word.dic"))
	.Select(x => x.Split("\t"))
	.ToDictionary(k => k[1], v => long.Parse(v[0]));
Dictionary<string, string> q2bMap = File.ReadLines(Path.Combine(baseFolder, "q2b.dic"))
	.Select(x => x.Split("\t"))
	.ToDictionary(k => k[0], v => v[1]);
string[] inputs = 
[
	"第十四届全运会在西安举办",
	"我是中国人，我爱中国"
];

int maxLength = inputs.Max(x => x.Length);
long[] tokens = inputs
	.Select(input => input
		.Select(c => tokenMap[q2bMap.GetValueOrDefault(c.ToString(), c.ToString())])
		.ToArray())
	.Aggregate(Enumerable.Empty<long>(), (a, b) => a.Concat([..b, ..new long[maxLength - b.Length]]))
	.ToArray();
using PaddlePredictor pred = PaddleConfig.FromModelDir(Path.Combine(baseFolder, "static")).CreatePredictor();
pred.GetInputTensor("token_ids").Shape = new[] { inputs.Length, maxLength };
pred.GetInputTensor("token_ids").SetData(tokens);
pred.GetInputTensor("length").Shape = new[] { inputs.Length };
pred.GetInputTensor("length").SetData(inputs.Select(x => (long)x.Length).ToArray());
pred.Run();
long[] resultTokens = pred.GetOutputTensor(pred.OutputNames[0]).GetData<long>().ToArray();
Dictionary<long, string> tagMap = File.ReadLines(Path.Combine(baseFolder, "tag.dic"))
	.Select(x => x.Split("\t"))
	.GroupBy(x => long.Parse(x[0]))
	.ToDictionary(k => k.Key, v => v.Last()[1]);
string[][] tags = resultTokens
	.Chunk(maxLength)
	.Select(x => x.Select(v => tagMap[v]).ToArray())
	.ToArray();

tags.Select((x, i) => ToSentOut(tags[i], inputs[i])).Dump();

static List<string> ToSentOut(string[] tags, string input)
{
	List<string> sentOut = new List<string>();
	List<string> tagsOut = new List<string>();
	string partialWord = String.Empty;

	for (int ind = 0; ind < input.Length; ind++)
	{
		string tag = tags[ind];
		char c = input[ind];

		if (String.IsNullOrEmpty(partialWord))
		{
			partialWord = c.ToString();
			tagsOut.Add(tag.Split('-')[0]);
			continue;
		}

		if (tag.EndsWith("-B") || (tag == "O" && tags[ind - 1] != "O"))
		{
			sentOut.Add(partialWord);
			tagsOut.Add(tag.Split('-')[0]);
			partialWord = c.ToString();
		}
		else
		{
			partialWord += c;
		}
	}
	if (!String.IsNullOrEmpty(partialWord))
	{
		sentOut.Add(partialWord);
	}
	
	return sentOut;
}
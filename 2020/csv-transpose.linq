<Query Kind="Program">
  <Namespace>System.Net.Http</Namespace>
</Query>

void Main()
{
	Transpose(ReadCsv(GetCsv())).Dump();
}

string[][] Transpose(IEnumerable<string[]> input)
{
	string[][] data = input.ToArray();
	var result = new string[data[0].Length][];
	for (var y = 0; y < data[0].Length; ++y)
	{
		result[y] = new string[data.Length];
		for (var x = 0; x < data.Length; ++x)
		{
			result[y][x] = data[x][y];
		}
	}

	return result;
}

IEnumerable<string[]> ReadCsv(Stream stream)
{
	using var r = new StreamReader(stream);
	while (true)
	{
		var line = r.ReadLine();
		if (line == null) yield break;
		yield return SplitCsv(line);
	}
}

string[] SplitCsv(string line, char quote = '\"', char delimitor = ',')
{
	List<string> result = new List<string>();
	StringBuilder currentStr = new StringBuilder("");
	bool inQuotes = false;
	for (int i = 0; i < line.Length; i++) // For each character
	{
		if (line[i] == quote) // Quotes are closing or opening
			inQuotes = !inQuotes;
		else if (line[i] == delimitor) // Comma
		{
			if (!inQuotes) // If not in quotes, end of current string, add it to result
			{
				result.Add(currentStr.ToString());
				currentStr.Clear();
			}
			else
				currentStr.Append(line[i]); // If in quotes, just add it 
		}
		else // Add any other character to current string
			currentStr.Append(line[i]);
	}
	result.Add(currentStr.ToString());
	return result.ToArray(); // Return array of all strings
}

Stream GetCsv()
{
	using var http = new HttpClient();
	var resp = http.Send(new HttpRequestMessage
	{
		Method = HttpMethod.Get,
		//RequestUri = new Uri("https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_confirmed_global.csv"),
		RequestUri = new Uri("https://io.starworks.cc:88/cv-public/2024/time_series_covid19_confirmed_global.csv"),
	});
	return resp.Content.ReadAsStream();
}
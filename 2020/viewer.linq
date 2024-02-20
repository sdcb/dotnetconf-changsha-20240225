<Query Kind="Statements">
  <NuGetReference Version="2.0.2">FlysEngine.Desktop</NuGetReference>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>System.Drawing</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Vortice.DirectWrite</Namespace>
</Query>

#load ".\csv-transpose"
//#load ".\analyse"

// https://www.zhihu.com/zvideo/1317745169014325248
var manager = new ViewManager(Util.Cache(() => GetData()));
ColorSource colorSource = new();
using RenderWindow form = new() { Width = 1024, Height = 768 };
form.UpdateLogic += (w, dt) => manager.Update(dt);
form.ReleaseDeviceSizeResources += o => form.XResource.TextFormats.Dispose();
form.Draw += (o, ctx) =>
{
	ctx.Clear(Color4.Gray);

	EntryViewSnapshot[] view = manager.GetView();
	float max = view[0].DisplayValue;
	float targetAxisValue = MathF.Log10(max);

	float itemHeight = (ctx.Size.Height / ViewManager.SlotCount / 1.5f);
	for (var i = 0; i < view.Length; ++i)
	{
		EntryViewSnapshot area = view[i];

		var location = new RectangleF(50, 20 + (itemHeight * 1.5f) * area.DisplayRank, (ctx.Size.Width - 100) * area.DisplayValue / max, itemHeight);
		Color4 color = colorSource[area.Item.Title];
		var format = o.XResource.TextFormats[itemHeight / 2];
		format.TextAlignment = TextAlignment.Leading;
		ctx.FillRectangle(location, o.XResource.GetColor(color));
		ctx.DrawText(
			area.ToString(),
			o.XResource.TextFormats[itemHeight / 2],
			new RectangleF(location.Left, location.Top, 1000, 100),
			o.XResource.GetColor(GetContrastingColor(color)));
	}

	{
		var fontSize = itemHeight * 2;
		var format = o.XResource.TextFormats[fontSize];
		format.TextAlignment = Vortice.DirectWrite.TextAlignment.Trailing;
		ctx.DrawText(
			manager.Date.ToString("yyyy-MM-dd"),
			o.XResource.TextFormats[fontSize],
			new RectangleF(0, ctx.Size.Height - fontSize, ctx.Size.Width, ctx.Size.Height),
			o.XResource.GetColor(Color4.White));
	}
};
RenderLoop.Run(form, () => form.Render(1, Vortice.DXGI.PresentFlags.None));

DateSnapshot[] GetData() => GetCountryDataView()
	.Select(x => new DateSnapshot
	{
		Date = x.Key,
		AreaDatas = x.Value
			.OrderByDescending(v => v.Value)
			.Select(x => new DateSnapshotItem
			{
				Value = x.Value,
				Title = x.Key,
			})
			.ToArray()
	})
	.OrderBy(x => x.Date)
	.ToArray();
	
Dictionary<DateTime, Dictionary<string, int>> GetCountryDataView()
{
	string[][] data = Transpose(ReadCsv(GetCsv()));
	Province[] provinces = LoadProvinceDatas(data);

	Dictionary<string, Dictionary<DateTime, int>> countryDataView = provinces
		.Where(x => x.Country != null)
		.GroupBy(x => x.Country)
		.ToDictionary(k => k.Key, v =>
		{
			var r = new Dictionary<DateTime, int>();
			foreach (var province in v)
			{
				foreach (var dateNumber in province.DateNumbers)
				{
					if (!r.ContainsKey(dateNumber.Key))
					{
						r.Add(dateNumber.Key, dateNumber.Value);
					}
					else
					{
						r[dateNumber.Key] += dateNumber.Value;
					}
				}
			}
			return r;
		});

	return countryDataView.First().Value.Keys
		.ToDictionary(k => k, date => countryDataView.ToDictionary(k => k.Key, v => v.Value[date]));

	static Province[] LoadProvinceDatas(string[][] data)
	{
		Province[] provinces = data[0].Skip(1).Select(x => new Province
		{
			Name = x,
		})
		.ToArray();

		{
			// Load countries
			string[] countries = data[1].Skip(1).ToArray();
			for (var i = 0; i < provinces.Length; ++i)
			{
				provinces[i].Country = countries[i];
			}
		}

		{
			// Load LatLng
			float[] lats = data[2].Skip(1).Select(ParseFloat).ToArray();
			float[] longs = data[3].Skip(1).Select(ParseFloat).ToArray();
			for (var i = 0; i < provinces.Length; ++i)
			{
				provinces[i].Lat = lats[i];
				provinces[i].Long = longs[i];
			}

			static float ParseFloat(string x)
			{
				float.TryParse(x, out var f);
				return f;
			}
		}

		// Read date numbers
		foreach (var dateData in data.Skip(4))
		{
			DateTime date = DateTime.ParseExact(dateData[0], "M/d/yy", null);
			for (var i = 0; i < dateData.Length - 1; ++i)
			{
				provinces[i].DateNumbers[date] = int.Parse(dateData[i + 1]);
			}
		}
		return provinces;
	}
}

// 生成对比色
static Color4 GetContrastingColor(Color4 original)
{
	// 评估原始颜色的亮度
	float luminance = 0.2126f * original.R + 0.7152f * original.G + 0.0722f * original.B;

	// 亮度高于中等水平，则生成较暗的颜色；否则生成较亮的颜色
	if (luminance > 0.5f)
	{
		return new Color4(0f, 0f, 0f, original.A); // 较暗的颜色
	}
	else
	{
		return new Color4(1f, 1f, 1f, original.A); // 较亮的颜色
	}
}

public class Province
{
	public string Name { get; set; }
	public string Country { get; set; }
	public float Lat { get; set; }
	public float Long { get; set; }
	public Dictionary<DateTime, int> DateNumbers { get; set; } = new();
}

public class ColorSource
{
	Dictionary<string, Color4> _colorMap = new();
	Color4[] _colors;

	public ColorSource(int maxColor = 800)
	{
		_colors = GenerateColorMapToScalar()
			.Skip(1)
			.Take(maxColor).ToArray();
	}

	static IEnumerable<Color4> GenerateColorMapToScalar()
	{
		for (int i = 0; ; ++i)
		{
			int j = 0;
			int lab = i;
			int r = 0, g = 0, b = 0;
			while (lab != 0)
			{
				r |= (((lab >> 0) & 1) << (7 - j));
				g |= (((lab >> 1) & 1) << (7 - j));
				b |= (((lab >> 2) & 1) << (7 - j));
				++j;
				lab >>= 3;
			}

			yield return new Color4(r / 256.0f, g / 256.0f, b / 256.0f);
		}
	}

	public Color4 this[string title]
	{
		get
		{
			if (_colorMap.TryGetValue(title, out Color4 val))
			{
				return val;
			}
			else
			{
				val = _colors[_colorMap.Count];
				_colorMap[title] = val;
				return val;
			}
		}
	}
}

public class ViewManager
{
	public const int SlotCount = 10;
	public const float DateInterval = 0.2f;

	readonly DateSnapshot[] _data;
	readonly Dictionary<string, OldItemStatus> _rankCache;

	int _id = 0;
	float _accumulateDt = 0;
	DumpContainer _dc = new DumpContainer().Dump();

	private int GetNextId() => Math.Clamp(_id + 1, _id, _data.Length - 1);
	private int GetPrevId() => Math.Clamp(_id - 1, _id, _data.Length - 1);

	public ViewManager(DateSnapshot[] data)
	{
		_data = data;
		int rank = 0;
		_rankCache = _data[0].AreaDatas.ToDictionary(k => k.Title, v => new OldItemStatus
		{
			FromRank = rank++,
			FromValue = v.Value,
		});
	}

	public DateTime Date => _data[_id].Date;
	public DateSnapshot Snapshot => _data[_id];

	public void Update(float dt)
	{
		_accumulateDt += dt;
		if (_accumulateDt > DateInterval)
		{
			// Set to next day
			_accumulateDt -= DateInterval;
			DateSnapshotItem[] datas = Snapshot.AreaDatas;
			for (var i = 0; i < datas.Length; ++i)
			{
				OldItemStatus old = _rankCache[datas[i].Title];
				old.FromRank = i;
				old.FromValue = datas[i].Value;
			}
			_id = GetNextId();
		}
	}

	public EntryViewSnapshot[] GetView()
	{
		return Snapshot.AreaDatas
			.Take(SlotCount)
			.Select((x, i) =>
			{
				OldItemStatus old = _rankCache[x.Title];
				int fromRank = old.FromRank;
				int toRank = i;
				int fromValue = old.FromValue;
				int toValue = x.Value;
				//_dc.Content = new { rank = new { from = fromRank, to = toRank }, value = new { from = fromValue, to = toValue }};

				return new EntryViewSnapshot
				{
					Item = x,
					DisplayRank = fromRank + 1.0f * (toRank - fromRank) * _accumulateDt / DateInterval,
					DisplayValue = fromValue + 1.0f * (toValue - fromValue) * _accumulateDt / DateInterval,
				};
			})
			.ToArray();
	}
}

public class OldItemStatus
{
	public int FromRank;
	public int FromValue;
}

public class EntryViewSnapshot
{
	public DateSnapshotItem Item { get; init; }
	public float DisplayRank { get; set; }
	public float DisplayValue { get; set; }

	public override string ToString() => $"{Item.Title}: {DisplayValue:N0}";
}

[Serializable]
public class DateSnapshot
{
	public DateTime Date { get; init; }
	public DateSnapshotItem[] AreaDatas { get; init; }
}

[Serializable]
public class DateSnapshotItem
{
	public string Title { get; init; }
	public int Value { get; init; }
}
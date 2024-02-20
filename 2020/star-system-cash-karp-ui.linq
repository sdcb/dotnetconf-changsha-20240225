<Query Kind="Program">
  <NuGetReference>FlysEngine.Desktop</NuGetReference>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DXGI</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Vortice.UIAnimation</Namespace>
</Query>

#load ".\star-system-cash-karp"

void Main()
{
	//StarSystem _sys = StarSystem.Create3Body("Eight");
	//StarSystem _sys = StarSystem.Create3Body("Moth1");
	//StarSystem _sys = StarSystem.Create3Body("Moth2");
	//StarSystem _sys = StarSystem.Create3Body("Dragonfly");
	StarSystem _sys = StarSystem.Create3Body("VI14.A");
	//StarSystem _sys = StarSystem.Create3Body("Henon42");
	//StarSystem _sys = StarSystem.CreateNSystem(3, precision: 1024);
	using StarWindow sw = new(_sys.AutoStep(boundedCapacity: 512, QueryCancelToken))
	{
		StartPosition = FormStartPosition.CenterScreen,
		Size = new System.Drawing.Size(1024, 768),
		Text = "星体运动模拟"
	};
	QueryCancelToken.Register(() => sw.Close());
	RenderLoop.Run(sw, () => sw.Render(1, PresentFlags.None));
}

class StarWindow : RenderWindow
{
	IEnumerator<StarSystemSnapshot> _system;
	StarSystemSnapshot _lastSnapshot;
	StarUIProps[] _uiProps;
	float _speed = 1.0f, _acc = 0;
	IUIAnimationVariable2 _scale;
	const float RefDt = 0.01f;
	ID2D1StrokeStyle1 _stroke;
	ID2D1Bitmap1? _bitmap;
	ID2D1Effect? _effect;

	public StarWindow(IEnumerable<StarSystemSnapshot> sys)
	{
		_system = sys.GetEnumerator();
		_system.MoveNext();
		_lastSnapshot = _system.Current;
		_uiProps = new StarUIProps[_lastSnapshot.Stars.Length];
		foreach ((Color4 color, int i) in GenerateColorMap().Take(_uiProps.Length).Select((color, i) => (color, i)))
		{
			_uiProps[i] = new();
			_uiProps[i].Color = color;
		}
		_scale = XResource.CreateAnimation(ClientSize.Height, ClientSize.Height, 0);
		_stroke = XResource.Direct2DFactory.CreateStrokeStyle(new StrokeStyleProperties1 { StartCap = CapStyle.Triangle, EndCap = CapStyle.Triangle });
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		double finalValue = _scale.Value * (1 + 0.2f * e.Delta / 120);
		using IUIAnimationTransition2 transition = XResource.TransitionLibrary.CreateAccelerateDecelerateTransition(0.25f, finalValue, 0.2, 0.8);
		XResource.Animation.ScheduleTransition(_scale, transition, XResource.DurationSinceStart.TotalSeconds);
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Up) _speed *= 1.2f;
		else if (e.KeyCode == Keys.Down) _speed /= 1.2f;
	}

	protected override void OnCreateDeviceResources()
	{
		_effect = new ID2D1Effect(XResource.RenderTarget.CreateEffect(EffectGuids.GaussianBlur));
	}

	protected override void OnReleaseDeviceResources()
	{
		_effect?.Dispose(); _effect = null;
	}

	protected override void OnCreateDeviceSizeResources()
	{
		ID2D1DeviceContext dc = XResource.RenderTarget;
		_bitmap = dc.CreateBitmap(new SizeI(ClientSize.Width, ClientSize.Height), new BitmapProperties1
		{
			BitmapOptions = BitmapOptions.Target,
			PixelFormat = new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
			DpiX = 96,
			DpiY = 96
		});
	}

	protected override void OnReleaseDeviceSizeResources()
	{
		_bitmap?.Dispose(); _bitmap = null;
	}

	protected override void OnUpdateLogic(float dt)
	{
		float unit = _speed * RefDt;

		try
		{
			while (_acc < unit)
			{
				_system.MoveNext();
				_acc += _system.Current.Timestamp - _lastSnapshot.Timestamp;

				for (int i = 0; i < _lastSnapshot.Stars.Length; ++i)
				{
					StarSnapshot star = _lastSnapshot.Stars[i];
					StarUIProps props = _uiProps[i];

					Vector2 now = new(star.Px, star.Py);
					if (props.TrackingHistory.First != null)
					{
						Vector2 old = props.TrackingHistory.First.Value;
						float dist = Vector2.Distance(old, now);
						if (dist > 2 / _scale.Value)
						{
							props.TrackingHistory.Add(now);
						}
					}
					else
					{
						props.TrackingHistory.Add(now);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
		}

		if (_acc >= unit) _acc -= unit;

		_lastSnapshot = _system.Current;
	}

	protected override void OnDraw(ID2D1DeviceContext ctx)
	{
		//ctx.Clear(new Color4(0.05f));
		//DrawCore(ctx);
		using ID2D1Image oldBmp = ctx.Target;
		ctx.Target = _bitmap;
		DrawCore(ctx);
		ctx.Transform = Matrix3x2.Identity;

		ctx.Target = oldBmp;
		ctx.Clear(new Color4(0.05f));
		ctx.UnitMode = UnitMode.Pixels;
		_effect!.SetInput(0, _bitmap, invalidate: true);
		_effect!.SetValue((int)GaussianBlurProperties.StandardDeviation, 15.0f);

		ctx.DrawImage(_effect!);
		ctx.DrawImage(_bitmap!);
		ctx.UnitMode = UnitMode.Dips;
	}

	private void DrawCore(ID2D1DeviceContext ctx)
	{
		ctx.Clear(Colors.Transparent);

		float allHeight = ctx.Size.Height;
		float allWidth = ctx.Size.Width;
		ctx.Transform =
			Matrix3x2.CreateTranslation(allWidth / (float)_scale.Value * 0.5f, allHeight / (float)_scale.Value * 0.5f) *
			Matrix3x2.CreateScale((float)_scale.Value, (float)_scale.Value);

		for (int i = 0; i < _lastSnapshot.Stars.Length; ++i)
		{
			StarSnapshot star = _lastSnapshot.Stars[i];
			StarUIProps prop = _uiProps[i];

			prop.TrackingHistory.Enumerate2((Vector2 from, Vector2 to, int i) =>
			{
				float alpha = 1.0f * i / (prop.TrackingHistory.Count - 1);
				Color4 color = new Color4(prop.Color.R, prop.Color.G, prop.Color.B, alpha);
				ctx.DrawLine(from, to, XResource.GetColor(color), 0.02f);
			});
		}

		for (int i = 0; i < _lastSnapshot.Stars.Length; ++i)
		{
			StarSnapshot star = _lastSnapshot.Stars[i];
			StarUIProps prop = _uiProps[i];
			using ID2D1GradientStopCollection collection = ctx.CreateGradientStopCollection(new[]
			{
				new GradientStop{ Color = Colors.White, Position = 0 },
				new GradientStop{ Color = star.StarType switch
				{
					StarType.Solar => prop.Color,
					StarType.BlackHole => Colors.Black,
					_ => Colors.Gray,
				}, Position = 1 },
			});
			using ID2D1RadialGradientBrush radialBrush = ctx.CreateRadialGradientBrush(new RadialGradientBrushProperties
			{
				Center = new Vector2(star.Px, star.Py),
				RadiusX = star.Size,
				RadiusY = star.Size,
			}, collection);

			ctx.FillEllipse(new Ellipse(new Vector2(star.Px, star.Py), star.Size, star.Size), radialBrush);
		}
	}

	protected override void OnClosed(EventArgs e) => _system.Dispose();
}

record StarUIProps
{
	public CircularList<Vector2> TrackingHistory { get; } = new(capacity: 10000);
	public Color4 Color { get; set; }
}

static IEnumerable<Color4> GenerateColorMap()
{
	for (int i = 1; ; ++i)
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

		yield return new Color(r, g, b).ToColor4();
	}
}

class CircularList<T> where T : struct
{
	T[] _data;
	int _tail;

	public CircularList(int capacity)
	{
		_data = new T[capacity];
	}

	public int Count { get; private set; }

	private int NextIndex(int id) => (id + 1) % _data.Length;

	public void Add(T val)
	{
		_data[_tail] = val;
		_tail = NextIndex(_tail);
		Count = Math.Min(_data.Length, Count + 1);
	}

	private int Head => (_tail - Count) switch
	{
		< 0 => _tail - Count + _data.Length,
		{ } x => x
	};

	public void Enumerate(Action<T, int> enumerator)
	{
		int head = Head;
		for (int i = 0; i < Count; ++i)
		{
			enumerator(_data[(head + i) % _data.Length], i);
		}
	}

	public void Enumerate2(Action<T, T, int> enumerator)
	{
		int head = Head;
		for (int i = 0; i < Count - 1; ++i)
		{
			enumerator(_data[(head + i) % _data.Length], _data[(head + i + 1) % _data.Length], i);
		}
	}

	public T? First => Count > 0 ? _data[Head] : null;
}
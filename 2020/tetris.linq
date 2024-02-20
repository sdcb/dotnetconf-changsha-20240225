<Query Kind="Program">
  <NuGetReference>FlysEngine.Desktop</NuGetReference>
  <Namespace>FlysEngine</Namespace>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DXGI</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
  <Namespace>Vortice.DirectInput</Namespace>
  <Namespace>System.ComponentModel</Namespace>
</Query>

#nullable enable
void Main()
{
	using Game g = new();
	RenderLoop.Run(g, () => g.Render(1, PresentFlags.None));
}

void Main1()
{
	// simulation
	var m = new Map();
	m.Current.Block.GetData().Dump();
	for (int i = 0; i < 300; ++i)
	{
		StepResult r = m.StepDirection();
		Util.ClearResults();
		new
		{
			i,
			Map = Map.Combine(m.World, m.Current)
		}.Dump();
		Thread.Sleep(20);
		if (r == StepResult.GameOver) break;
	}
	m.World.Dump();
}

void Main2()
{
	var m = new Map();
	var i = new DeviceInput();
	var dc = new DumpContainer().Dump();
	while (!QueryCancelToken.IsCancellationRequested)
	{
		StepResult? r = null;
		i.UpdateAll();
		if (i.KeyboardState.IsPressed(Key.Left))
			r = m.StepDirection(Direction.Left);
		else if (i.KeyboardState.IsPressed(Key.Right))
			r = m.StepDirection(Direction.Right);
		else if (i.KeyboardState.IsPressed(Key.Down))
			r = m.StepDirection(Direction.Down);
		else if (i.KeyboardState.IsPressed(Key.R))
			r = m.StepRotation();
		else if (i.KeyboardState.IsPressed(Key.Up))
			r = m.StepLanding();
		dc.Content = Map.Combine(m.World, m.Current);
		if (r == StepResult.GameOver) break;
		Thread.Sleep(100);
	}
}

void Main3()
{
	// DetectRowsToDelete
	var world = new WorldCell[4, 2];
	world[0, 0].On = true; world[0, 1].On = true;
	world[1, 0].On = true;
	world[2, 0].On = true;
	world[3, 0].On = true; world[3, 1].On = true;
	new { world, RowsToDelete = Map.DetectRowsToDelete(world) }.Dump();
}

void Main4()
{
	// DeleteRows
	var world = new WorldCell[4, 2];
	world[0, 0].On = true; world[0, 1].On = true;
	world[1, 0].On = true;
	world[2, 0].On = true; world[2, 1].On = true;
	world[3, 0].On = true; world[3, 1].On = true;
	new { world, Deleted = Map.DeleteRows(world, Map.DetectRowsToDelete(world)) }.Dump();
}

class Game : RenderWindow
{
	Map _map = new();
	Input _input = new();
	BufferTimer _stepTicker = new();

	float UnitSize => XResource.RenderTarget.Size.Height / _map.WorldSize.Height;
	float NextUnitSize => UnitSize * 0.5f;
	float StrokeSize => UnitSize / 20; // default: 2
	
	Vector2 GetMapTopLeft()
	{
		Size size = XResource.RenderTarget.Size;
		float unitSize = UnitSize;

		float width = unitSize * _map.WorldSize.Width;
		float height = unitSize * _map.WorldSize.Height;
		return new Vector2(0, (size.Height - height) / 2);
	}

	Vector2 GetNextTopLeft()
	{
		Size size = XResource.RenderTarget.Size;
		float unitSize = UnitSize;

		float width = unitSize * _map.WorldSize.Width;
		float height = unitSize * _map.WorldSize.Height;
		return new Vector2(width + (size.Width - width) / 2 - NextUnitSize * 2, size.Height / 8);
	}

	public Game()
	{
		Text = "俄罗斯方块";
		Load += delegate { ClientSize = new System.Drawing.Size(600, 800); };
	}

	float IntervalTime => _map.Score switch
	{
		<= 100 => 0.5f,
		<= 500 => 0.4f,
		<= 1000 => 0.3f,
		<= 2000 => 0.25f,
		_ => 0.2f,
	};

	protected override void OnUpdateLogic(float dt)
	{
		_input.UpdateContinues(dt, _map);
		if (!Focused) _input.ClearContinues();

		_stepTicker.Tick(dt, IntervalTime, () =>
		{
			if (_map.StepDirection(Direction.Down) == StepResult.GameOver)
			{
				MessageBox.Show($"游戏结束, 得分: {_map.Score}");
				_map = new Map();
			}
		});
	}

	protected override void OnKeyDown(KeyEventArgs e) => _input.UpdateImmediate(e.KeyCode, _map);

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		_input.Dispose();
	}

	protected override void OnDraw(ID2D1DeviceContext ctx)
	{
		ctx.Clear(Colors.Gray);
		float stroke = StrokeSize;
		float unitSize = UnitSize;

		// map area
		ctx.Transform = Matrix3x2.CreateTranslation(GetMapTopLeft());
		Size mapSize = new Size(_map.WorldSize.Width * unitSize, _map.WorldSize.Height * unitSize);
		{
			// border
			ctx.DrawRectangle(new Rect(Vector2.Zero, mapSize), C(Colors.White), stroke);

			if (_input.Device.KeyboardState.PressedKeys.Contains(Key.LeftShift))
			{
				// grid columns
				for (int x = 1; x < _map.WorldSize.Width; ++x)
				{
					float column = x * unitSize;
					ctx.DrawLine(new Vector2(column, 0), new Vector2(column, mapSize.Height), C(Colors.Blue), unitSize / 80);
				}
				// grid rows
				for (int y = 0; y < _map.WorldSize.Height; ++y)
				{
					float row = y * unitSize;
					ctx.DrawLine(new Vector2(0, row), new Vector2(mapSize.Width, row), C(Colors.Blue), unitSize / 80);
				}
			}

			// world
			DrawBlock(ctx, _map.World, unitSize);

			// before landing
			DrawBlock(ctx, _map.GetBlockBeforeLanding(), unitSize, Colors.DarkGray);

			// current
			DrawBlock(ctx, _map.Current, unitSize);
		}

		// score
		ctx.Transform = Matrix3x2.CreateTranslation(GetMapTopLeft() + new Vector2(mapSize.Width + unitSize / 10, 0));
		ctx.DrawText($"得分：{_map.Score}", XResource.TextFormats[unitSize / 2], new Rect(Vector2.Zero, ctx.Size - mapSize), C(Colors.White));

		// next
		ctx.Transform = Matrix3x2.CreateTranslation(GetNextTopLeft());
		DrawBlock(ctx, _map.Next, NextUnitSize);
	}

	ID2D1Brush C(Color4 color) => XResource.GetColor(color);
	void DrawBlock(ID2D1DeviceContext ctx, MapBlock block, float unitSize, Color4? color = null) => DrawBlock(ctx, block.Block.GetData(), block.Pos, unitSize, color ?? block.Block.RawData.Color);
	void DrawBlock(ID2D1DeviceContext ctx, Block block, float unitSize) => DrawBlock(ctx, block.GetData(), Int2.Zero, unitSize, block.RawData.Color);
	void DrawBlock(ID2D1DeviceContext ctx, WorldCell[,] data, float unitSize)
	{
		int height = data.GetLength(0), width = data.GetLength(1);
		float displayUnitSize = unitSize * 0.95f;

		for (int y = 0; y < height; ++y)
			for (int x = 0; x < width; ++x)
			{
				if (data[y, x].On)
				{
					ctx.FillRectangle(new Rect(x * unitSize, y * unitSize, displayUnitSize, displayUnitSize), C(data[y, x].Color));
				}
			}
	}
	void DrawBlock(ID2D1DeviceContext ctx, bool[,] data, Int2 pos, float unitSize, Color4 color)
	{
		int height = data.GetLength(0), width = data.GetLength(1);
		float displayUnitSize = unitSize * 0.95f;

		for (int y = 0; y < height; ++y)
			for (int x = 0; x < width; ++x)
			{
				if (data[y, x])
				{
					ctx.FillRectangle(new Rect((x + pos.X) * unitSize, (y + pos.Y) * unitSize, displayUnitSize, displayUnitSize), C(color));
				}
			}
	}
}

class Input : IDisposable
{
	BufferTimer _effectTimer = new();
	public DeviceInput Device = new();
	InputInstruction? immediateInput = null;
	List<InputInstruction> continuesInput = new();

	public void ClearContinues() => continuesInput.Clear();

	public void UpdateContinues(float dt, Map map)
	{
		Device.UpdateKeyboard();
		continuesInput = InputInstruction.FromContinuesKeyboard(Device.KeyboardState.PressedKeys) switch
		{
			{ Count: > 0 } x => x,
			_ => continuesInput,
		};

		_effectTimer.Tick(dt, 0.1f, () =>
		{
			foreach (InputInstruction i in continuesInput) i.ApplyToMap(map);
			ClearContinues();
		});
	}

	public void UpdateImmediate(Keys key, Map map)
	{
		immediateInput = InputInstruction.FromImmediateEvent(key);
		if (immediateInput == null) return;
		immediateInput.ApplyToMap(map);
	}

	public void Dispose() => Device.Dispose();
}

record InputInstruction(Direction Direction = default, bool IsLanding = false, bool IsRotation = false)
{
	public static List<InputInstruction> FromContinuesKeyboard(List<Key> pressedKeys) => pressedKeys
		.Select(key => key switch
		{
			Key.Down => CreateDirection(Direction.Down),
			_ => null
		})
		.Where(x => x != null)
		.ToList()!;

	public static InputInstruction? FromImmediateEvent(Keys key) => key switch
	{
		Keys.Space => CreateRotation(),
		Keys.Up => CreateLanding(),
		Keys.Left => CreateDirection(Direction.Left),
		Keys.Right => CreateDirection(Direction.Right),
		_ => null,
	};

	public StepResult ApplyToMap(Map map) => this switch
	{
		{ IsLanding: true } => map.StepLanding(),
		{ IsRotation: true } => map.StepRotation(),
		_ => map.StepDirection(Direction),
	};

	public static InputInstruction CreateDirection(Direction direction) => new(direction);
	public static InputInstruction CreateLanding() => new(IsLanding: true);
	public static InputInstruction CreateRotation() => new(IsRotation: true);
}

class BufferTimer
{
	float _total = 0;

	public void Tick(float dt, float bufferTime, Action action)
	{
		_total += dt;
		while (_total > bufferTime)
		{
			action();
			_total -= bufferTime;
		}
	}
}

class Map
{
	public readonly SizeI WorldSize = new SizeI(10, 20);
	public WorldCell[,] World;
	public Block Next;
	public MapBlock Current;
	public int Score = 0;

	public Map()
	{
		World = new WorldCell[WorldSize.Height, WorldSize.Width];
		Current = ToMapCenter(Block.CreateRandom());
		Next = Block.CreateRandom();
		Score = 0;
	}

	public StepResult StepDirection(Direction direction = Direction.Down) => Step(Current.WithDirection(direction), isDown: direction == Direction.Down);
	public StepResult StepRotation() => Step(Current.WithRotation(), isDown: false);
	public StepResult StepLanding() => Land(GetBlockBeforeLanding());

	private StepResult Land(MapBlock block)
	{
		WorldCell[,] newWorld = Combine(World, block);
		int[] rowsToDelete = DetectRowsToDelete(newWorld).ToArray();
		Score += rowsToDelete.Length switch
		{
			1 => 100,
			2 => 250,
			3 => 500,
			4 => 1000,
			_ => 0
		};
		World = DeleteRows(newWorld, rowsToDelete); ;
		Current = ToMapCenter(Next);
		Next = Block.CreateRandom();

		if (block.Pos.Y <= 0) return StepResult.GameOver;
		return StepResult.StepLanded;
	}

	private StepResult Step(MapBlock next, bool isDown)
	{
		BlockInteraction interaction = FindInteraction(World, next);
		if (interaction == BlockInteraction.ByTopBorder)
		{
			return StepResult.GameOver;
		}
		else if (interaction == BlockInteraction.ByBottom || (interaction == BlockInteraction.ByBlock && isDown))
		{
			return Land(Current);
		}
		else if (interaction == BlockInteraction.BySideBorder || (interaction == BlockInteraction.ByBlock && !isDown))
		{
			return StepResult.NoStep;
		}
		else
		{
			Current = next;
			return StepResult.Step;
		}
	}

	MapBlock ToMapCenter(Block block) => new MapBlock(block, new(WorldSize.Width / 2 - 1, -1));

	public MapBlock GetBlockBeforeLanding()
	{
		MapBlock? next = null, prev = Current;
		while (true)
		{
			next = prev.WithDirection(Direction.Down);
			BlockInteraction interaction = Map.FindInteraction(World, next);
			if (interaction == BlockInteraction.ByBottom || interaction == BlockInteraction.ByBlock)
			{
				return prev;
			}
			prev = next;
		}
	}

	internal static BlockInteraction FindInteraction(WorldCell[,] world, MapBlock next)
	{
		bool[,] data = next.Block.GetData();
		Int2 pos = next.Pos;
		int width = next.Block.RawData.Width;
		int maxY = world.GetLength(0), maxX = world.GetLength(1);

		for (int x = 0; x < width; ++x)
		{
			for (int y = 0; y < width; ++y)
			{
				Int2 worldPos = new(pos.X + x, pos.Y + y);
				if (!data[y, x]) continue;
				if (worldPos.Y < 0) continue;

				BlockInteraction? interaction = worldPos switch
				{
					//var p when p.Y < 0 => BlockInteraction.ByTopBorder,
					var p when p.Y >= maxY => BlockInteraction.ByBottom,
					var p when p.X < 0 || p.X >= maxX => BlockInteraction.BySideBorder,
					var p when world[p.Y, p.X].On => pos.Y switch
					{
						< 0 => BlockInteraction.ByTopBorder,
						_ => BlockInteraction.ByBlock,
					},
					_ => null,
				};
				if (interaction != null) return interaction.Value;
			}
		}
		return BlockInteraction.No;
	}

	internal static WorldCell[,] Combine(WorldCell[,] world, MapBlock current)
	{
		bool[,] data = current.Block.GetData();
		Int2 pos = current.Pos;
		int maxY = world.GetLength(0), maxX = world.GetLength(1);
		int width = current.Block.RawData.Width;

		WorldCell[,] newWorld = (WorldCell[,])world.Clone();

		for (int x = 0; x < width; ++x)
		{
			for (int y = 0; y < width; ++y)
			{
				Int2 worldPos = new(pos.X + x, pos.Y + y);
				if (data[y, x] && worldPos.Y >= 0 && worldPos.Y < maxY)
				{
					newWorld[worldPos.Y, worldPos.X] = new (true, current.Block.RawData.Color);
				}
			}
		}

		return newWorld;
	}

	internal static IEnumerable<int> DetectRowsToDelete(WorldCell[,] world)
	{
		int height = world.GetLength(0), width = world.GetLength(1);
		for (int y = 0; y < height; ++y)
		{
			bool yield = true;
			for (int x = 0; x < width; ++x)
			{
				if (!world[y, x].On)
				{
					yield = false;
					break;
				}
			}
			if (yield) yield return y;
		}
	}

	internal static T[,] DeleteRows<T>(T[,] world, IEnumerable<int> rowsToDelete)
	{
		int height = world.GetLength(0), width = world.GetLength(1);
		T[,] result = (T[,])world.Clone();
		foreach (int startY in rowsToDelete)
		{
			for (int y = startY; y >= 0; --y)
			{
				for (int x = 0; x < width; ++x)
				{
					result[y, x] = (y - 1) switch
					{
						>= 0 => result[y - 1, x],
						_ => default!
					};
				}
			}
		}
		return result;
	}
}

record struct WorldCell(bool On, Color4 Color)
{
	bool ToDump() => On;
}

record MapBlock(Block Block, Int2 Pos)
{
	public MapBlock WithDirection(Direction direction)
	{
		Int2 offset = _directionMap[direction];
		return this with { Pos = new(Pos.X + offset.X, Pos.Y + offset.Y) };
	}

	public MapBlock WithRotation() => this with { Block = Block.NextRotation() };

	static Dictionary<Direction, Int2> _directionMap = new()
	{
		[Direction.Left] = new Int2(-1, 0),
		[Direction.Right] = new Int2(1, 0),
		[Direction.Down] = new Int2(0, 1),
	};
}


enum BlockInteraction { No, ByBottom, BySideBorder, ByTopBorder, ByBlock }
enum Direction { Left, Right, Down }
enum StepResult { NoStep, Step, StepLanded, GameOver, Dead, }

record Block(BlockData RawData, int Rotation)
{
	public Block NextRotation() => this with { Rotation = (Rotation + 1) % 4 };

	public bool[,] GetData()
	{
		int width = RawData.Width;
		bool[,] result = (bool[,])RawData.Data.Clone();

		for (int i = 0; i < Rotation; ++i)
		{
			result = Rotate90(result);
		}

		return BringToTopLeft(result);

		static T[,] Rotate90<T>(T[,] oldMatrix)
		{
			T[,] newMatrix = new T[oldMatrix.GetLength(1), oldMatrix.GetLength(0)];
			int newRow = 0;
			for (int oldColumn = oldMatrix.GetLength(1) - 1; oldColumn >= 0; oldColumn--)
			{
				int newColumn = 0;
				for (int oldRow = 0; oldRow < oldMatrix.GetLength(0); oldRow++)
				{
					newMatrix[newRow, newColumn] = oldMatrix[oldRow, oldColumn];
					newColumn++;
				}
				newRow++;
			}
			return newMatrix;
		}

		static bool[,] BringToTopLeft(bool[,] arr)
		{
			int height = arr.GetLength(0), width = arr.GetLength(1);
			bool[,] result = (bool[,])arr.Clone();
			// top direction
			while (R1Empty(result, height))
			{
				// bring to top
				for (int y = 0; y < height; ++y)
					for (int x = 0; x < width; ++x)
						result[y, x] = y + 1 < height ? result[y + 1, x] : false;
			}

			while (C1Empty(result, width))
			{
				for (int y = 0; y < height; ++y)
					for (int x = 0; x < width; ++x)
						result[y, x] = x + 1 < width ? result[y, x + 1] : false;
			}

			return result;

			static bool R1Empty(bool[,] arr, int width)
			{
				bool allEmpty = true;
				for (int i = 0; i < width; ++i)
				{
					if (arr[0, i]) return false;
				}
				return allEmpty;
			}
			static bool C1Empty(bool[,] arr, int height)
			{
				bool allEmpty = true;
				for (int i = 0; i < height; ++i)
				{
					if (arr[i, 0]) return false;
				}
				return allEmpty;
			}
		}
	}

	public static Block CreateRandom() => new Block(BlockData.GetRandom(), Random.Shared.Next(4));
}

record BlockData(Color4 Color, bool[,] Data)
{
	public int Width => Data.GetLength(0);

	public static BlockData FromRaw(Color4 color, string raw)
	{
		int width = Check(Math.Sqrt(raw.Length));
		bool[,] data = Make2DArray(raw.Select(v => v == '*' ? true : false), width, width);

		return new BlockData(color, data);

		static int Check(double v)
		{
			if (v != (int)v) throw new Exception($"bug: 未取整: {v}");
			return (int)v;
		}

		static T[,] Make2DArray<T>(IEnumerable<T> input, int height, int width)
		{
			T[,] output = new T[height, width];
			int x = 0, y = 0;
			foreach (T val in input)
			{
				output[y, x] = val;
				++x;
				if (x == width)
				{
					x = 0;
					++y;
				}
			}
			return output;
		}
	}

	public static BlockData[] All = new[]
	{
		BlockData.FromRaw(Colors.Cyan, "....****........"), // I
		BlockData.FromRaw(Colors.Yellow, "****"),           // O
		BlockData.FromRaw(Colors.Purple, "***.*...."),      // T
		BlockData.FromRaw(Colors.Blue, "*..*..**."),        // L1
		BlockData.FromRaw(Colors.Orange, ".*..*.**."),      // L2
		BlockData.FromRaw(Colors.Green, "**..**..."),       // S1
		BlockData.FromRaw(Colors.Red, ".****...."),         // S2
	};

	public static BlockData GetRandom() => Random.Shared.Next(5) switch
	{
		3 => All[3 + Random.Shared.Next(2)],
		4 => All[5 + Random.Shared.Next(2)],
		var x => All[x],
	};
}
<Query Kind="Program">
  <NuGetReference>FlysEngine.Desktop</NuGetReference>
  <Namespace>FlysEngine</Namespace>
  <Namespace>FlysEngine.Desktop</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Windows.Forms</Namespace>
  <Namespace>Vortice.Direct2D1</Namespace>
  <Namespace>Vortice.DXGI</Namespace>
  <Namespace>Vortice.Mathematics</Namespace>
</Query>

void Main()
{
	using Game g = new();
	RenderLoop.Run(g, () => g.Render(1, PresentFlags.None));
}

static Vector2 NextRandomVector2(int maxX, int maxY) => new Vector2(
		Random.Shared.Next(maxX),
		Random.Shared.Next(maxY));

class Game : RenderWindow
{
	public Game()
	{
		Text = "贪吃蛇";
	}

	protected override void OnLoad(EventArgs e)
	{
		ClientSize = new System.Drawing.Size(1280, 720);
	}

	Map map = new();
	float GetBlockSize() => XResource.RenderTarget.Size.Height / map.Size.Height;
	Vector2 GetMapTopLeft()
	{
		Size size = XResource.RenderTarget.Size;
		float blockSize = GetBlockSize();

		float width = blockSize * map.Size.Width;
		float height = blockSize * map.Size.Height;
		return new Vector2(0, (size.Height - height) / 2);
	}

	AccTimer _tickTimer = new ();

	float Speed => map.Snake.BodyPos.Count switch
	{
		<= 3 => 0.5f,
		<= 4 => 0.4f,
		<= 5 => 0.3f,
		<= 6 => 0.25f,
		<= 7 => 0.2f,
		<= 10 => 0.15f,
		_ => 0.12f
	};

	protected override void OnUpdateLogic(float dt)
	{
		_tickTimer.Tick(dt, Speed, () =>
		{
			map.Step();
			if (!map.Alive)
			{
				MessageBox.Show($"得分：{map.Snake.BodyPos.Count}");
				map.Reset();
			}
		});
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		map.Snake.ChangeDirection(e.KeyCode switch
		{
			Keys.Left => Direction.Left,
			Keys.Right => Direction.Right,
			Keys.Up => Direction.Up,
			Keys.Down => Direction.Down,
			_ => map.Snake.Direction,
		});
	}

	protected override void OnDraw(ID2D1DeviceContext ctx)
	{
		ctx.Clear(Colors.CornflowerBlue);
		Vector2 topLeft = GetMapTopLeft();
		float blockSize = GetBlockSize();

		// food
		ctx.FillEllipse(new Ellipse(topLeft + (map.FoodPos + new Vector2(0.5f)) * blockSize, blockSize * 0.5f, blockSize * 0.5f), XResource.GetColor(Colors.Yellow));

		// snake body
		foreach (Vector2 p in map.Snake.BodyPos)
		{
			ctx.FillRectangle(new Rect(topLeft + p * blockSize, new Size(blockSize * 0.9f)), XResource.GetColor(Colors.Red));
		}
		ctx.FillRectangle(new Rect(topLeft + map.Snake.HeaderPos * blockSize + new Vector2(blockSize * 0.1f), new Size(blockSize * 0.7f)), XResource.GetColor(Colors.DarkRed));

		// score
		ctx.DrawText($"得分：{map.Snake.BodyPos.Count}", XResource.TextFormats[(int)blockSize / 2, "黑体"], new Rect(Vector2.Zero, ctx.Size), XResource.GetColor(Colors.White));
	}
}

record AccTimer
{
	float _acc = 0;

	public void Tick(float dt, float speed, Action action)
	{
		_acc += dt;
		while (_acc > speed)
		{
			action();
			_acc -= speed;
		}
	}
}

class Map
{
	public SizeI Size { get; init; } = new SizeI(16, 9);
	public Vector2 FoodPos { get; private set; }
	public Snake Snake { get; private set; }
	public bool Alive { get; private set; } = true;

	public Map()
	{
		Reset();
	}

	public void CreateSnake(int length = 3) => Snake = new Snake(new LinkedList<Vector2>(Enumerable
		.Range(0, length)
		.Select(x => new Vector2(x, 0))
		.Reverse()));

	public void CreateNextFood()
	{
		Vector2 p = NextRandomVector2(Size.Width, Size.Height);
		while (Snake.BodyPos.Contains(p))
		{
			p = NextRandomVector2(Size.Width, Size.Height);
		}

		FoodPos = p;
	}

	public void Reset()
	{
		CreateSnake();
		CreateNextFood();
		Alive = true;
	}

	public void Step()
	{
		if (!Alive) return;

		Vector2 next = Snake.NextPos;
		if (!IsInside(next))
		{
			Alive = false;
			return;
		}

		if (Snake.BodyPos.Contains(next))
		{
			Alive = false;
			return;
		}

		if (next == FoodPos)
		{
			Snake.Grow();
			CreateNextFood();
		}
		else
		{
			Snake.Go();
		}
	}

	public bool IsInside(Vector2 p) =>
		p.X >= 0 && p.X < Size.Width &&
		p.Y >= 0 && p.Y < Size.Height;
}

record Snake(LinkedList<Vector2> BodyPos)
{
	public Direction Direction { get; private set; } = Direction.Right;
	public Direction ConfirmedDirection { get; private set; } = Direction.Right;
	public Vector2 HeaderPos => BodyPos.First.Value;
	public Vector2 TailPos => BodyPos.Last.Value;

	public void Grow() => BodyPos.AddFirst(NextPos);

	public void Go()
	{
		BodyPos.AddFirst(NextPos);
		BodyPos.RemoveLast();
		ConfirmedDirection = Direction;
	}

	public Vector2 NextPos => Direction switch
	{
		Direction.Up => new(HeaderPos.X, HeaderPos.Y - 1),
		Direction.Down => new(HeaderPos.X, HeaderPos.Y + 1),
		Direction.Left => new(HeaderPos.X - 1, HeaderPos.Y),
		Direction.Right => new(HeaderPos.X + 1, HeaderPos.Y),
		_ => throw new ArgumentOutOfRangeException(nameof(Direction)),
	};

	public void ChangeDirection(Direction direction)
	{
		static bool IsVert(Direction direction) => direction == Direction.Up || direction == Direction.Down;
		bool vertMe = IsVert(ConfirmedDirection);
		bool vertIt = IsVert(direction);
		if (vertMe ^ vertIt)
		{
			Direction = direction;
		}
	}
}

enum Direction
{
	Up, Down, Left, Right,
}

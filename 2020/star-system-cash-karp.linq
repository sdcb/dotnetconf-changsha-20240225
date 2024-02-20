<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Diagnostics.CodeAnalysis</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
</Query>

void Main()
{
	using StarSystem sys = StarSystem.Create3Body("Eight");

	CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(QueryCancelToken);

	Task _ = Task.Run(async () =>
	{
		Stopwatch totalSw = Stopwatch.StartNew();
		try
		{
			for (double lastElapsed = 0; !cts.Token.IsCancellationRequested;)
			{
				Stopwatch sw = Stopwatch.StartNew();
				await Task.Delay(1000, cts.Token);

				double elapsed = sys.Elapsed - lastElapsed;
				lastElapsed = sys.Elapsed;
				Console.WriteLine($"lastElapsed: {lastElapsed:N0}s, total step time: {sys.Elapsed:N0}s, perf: {elapsed / sw.ElapsedMilliseconds:N12}tps.");
			}
		}
		catch (TaskCanceledException) { }
		Console.WriteLine($"total step time: {sys.Elapsed}s, perf: {sys.Elapsed / totalSw.ElapsedMilliseconds:N4}tps, elapsed={totalSw.ElapsedMilliseconds}ms.");
	});

	try
	{
		for (int i = 0; !sys.Crashed && !cts.Token.IsCancellationRequested; ++i)
		{
			sys.Step();
		}
		Console.WriteLine("Crashed");
	}
	finally
	{
		cts.Cancel();
	}
}

record StarCache(int Precision) : IDisposable
{
	public double Fx = 0;
	public double Fy = 0;

	public void Dispose()
	{
	}
}

class StarSystem : IDisposable
{
	readonly Star[] _stars;
	readonly Ode45CashKarp _ode;

	private StarSystem(Star[] stars, double tmax, double tol = 1e-10)
	{
		_stars = stars;
		_ode = new Ode45CashKarp(new ArrayString<double>(4, Array.ConvertAll(_stars, x => x.State)), NewtonsLaw, t: 0, dt0: 0.02, new Ode45Options(
			tol: tol, 
			dtMinMag: 2e-7, 
			dtMaxMag: 0.1));
	}

	public void Dispose()
	{
		for (int i = 0; i < _stars.Length; ++i)
		{
			_stars[i].Dispose();
		}
	}

	public double Elapsed => _ode.T;
	public bool Crashed => _stars.Any(x => x.Crashed);

	public void Step()
	{
		ArrayString<double> oldStates = new ArrayString<double>(4, Array.ConvertAll(_stars, x => x.State));
		_ode.Steps(10000, _ode.T + 0.02);
	}

	void NewtonsLaw(IFixedSizeList<double> delta, ArrayString<double> oldStates, double t)
	{
		const double G = 1.0;
		for (int i = 0; i < oldStates.Count; ++i)
		{
			// Px: [0], Py: [1], Vx: [2], Vy: [3]
			delta[i * 4 + 0] = oldStates[i][2];
			delta[i * 4 + 1] = oldStates[i][3];
			delta[i * 4 + 2] = delta[i * 4 + 3] = 0;

			for (int j = 0; j < oldStates.Count; ++j)
			{
				if (i == j) continue;

				double rx = oldStates[j][0] - oldStates[i][0];
				double ry = oldStates[j][1] - oldStates[i][1];
				double r3 = Math.Pow(rx * rx + ry * ry, 1.5);

				delta[i * 4 + 2] += G * _stars[j].Mass * rx / r3;
				delta[i * 4 + 3] += G * _stars[j].Mass * ry / r3;
			}
		}
	}

	public IEnumerable<StarSystemSnapshot> AutoStep(int boundedCapacity = 512, CancellationToken cancellationToken = default)
	{
		BlockingCollection<StarSystemSnapshot> q = new(boundedCapacity: boundedCapacity);
		Task _ = Task.Factory.StartNew(() =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				Step();
				try
				{
					q.Add(GetSnapshot(), cancellationToken);
				}
				catch (TaskCanceledException)
				{
					break;
				}
			}
			q.CompleteAdding();
		}, TaskCreationOptions.LongRunning);
		return q.GetConsumingEnumerable(cancellationToken);
	}

	public StarSystemSnapshot GetSnapshot() => new StarSystemSnapshot((float)Elapsed, _stars.Select(x => x.GetSnapshot()).ToArray());

	public static StarSystem CreateNSystem(int N, int precision) => new StarSystem(CreateStars(N, precision).ToArray(), double.MaxValue);

	static IEnumerable<Star> CreateStars(int N, int precision)
	{
		for (var i = 0; i < N; ++i)
		{
			double angle = 1.0f * i / N * Math.PI * 2;
			double R = 1;
			double M = 2 * 2 / (N * Math.Sqrt(N) * Math.Log(N));
			double v = 0.5;
			double px = R * Math.Sin(angle);
			double py = R * -Math.Cos(angle);
			double vx = v * Math.Cos(angle);
			double vy = v * Math.Sin(angle);
			yield return new Star(i, StarType.Solar, px, py, vx, vy, M);
		}
	}

	public static StarSystem CreateSolarEarthMoon(int precision, double dt = 0.001953125) => Create(precision, dt,
		new(0, 0, 0, -0.2, StarType.Solar, 1),
		new(-1.5, 0, 0, 0.55, StarType.Planet, 0.3),
		new(-1.7, 0, 0, 1.4, StarType.Moon, 0.01));

	public static StarSystem Create3Body(string name)
	{
		using HttpClient http = new HttpClient();
		HttpResponseMessage resp = http.Send(new HttpRequestMessage(HttpMethod.Get, "https://io.starworks.cc:88/cv-public/2023/3-bodies.json"));
		if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentType?.MediaType != "application/json")
		{
			throw new Exception(new StreamReader(resp.Content.ReadAsStream()).ReadToEnd());
		}

		JObject obj = (JObject)JObject.Parse(new StreamReader(resp.Content.ReadAsStream()).ReadToEnd())[name]!;
		Star[] stars = obj["y"]!
			.Chunk(6)
			.Select((x, i) => new Star(i + 1, StarType.Solar, (double)x[0], (double)x[1], (double)x[3], (double)x[4], mass: 1))
			.ToArray();
		double tol = (double?)obj["tol"] ?? 1e-10;
		double scale = (double?)obj["scale"] ?? 1;
		double tmax = ((double?)obj["tmax"] ?? 60) * Math.Pow(scale, 1.5);
		return new StarSystem(stars, tmax, 1e-11);
	}

	public static StarSystem Create(int precision, double dt = 0.001953125, params StarParams[] stars) => new StarSystem(stars.Select((x, i) => x.Create(i, precision)).ToArray(), double.MaxValue);
}

public record StarParams(double Px, double Py, double Vx, double Vy, StarType StarType = StarType.Solar, double Mass = 1)
{
	internal Star Create(int n, int precision) => new Star(n, StarType, Px, Py, Vx, Vy, Mass);
}

internal record Star : IDisposable
{
	public int Id;
	public StarType StarType;
	public double Mass;
	
	public double[] State = new double[4];
	public bool Crashed => Px > 50 || Px < -50 || Py > 50 || Py < -50;
	public double Px => State[0];
	public double Py => State[1];
	public double Vx => State[2];
	public double Vy => State[3];
	public double Size => StarType switch
	{
		StarType.BlackHole => Math.Log(Math.Log(Mass)),
		_ => Math.Log(Mass)
	};
	
	public Star(int id, StarType starType, double px, double py, double vx, double vy, double mass)
	{
		Id = id; StarType = starType; Mass = mass;
		State[0] = px; State[1] = py; State[2] = vx; State[3] = vy;
	}

	public void Dispose()
	{
	}

	public StarSnapshot GetSnapshot() => new StarSnapshot(Id, (float)Px, (float)Py, StarType, (float)Mass);
}

public enum StarType { Moon, Planet, Solar, BlackHole }

public record StarSnapshot(int Id, float Px, float Py, StarType StarType, float Mass)
{
	public float Size => StarType switch
	{
		StarType.BlackHole => MathF.Log(MathF.Log(Mass) + 1) + 1,
		_ => 0.05f
	};
}
public record StarSystemSnapshot(float Timestamp, StarSnapshot[] Stars);

public class Ode45CashKarp
{
	public double Dt, T;
	public ArrayString<double> Y;
	public ArrayString<double> _w;

	IFixedSizeList<double> _k1, _k2, _k3, _k4, _k5, _k6;
	double[] _errorScale;
	Deriv _deriv;
	Ode45Options _options;

	public Ode45CashKarp(ArrayString<double> y0, Deriv deriv, double t = 0, double dt0 = 1e-3, Ode45Options? options = null)
	{
		Y = y0;

		T = t;
		Dt = dt0;
		_deriv = deriv;
		_options = options ?? new Ode45Options();
		int n = y0.UnitIndexer.Count;
		_errorScale = new double[n];
		_w = y0.Clone();
		_k1 = new FixedSizeList<double>(n);
		_k2 = new FixedSizeList<double>(n);
		_k3 = new FixedSizeList<double>(n);
		_k4 = new FixedSizeList<double>(n);
		_k5 = new FixedSizeList<double>(n);
		_k6 = new FixedSizeList<double>(n);
	}

	void CalculateK1() => _deriv(_k1, Y, T);

	void CalculateKs(double dt)
	{
		int n = _k1.Count;
		ArrayStringUnitIndexer<double> w = this._w.UnitIndexer;
		ArrayStringUnitIndexer<double> y = this.Y.UnitIndexer;

		// k2: 1/5 => 1/5
		for (int i = 0; i < n; i++)
		{
			w[i] = y[i] + dt * (
			  0.2 * this._k1[i]);
		}
		this._deriv(this._k2, this._w, this.T + dt * 0.2);

		// k3: 3/10 => 3/40, 9/40
		for (int i = 0; i < n; i++)
		{
			w[i] = y[i] + dt * (
			  0.075 * this._k1[i] +
			  0.225 * this._k2[i]);
		}
		this._deriv(this._k3, this._w, this.T + dt * 0.3);

		// k4: 3/5 => 3/10, -9/10, 6/5
		for (int i = 0; i < n; i++)
		{
			w[i] = y[i] + dt * (
			   0.3 * this._k1[i] +
			  -0.9 * this._k2[i] +
			   1.2 * this._k3[i]);
		}
		this._deriv(this._k4, this._w, this.T + dt * 0.6);

		// k5: 1 => -11/54, 5/2, -70/27, 35/27
		for (int i = 0; i < n; i++)
		{
			w[i] = y[i] + dt * (
			  -0.203703703703703703 * this._k1[i] +
			   2.5 * this._k2[i] +
			  -2.592592592592592592 * this._k3[i] +
			   1.296296296296296296 * this._k4[i]);
		}
		this._deriv(this._k5, this._w, this.T + dt /* * b5 */ );

		// k6: 7/8 => 1631/55296, 175/512, 575/13824, 44275/110592, 253/4096
		for (int i = 0; i < n; i++)
		{
			w[i] = y[i] + dt * (
			  0.029495804398148148 * this._k1[i] +
			  0.341796875 * this._k2[i] +
			  0.041594328703703703 * this._k3[i] +
			  0.400345413773148148 * this._k4[i] +
			  0.061767578125 * this._k5[i]);
		}
		this._deriv(this._k6, this._w, this.T + dt * 0.875);
	}

	double CalculateError(double dt)
	{
		double error = 0;
		int n = Y.UnitIndexer.Count;
		for (int i = 0; i < n; ++i)
		{
			error = _options.ErrorReduceFunction(i, error,
			  dt * (
				 0.004293774801587301 * this._k1[i] + // 2825/27648  - 37/378
				/* 0                 * this._k2[i] + // 0 - 0 */
				-0.018668586093857832 * this._k3[i] + // 18575/48384 - 250/621
				 0.034155026830808080 * this._k4[i] + // 13525/55296 - 125/594
				 0.019321986607142857 * this._k5[i] + // 277/14336   - 0
				-0.039102202145680406 * this._k6[i]   // 1/4         - 512/1771
			  ) / this._errorScale[i]
			);
		}
		return _options.ErrorPostFunction(error);
	}

	void Update(double dt)
	{
		int n = _k1.Count;
		ArrayStringUnitIndexer<double> y = this.Y.UnitIndexer;
		for (var i = 0; i < n; i++)
		{
			y[i] += dt * (
			  0.097883597883597883 * this._k1[i] + // 37/378
												   // 0                 * this._k2[i] + // 0
			  0.402576489533011272 * this._k3[i] + // 250/621
			  0.210437710437710437 * this._k4[i] + // 125/594
												   // 0                 * this._k5[i] + // 0
			  0.289102202145680406 * this._k6[i]   // 512/1771
			);
		}
		this.T += dt;
	}

	void CalculateErrorScale(double dt)
	{
		ArrayStringUnitIndexer<double> y = this.Y.UnitIndexer;
		for (int i = 0; i < y.Count; ++i)
		{
			this._errorScale[i] = _options.ErrorScaleFunction(i, dt, y[i], this._k1[i]);
		}
	}

	static double MinMag(double a, double b) => a > 0 ? Math.Min(a, b) : Math.Max(a, b);
	static double MaxMag(double a, double b) => a > 0 ? Math.Max(a, b) : Math.Min(a, b);

	public bool Step(double? tEnd = null)
	{
		// Bail out early if we're *at* the limit:
		if (tEnd.HasValue && Math.Abs(this.T - tEnd.Value) < this.Dt * 1e-10)
		{
			return false;
		}

		double thisDt = this.Dt;

		// Don't integrate past a tLimit, if provided:
		if (tEnd != null)
		{
			thisDt = thisDt switch
			{
				> 0 => Math.Min(tEnd.Value - this.T, thisDt),
				_ => Math.Max(tEnd.Value - this.T, thisDt),
			};
		}

		// Limit the magnitude of dt to dtMaxMag
		if (_options.DtMaxMag != null && Math.Abs(thisDt) > _options.DtMaxMag)
		{
			_options.Log($"ODE45-STEP: step greater than maximum stepsize requested. dt magnitude has been limited.");
			thisDt = thisDt > 0 ? _options.DtMaxMag.Value : -_options.DtMaxMag.Value;
		}

		// Limit the magnitude of dt to dtMinMag
		if (Math.Abs(thisDt) < _options.DtMinMag)
		{
			_options.Log($"ODE45-STEP: step smaller than minimum stepsize requested. dt magnitude has been limited.");
			thisDt = thisDt > 0 ? _options.DtMinMag : -_options.DtMinMag;
		}

		// The first derivative doesn't change even if dt does, so only calculate this once:
		this.CalculateK1();

		// The scale factor per-dimension probably doesn't need to change either across a single adaptive step:
		this.CalculateErrorScale(thisDt);

		bool lowerDtLimitReached = false;

		double error;
		while (true)
		{
			// Calulate intermediate k's for the proposed step:
			CalculateKs(thisDt);

			// Calulate the max error of the proposed step:
			error = this.CalculateError(thisDt);

			if (error < _options.Tol || lowerDtLimitReached)
			{
				// Success! Exit: 
				break;
			}

			if (!double.IsFinite(error))
			{
				throw new Exception("ode45-cash-karp::step() NaN encountered while integrating.");
			}

			// Failure, adapt the timestep:
			double negociatedNextDt = 0.9 * thisDt * Math.Pow(_options.Tol / error, 0.2);

			// Cut the timestep, but not by more than maxDecreaseFactor
			thisDt = MaxMag(thisDt / _options.MaxDecreaseFactor, negociatedNextDt);

			// If stepsize too small, finish off by taking the currently proposed step and logging a warning:
			if (Math.Abs(thisDt) < _options.DtMinMag)
			{
				thisDt = _options.DtMinMag * Math.Sign(thisDt);
				_options.Log($"ODE-STEP: Minimum stepsize reached.");
				lowerDtLimitReached = true;
			}
		}

		// Apply this update:
		Update(thisDt);

		// Calculate the next timestep size:
		double nextDt = 0.9 * thisDt * Math.Pow(_options.Tol / error, 0.25);

		// Increase the timestep for next time around, but not by more than the maxIncreaseFactor
		Dt = MaxMag(Dt / _options.MaxDecreaseFactor, MinMag(Dt * _options.MaxIncreaseFactor, nextDt));

		if (tEnd != null)
		{
			return Math.Abs(T - tEnd.Value) > Dt * 1e-8;
		}
		else
		{
			return true;
		}
	}

	public bool Steps(int n, double? tLimit)
	{
		for (int i = 0; i < n; ++i)
		{
			if (!Step(tLimit)) return false;
		}
		return true;
	}
}

public record Ode45Options
{
	public required double Tol { get; init; }
	public required double MaxIncreaseFactor { get; init; }
	public required double MaxDecreaseFactor { get; init; }
	public required double DtMinMag { get; init; }
	public required double? DtMaxMag { get; init; }
	public required bool Verbose { get; init; }
	public int MaxLogs { get; set; } = 10;

	[SetsRequiredMembers]
	public Ode45Options(
		double tol = 1e-8,
		double maxIncreaseFactor = 10,
		double maxDecreaseFactor = 10,
		double dtMinMag = 0,
		double? dtMaxMag = null,
		bool verbose = true)
	{
		Tol = tol;
		MaxIncreaseFactor = maxIncreaseFactor;
		MaxDecreaseFactor = maxDecreaseFactor;
		DtMinMag = dtMinMag;
		DtMaxMag = dtMaxMag;
		Verbose = verbose;
	}

	public virtual double ErrorScaleFunction(int i, double dt, double y, double dydt)
		=> Math.Abs(y) + Math.Abs(dt * dydt) + 1e-32;

	public virtual double ErrorReduceFunction(int i, double accumulatedError, double errorEstimate)
		=> Math.Max(accumulatedError, Math.Abs(errorEstimate));

	public virtual double ErrorPostFunction(double accumulatedError)
		=> accumulatedError;

	private int logCount = 0;
	private bool maxLogWarningIssued = false;
	public virtual void Log(string msg)
	{
		if (!this.Verbose) return;
		if (logCount < MaxLogs)
		{
			Console.WriteLine(msg);
			++logCount;
		}
		else
		{
			if (!maxLogWarningIssued)
			{
				Console.WriteLine("ode45-cash-karp: too many warnings. Silencing further output");
				maxLogWarningIssued = true;
		  	}
		}
	}
}

public delegate double ErrorScaleFunction(int i, double dt, double y, double dydt);
public delegate double ErrorReduceFunction(int i, double accumulatedError, double errorEstimate);
public delegate double ErrorPostFunction(double accumulatedError);
public delegate void Deriv(IFixedSizeList<double> dydt, ArrayString<double> y, double t);

public class ArrayString<T> : IFixedSizeList<T[]>
{
	// A list of generic arrays
	private List<T[]> arrays;

	// A property that represents the size of each array
	public int ArrayUnitSize { get; private set; }

	// The constructor that takes an array size and multiple generic arrays as parameters
	public ArrayString(int arrayUnitSize, params T[][] args)
	{
		// Initialize the property with the given array size
		ArrayUnitSize = arrayUnitSize;

		// Initialize the list with the given arrays
		arrays = new List<T[]>(args);

		// Verify that all arrays have the same length as the property
		foreach (T[] array in arrays)
		{
			if (array.Length != ArrayUnitSize)
			{
				throw new ArgumentException("All arrays must have the same length as ArrayUnitSize");
			}
		}
	}

	// The constructor that takes an array size and multiple generic arrays as parameters
	public ArrayString(int arrayUnitSize, IEnumerable<T[]> args)
	{
		// Initialize the property with the given array size
		ArrayUnitSize = arrayUnitSize;

		// Initialize the list with the given arrays
		arrays = new List<T[]>(args);

		// Verify that all arrays have the same length as the property
		foreach (T[] array in arrays)
		{
			if (array.Length != ArrayUnitSize)
			{
				throw new ArgumentException("All arrays must have the same length as ArrayUnitSize");
			}
		}
	}

	// Get count of arrays
	public int Count => arrays.Count;

	/*
     * indexer to return an array at a given index 
     */

	public T[] this[int index]
	{
		get
		{
			if (index >= 0 && index < this.arrays.Count)
			{
				return this.arrays[index];
			}
			else
			{
				throw new IndexOutOfRangeException("Invalid index");
			}
		}

		set
		{
			if (index >= 0 && index < this.arrays.Count)
			{
				this.arrays[index] = value;
			}
			else
			{
				throw new IndexOutOfRangeException("Invalid index");
			}
		}
	}

	// A method that returns a clone of this instance   
	public ArrayString<T> Clone()
	{
		// Create a new list of generic arrays   
		List<T[]> clonedArrays = new List<T[]>();

		// Copy each element from this instance to the new list   
		foreach (T[] array in this.arrays)
		{
			T[] clonedArray = new T[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				clonedArray[i] = array[i];
			}
			clonedArrays.Add(clonedArray);
		}

		// Return a new ArrayString instance with the same property and cloned list    
		return new ArrayString<T>(this.ArrayUnitSize, clonedArrays.ToArray());
	}

	/*
	* Implement IEnumerable<T[]> interface 
	*/

	public IEnumerator<T[]> GetEnumerator()
	{
		return this.arrays.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	/*
	 * Add a method that returns a new class that implements single element indexer and IEnumerable<T> interface 
	 */

	public ArrayStringUnitIndexer<T> UnitIndexer => new ArrayStringUnitIndexer<T>(this);
}

public class ArrayStringUnitIndexer<T> : IFixedSizeList<T>
{
	// A reference to an instance of ArrayString class
	private readonly ArrayString<T> parent;

	// The constructor that takes an instance of ArrayString class as parameter
	public ArrayStringUnitIndexer(ArrayString<T> parent)
	{
		this.parent = parent;
	}

	// get all elements of total ArrayString
	public int Count => this.parent.Count * this.parent.ArrayUnitSize;

	// The indexer that allows access or assignment by index to single elements in parent's arrays
	public T this[int index]
	{
		get
		{
			// Find the array and the position that correspond to the index using parent's property
			int arrayIndex = index / parent.ArrayUnitSize;
			int position = index % parent.ArrayUnitSize;

			// Check if the index is valid using parent's indexer
			if (arrayIndex < parent.Count)
			{
				return parent[arrayIndex][position];
			}
			else
			{
				throw new IndexOutOfRangeException("Invalid index");
			}
		}

		set
		{
			// Find the array and the position that correspond to the index using parent's property 
			int arrayIndex = index / parent.ArrayUnitSize;
			int position = index % parent.ArrayUnitSize;

			// Check if the index is valid using parent's indexer  
			if (arrayIndex < parent.Count)
			{
				parent[arrayIndex][position] = value;
			}
			else
			{
				throw new IndexOutOfRangeException("Invalid index");
			}
		}
	}

	/*
     * Implement IEnumerable<T> interface 
     */

	public IEnumerator<T> GetEnumerator()
	{
		foreach (T[] array in this.parent)
		{
			foreach (T element in array)
			{
				yield return element;
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

public interface IFixedSizeList<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
{
	T this[int index] { get; set; }
}

public class FixedSizeList<T> : IFixedSizeList<T>
{
	private T[] _array;

	public FixedSizeList(T[] array)
	{
		_array = array;
	}

	public FixedSizeList(int capacity)
	{
		_array = new T[capacity];
	}

	public int Count => _array.Length;

	public T this[int index]
	{
		get => _array[index];
		set => _array[index] = value;
	}

	T IFixedSizeList<T>.this[int index]
	{
		get => this[index];
		set => this[index] = value;
	}

	public IEnumerator<T> GetEnumerator()
	{
		return ((IEnumerable<T>)_array).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
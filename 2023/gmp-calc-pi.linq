<Query Kind="Statements">
  <NuGetReference Prerelease="true">Sdcb.Arithmetic.Gmp</NuGetReference>
  <NuGetReference Prerelease="true">Sdcb.Arithmetic.Gmp.runtime.win32</NuGetReference>
  <NuGetReference Prerelease="true">Sdcb.Arithmetic.Gmp.runtime.win64</NuGetReference>
  <Namespace>Sdcb.Arithmetic.Gmp</Namespace>
</Query>

// from: https://gist.github.com/komasaru/68f209118edbac0700da
// check: http://www.eveandersson.com/pi/digits/1000000
while (true)
{
	Stopwatch sw = Stopwatch.StartNew();
	using GmpFloat pi = CalcPI(100_0000);
	Console.WriteLine($"耗时：{sw.ElapsedMilliseconds}ms");
	//Console.WriteLine();
	pi.ToString()!.Contains("56787961303311646283996346460422090106105779458151").Dump();
	//Console.WriteLine(pi.ToString());
	break;
}

GmpFloat CalcPI(int inputDigits)
{
	const double DIGITS_PER_TERM = 14.1816474627254776555; // = log(53360^3) / log(10)
	int DIGITS = (int)Math.Max(inputDigits, Math.Ceiling(DIGITS_PER_TERM));
	uint PREC = (uint)(DIGITS * Math.Log2(10));
	int N = (int)(DIGITS / DIGITS_PER_TERM);
	using GmpInteger A = GmpInteger.From(13591409);
	using GmpInteger B = GmpInteger.From(545140134);
	using GmpInteger C = GmpInteger.From(640320);
	using GmpInteger D = GmpInteger.From(426880);
	using GmpInteger E = GmpInteger.From(10005);
	using GmpInteger E3_24 = C * C * C / 24;

	using PQT pqt = ComputePQT(0, N);

	GmpFloat pi = new GmpFloat(precision: PREC);
	// pi = D * sqrt((mpf_class)E) * PQT.Q;
	pi.Assign(GmpFloat.From(D, PREC) * GmpFloat.Sqrt((GmpFloat)E, PREC) * (GmpFloat)pqt.Q);
	// pi /= (A * PQT.Q + PQT.T);
	GmpFloat.DivideInplace(pi, pi, GmpFloat.From(A * pqt.Q + pqt.T, PREC));
	return pi;

	PQT ComputePQT(int n1, int n2)
	{
		int m;

		if (n1 + 1 == n2)
		{
			PQT res = new PQT();
			res.P = GmpInteger.From(2 * n2 - 1);
			GmpInteger.MultiplyInplace(res.P, res.P, 6 * n2 - 1);
			GmpInteger.MultiplyInplace(res.P, res.P, 6 * n2 - 5);

			GmpInteger q = GmpInteger.From(E3_24);
			GmpInteger.MultiplyInplace(q, q, n2);
			GmpInteger.MultiplyInplace(q, q, n2);
			GmpInteger.MultiplyInplace(q, q, n2);
			res.Q = q;

			GmpInteger t = GmpInteger.From(B);
			GmpInteger.MultiplyInplace(t, t, n2);
			GmpInteger.AddInplace(t, t, A);
			GmpInteger.MultiplyInplace(t, t, res.P);
			// res.T = (A + B * n2) * res.P;			
			if ((n2 & 1) == 1) GmpInteger.NegateInplace(t, t);
			res.T = t;

			return res;
		}
		else
		{
			m = (n1 + n2) / 2;
			PQT res1 = ComputePQT(n1, m);
			using PQT res2 = ComputePQT(m, n2);
			GmpInteger p = res1.P * res2.P;
			GmpInteger q = res1.Q * res2.Q;

			// t = res1.T * res2.Q + res1.P * res2.T
			GmpInteger.MultiplyInplace(res1.T, res1.T, res2.Q);
			GmpInteger.MultiplyInplace(res1.P, res1.P, res2.T);
			GmpInteger.AddInplace(res1.T, res1.T, res1.P);
			res1.P.Dispose();
			res1.Q.Dispose();
			return new PQT
			{
				P = p,
				Q = q,
				T = res1.T,
			};
		}
	}
}

public ref struct PQT
{
	public GmpInteger P;
	public GmpInteger Q;
	public GmpInteger T;

	public void Dispose()
	{
		P?.Dispose();
		Q?.Dispose();
		T?.Dispose();
	}
}
namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class SeededRandomManager : NetworkSingleton<SeededRandomManager>
{
	private global::System.Random _random;

	private int _currentSeed;

	private int _mysteryBoxCounter;

	private int _angelsReelCounter;

	private int _devilsReelCounter;

	public int MysteryBoxCounter
	{
		get
		{
			_mysteryBoxCounter++;
			return _mysteryBoxCounter;
		}
	}

	public int AngelsReelCounter
	{
		get
		{
			_angelsReelCounter++;
			return _angelsReelCounter;
		}
	}

	public int DevilsReelCounter
	{
		get
		{
			_devilsReelCounter++;
			return _devilsReelCounter;
		}
	}

	public int CurrentSeed => _currentSeed;

	public float value
	{
		get
		{
			if (_random == null)
			{
				Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
				return UnityEngine.Random.value;
			}
			return (float)_random.NextDouble();
		}
	}

	public Vector2 insideUnitCircle
	{
		get
		{
			float f = Range(0f, MathF.PI * 2f);
			float num = Mathf.Sqrt(value);
			return new Vector2(num * Mathf.Cos(f), num * Mathf.Sin(f));
		}
	}

	public Vector3 insideUnitSphere
	{
		get
		{
			float num = value;
			float num2 = value;
			float f = MathF.PI * 2f * num;
			float f2 = Mathf.Acos(2f * num2 - 1f);
			float num3 = Mathf.Pow(value, 1f / 3f);
			return new Vector3(num3 * Mathf.Sin(f2) * Mathf.Cos(f), num3 * Mathf.Sin(f2) * Mathf.Sin(f), num3 * Mathf.Cos(f2));
		}
	}

	public Quaternion rotation => Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

	public Color color => new Color(value, value, value, 1f);

	public Color colorWithAlpha => new Color(value, value, value, value);

	protected override void OnAwake()
	{
		base.OnAwake();
	}

	public void InitializeSeed(int seed)
	{
		_currentSeed = seed;
		_random = new global::System.Random(seed);
		Debug.Log($"[SeededRandomManager] Initialized with seed: {seed}");
	}

	public int Range(int min, int max)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
			return UnityEngine.Random.Range(min, max);
		}
		if (min >= max)
		{
			return min;
		}
		return _random.Next(min, max);
	}

	public float Range(float min, float max)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
			return UnityEngine.Random.Range(min, max);
		}
		if (min >= max)
		{
			return min;
		}
		double num = max - min;
		double num2 = _random.NextDouble();
		return (float)((double)min + num2 * num);
	}

	public global::System.Random GetRandomInstance()
	{
		if (_random == null)
		{
			Debug.LogWarning("[SeededRandomManager] Random not initialized, creating new instance");
			InitializeSeed(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		return _random;
	}

	private int GetContextualSeed(int context)
	{
		return _currentSeed * 31 + context;
	}

	public int RangeWithContext(int min, int max, int context)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
			return UnityEngine.Random.Range(min, max);
		}
		if (min >= max)
		{
			return min;
		}
		return new global::System.Random(GetContextualSeed(context)).Next(min, max);
	}

	public float RangeWithContext(float min, float max, int context)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
			return UnityEngine.Random.Range(min, max);
		}
		if (min >= max)
		{
			return min;
		}
		global::System.Random random = new global::System.Random(GetContextualSeed(context));
		double num = max - min;
		double num2 = random.NextDouble();
		return (float)((double)min + num2 * num);
	}

	public float ValueWithContext(int context)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Using Unity Random as fallback.");
			return UnityEngine.Random.value;
		}
		return (float)new global::System.Random(GetContextualSeed(context)).NextDouble();
	}

	public global::System.Random GetContextualRandomInstance(int context)
	{
		if (_random == null)
		{
			Debug.LogError("[SeededRandomManager] Random not initialized! Seed should be loaded from save file. Cannot create contextual random.");
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		return new global::System.Random(GetContextualSeed(context));
	}

	public override bool Weaved()
	{
		return true;
	}
}

}

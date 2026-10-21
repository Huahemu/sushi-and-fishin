namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::Shapes;
using global::TMPro;
using global::UnityEngine;

public class Crash : GameBase
{
	[Header("References")]
	[SerializeField]
	private Transform rocketImage;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private TextMeshPro cashoutText;

	[SerializeField]
	private TextMeshPro countdownText;

	[Header("Settings")]
	[SerializeField]
	private float instantCrashChance = 0.1f;

	[SerializeField]
	private float maxPoint = 100f;

	[SerializeField]
	private float raiseSpeed = 0.15f;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent sfxLoop;

	[SerializeField]
	private EventReference sfxCrashOver;

	[SerializeField]
	private EventReference sfxTick;

	[Header("Line Graph Settings")]
	[SerializeField]
	private float lineThickness = 0.05f;

	[SerializeField]
	private Color lineColor = Color.white;

	[SerializeField]
	private float timeAxisLength = 10f;

	[SerializeField]
	private float multiplierAxisHeight = 5f;

	[SerializeField]
	private int curveResolution = 100;

	[SerializeField]
	private Transform lineGraphParent;

	[Header("Curve Steepness Control")]
	[SerializeField]
	private float minCurvePower = 1f;

	[SerializeField]
	private float maxCurvePower = 5f;

	[SerializeField]
	private float curvePowerMultiplier = 0.1f;

	[SerializeField]
	private float endPointOffsetX = 0.1f;

	[SerializeField]
	private float endPointOffsetY = 0.1f;

	[SerializeField]
	private float curveDrawDuration = 10f;

	[SerializeField]
	private float multiplierTextOffsetX = 0.5f;

	[Header("Border Settings")]
	[SerializeField]
	private bool showBorders = true;

	[SerializeField]
	private float borderThickness = 0.02f;

	[SerializeField]
	private Color borderColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

	[SyncVar(hook = "OnMultiplierChanged")]
	private float _multiplier;

	[SyncVar(hook = "OnHasStartedChanged")]
	private bool _hasStarted;

	[SyncVar(hook = "OnHasCrashedChanged")]
	private bool _hasCrashed;

	private bool _hasEnded;

	private Polyline multiplierLine;

	private float gameStartTime;

	private float currentMultiplierValue;

	private Coroutine curveUpdateCoroutine;

	private float lastUpdateMultiplier;

	private const float UPDATE_THRESHOLD = 0.01f;

	private Polyline xAxisBorder;

	private Polyline yAxisBorder;

	private Polyline topBorder;

	private Polyline rightBorder;

	private bool bordersInitialized;

	public Action<float, float> _Mirror_SyncVarHookDelegate__multiplier;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__hasStarted;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__hasCrashed;

	public float Network_multiplier
	{
		get
		{
			return _multiplier;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _multiplier, 8uL, _Mirror_SyncVarHookDelegate__multiplier);
		}
	}

	public bool Network_hasStarted
	{
		get
		{
			return _hasStarted;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _hasStarted, 16uL, _Mirror_SyncVarHookDelegate__hasStarted);
		}
	}

	public bool Network_hasCrashed
	{
		get
		{
			return _hasCrashed;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _hasCrashed, 32uL, _Mirror_SyncVarHookDelegate__hasCrashed);
		}
	}

	private void OnMultiplierChanged(float oldValue, float newValue)
	{
		multiplierText.text = newValue.ToString("0.00") + "x";
		long num = (long)Math.Round((float)currentBet * newValue);
		potentialWinningText.text = "$" + num.ToString("N0");
		if (_hasStarted)
		{
			currentMultiplierValue = newValue;
			UpdateExponentialCurve();
		}
	}

	private void OnHasStartedChanged(bool oldValue, bool newValue)
	{
		if (newValue && !oldValue && base.isClient)
		{
			InitializeLine();
			gameStartTime = Time.time;
			currentMultiplierValue = 1f;
			if (curveUpdateCoroutine != null)
			{
				StopCoroutine(curveUpdateCoroutine);
			}
			curveUpdateCoroutine = StartCoroutine(ContinuousCurveUpdate());
		}
	}

	private void OnHasCrashedChanged(bool oldValue, bool newValue)
	{
		if (newValue && !oldValue)
		{
			UpdateExponentialCurve();
		}
	}

	protected override void StartGame()
	{
		base.StartGame();
		InitializeLine();
		StartCoroutine(StartCountDown());
	}

	private IEnumerator StartCountDown()
	{
		RpcShowCountdown();
		float startTime = Time.time;
		while (Time.time - startTime < 1f)
		{
			string value = (3f - (Time.time - startTime) * 3f).ToString("0");
			RpcUpdateCountdownText(value);
			RpcPlayMysteriousTickingNoise();
			yield return new WaitForSeconds(0.34f);
		}
		RpcHideCountdown();
		SetCrashPoint();
	}

	private void SetCrashPoint()
	{
		global::System.Random seededRandom = GetSeededRandom();
		float num = (float)seededRandom.NextDouble();
		float crashPoint = 1.01f;
		if (num > instantCrashChance)
		{
			crashPoint = GetRandomCrashPoint((float)seededRandom.NextDouble());
		}
		StartCoroutine(RaiseRoutine(crashPoint));
		Network_hasStarted = true;
	}

	private IEnumerator RaiseRoutine(float crashPoint)
	{
		float t = 0f;
		Network_multiplier = 1f;
		gameStartTime = Time.time;
		RpcStartRiseLoop();
		while (_multiplier < crashPoint)
		{
			t += Time.deltaTime;
			if (_hasCrashed)
			{
				t += Time.deltaTime * 4f;
			}
			Network_multiplier = Mathf.Exp(raiseSpeed * t);
			yield return null;
		}
		currentMultiplierValue = crashPoint;
		RpcStopRiseLoop();
		RpcPlayCrashOverSFX();
		Network_multiplier = crashPoint;
		CrashOnPoint();
	}

	private void CrashOnPoint()
	{
		if (!_hasEnded)
		{
			Payout(0.0, ChangeType.GameResult, null, -1L);
		}
		_hasEnded = true;
		Network_hasCrashed = true;
		RpcSetCrashColors();
		StartCoroutine(ResetGameRoutine());
	}

	[Server]
	public void Cashout()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Crash::Cashout()' called when server was not active");
		}
		else if (isPlaying && _hasStarted && !_hasEnded)
		{
			_hasEnded = true;
			RpcCashout(_multiplier.ToString("0.00") + "x $" + (long)Math.Round((double)currentBet * (double)_multiplier));
			Payout(_multiplier, ChangeType.GameResult, null, -1L);
		}
	}

	[ClientRpc]
	private void RpcCashout(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void Crash::RpcCashout(System.String)", 812465518, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	protected override void ResetGame()
	{
		Network_multiplier = 0f;
		Network_hasStarted = false;
		_hasEnded = false;
		Network_hasCrashed = false;
		RpcCashout("");
		ClearLine();
		base.ResetGame();
	}

	[ClientRpc]
	private void RpcShowCountdown()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcShowCountdown()", 939937333, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcHideCountdown()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcHideCountdown()", -922614198, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUpdateCountdownText(string value)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(value);
		SendRPCInternal("System.Void Crash::RpcUpdateCountdownText(System.String)", 1974833200, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private float GetRandomCrashPoint(float r)
	{
		r = Mathf.Max(r, 0.001f);
		return Mathf.Clamp(1f / r, 1.001f, maxPoint);
	}

	[ClientRpc]
	private void RpcPlayMysteriousTickingNoise()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcPlayMysteriousTickingNoise()", -1397534610, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayCrashOverSFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcPlayCrashOverSFX()", 1728586995, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetCrashColors()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcSetCrashColors()", 2139037266, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcStartRiseLoop()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcStartRiseLoop()", 497445318, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcStopRiseLoop()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Crash::RpcStopRiseLoop()", -1665586290, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator ContinuousCurveUpdate()
	{
		while (_hasStarted && !_hasCrashed)
		{
			if (Mathf.Abs(_multiplier - lastUpdateMultiplier) > 0.01f)
			{
				UpdateExponentialCurve();
				lastUpdateMultiplier = _multiplier;
			}
			yield return new WaitForSeconds(0.05f);
		}
		if (_hasCrashed)
		{
			UpdateExponentialCurve();
		}
		curveUpdateCoroutine = null;
	}

	private void Update()
	{
		if (base.isClient && _hasStarted && !_hasCrashed && curveUpdateCoroutine == null)
		{
			curveUpdateCoroutine = StartCoroutine(ContinuousCurveUpdate());
		}
	}

	private void InitializeLine()
	{
		if (multiplierLine == null)
		{
			GameObject gameObject = new GameObject("MultiplierLine");
			Transform parent = ((lineGraphParent != null) ? lineGraphParent : base.transform);
			gameObject.transform.SetParent(parent);
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = Vector3.one;
			multiplierLine = gameObject.AddComponent<Polyline>();
			multiplierLine.Thickness = lineThickness;
			multiplierLine.ThicknessSpace = ThicknessSpace.Meters;
			multiplierLine.Geometry = PolylineGeometry.Billboard;
			multiplierLine.Joins = PolylineJoins.Round;
			multiplierLine.BlendMode = ShapesBlendMode.Opaque;
			multiplierLine.Closed = false;
			multiplierLine.Color = lineColor;
			multiplierLine.gameObject.SetActive(value: false);
		}
	}

	private void UpdateExponentialCurve()
	{
		if (multiplierLine == null || !_hasStarted)
		{
			return;
		}
		float num = (_hasCrashed ? currentMultiplierValue : _multiplier);
		if (num <= 0f)
		{
			num = 1f;
		}
		float num2 = Mathf.Clamp01(CalculateElapsedTimeFromMultiplier(num) / curveDrawDuration);
		if (num2 <= 0f)
		{
			if (multiplierLine != null)
			{
				multiplierLine.gameObject.SetActive(value: false);
			}
			if (multiplierText != null)
			{
				multiplierText.gameObject.SetActive(value: false);
			}
			return;
		}
		float a = 0f;
		float a2 = 0f;
		float b = timeAxisLength * (1f - endPointOffsetX);
		float b2 = multiplierAxisHeight * (1f - endPointOffsetY);
		float value = minCurvePower + (num - 1f) * curvePowerMultiplier;
		value = Mathf.Clamp(value, minCurvePower, maxCurvePower);
		int num3 = Mathf.Clamp(Mathf.CeilToInt((float)curveResolution * num2), 2, 50);
		List<Vector3> list = new List<Vector3>(num3);
		Color color = (_hasCrashed ? Color.red : lineColor);
		for (int i = 0; i < num3; i++)
		{
			float num4 = (float)i / (float)(num3 - 1) * num2;
			float x = Mathf.Lerp(a, b, num4);
			float t = Mathf.Pow(num4, value);
			float y = Mathf.Lerp(a2, b2, t);
			list.Add(new Vector3(x, y, 0f));
		}
		multiplierLine.SetPoints(list);
		multiplierLine.Color = color;
		multiplierLine.gameObject.SetActive(value: true);
		if (rocketImage != null && list.Count >= 2)
		{
			Transform transform = ((lineGraphParent != null) ? lineGraphParent : base.transform);
			rocketImage.position = transform.TransformPoint(list[list.Count - 1]);
			Vector3 vector = list[list.Count - 1];
			Vector3 vector2 = list[list.Count - 2];
			Vector3 vector3 = vector - vector2;
			float num5 = Mathf.Atan2(vector3.y, vector3.x) * 57.29578f;
			rocketImage.localRotation = Quaternion.Euler(0f, 0f, 0f - num5 + 90f);
			rocketImage.gameObject.SetActive(value: true);
		}
		multiplierText.gameObject.SetActive(value: true);
		potentialWinningText.gameObject.SetActive(value: true);
		if (showBorders && !bordersInitialized)
		{
			UpdateBorders();
			bordersInitialized = true;
		}
	}

	private void ClearLine()
	{
		if (curveUpdateCoroutine != null)
		{
			StopCoroutine(curveUpdateCoroutine);
			curveUpdateCoroutine = null;
		}
		if (multiplierLine != null)
		{
			multiplierLine.gameObject.SetActive(value: false);
		}
		if (multiplierText != null)
		{
			multiplierText.gameObject.SetActive(value: false);
		}
		potentialWinningText.gameObject.SetActive(value: false);
		rocketImage.gameObject.SetActive(value: false);
		currentMultiplierValue = 1f;
		lastUpdateMultiplier = 0f;
		bordersInitialized = false;
		DestroyBorders();
	}

	private void UpdateBorders()
	{
		if (!bordersInitialized)
		{
			Transform parent = ((lineGraphParent != null) ? lineGraphParent : base.transform);
			if (xAxisBorder == null)
			{
				GameObject gameObject = new GameObject("XAxisBorder");
				gameObject.transform.SetParent(parent);
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
				gameObject.transform.localScale = Vector3.one;
				xAxisBorder = gameObject.AddComponent<Polyline>();
				xAxisBorder.SetPoints(new List<Vector3>
				{
					new Vector3(0f, 0f, 0f),
					new Vector3(timeAxisLength, 0f, 0f)
				});
				xAxisBorder.Thickness = borderThickness;
				xAxisBorder.ThicknessSpace = ThicknessSpace.Meters;
				xAxisBorder.Geometry = PolylineGeometry.Billboard;
				xAxisBorder.Joins = PolylineJoins.Round;
				xAxisBorder.BlendMode = ShapesBlendMode.Opaque;
				xAxisBorder.Closed = false;
				xAxisBorder.Color = borderColor;
				xAxisBorder.gameObject.SetActive(value: true);
			}
			if (yAxisBorder == null)
			{
				GameObject gameObject2 = new GameObject("YAxisBorder");
				gameObject2.transform.SetParent(parent);
				gameObject2.transform.localPosition = Vector3.zero;
				gameObject2.transform.localRotation = Quaternion.identity;
				gameObject2.transform.localScale = Vector3.one;
				yAxisBorder = gameObject2.AddComponent<Polyline>();
				yAxisBorder.SetPoints(new List<Vector3>
				{
					new Vector3(0f, 0f, 0f),
					new Vector3(0f, multiplierAxisHeight, 0f)
				});
				yAxisBorder.Thickness = borderThickness;
				yAxisBorder.ThicknessSpace = ThicknessSpace.Meters;
				yAxisBorder.Geometry = PolylineGeometry.Billboard;
				yAxisBorder.Joins = PolylineJoins.Round;
				yAxisBorder.BlendMode = ShapesBlendMode.Opaque;
				yAxisBorder.Closed = false;
				yAxisBorder.Color = borderColor;
				yAxisBorder.gameObject.SetActive(value: true);
			}
			if (topBorder == null)
			{
				GameObject gameObject3 = new GameObject("TopBorder");
				gameObject3.transform.SetParent(parent);
				gameObject3.transform.localPosition = Vector3.zero;
				gameObject3.transform.localRotation = Quaternion.identity;
				gameObject3.transform.localScale = Vector3.one;
				topBorder = gameObject3.AddComponent<Polyline>();
				topBorder.SetPoints(new List<Vector3>
				{
					new Vector3(0f, multiplierAxisHeight, 0f),
					new Vector3(timeAxisLength, multiplierAxisHeight, 0f)
				});
				topBorder.Thickness = borderThickness;
				topBorder.ThicknessSpace = ThicknessSpace.Meters;
				topBorder.Geometry = PolylineGeometry.Billboard;
				topBorder.Joins = PolylineJoins.Round;
				topBorder.BlendMode = ShapesBlendMode.Opaque;
				topBorder.Closed = false;
				topBorder.Color = borderColor;
				topBorder.gameObject.SetActive(value: true);
			}
			if (rightBorder == null)
			{
				GameObject gameObject4 = new GameObject("RightBorder");
				gameObject4.transform.SetParent(parent);
				gameObject4.transform.localPosition = Vector3.zero;
				gameObject4.transform.localRotation = Quaternion.identity;
				gameObject4.transform.localScale = Vector3.one;
				rightBorder = gameObject4.AddComponent<Polyline>();
				rightBorder.SetPoints(new List<Vector3>
				{
					new Vector3(timeAxisLength, 0f, 0f),
					new Vector3(timeAxisLength, multiplierAxisHeight, 0f)
				});
				rightBorder.Thickness = borderThickness;
				rightBorder.ThicknessSpace = ThicknessSpace.Meters;
				rightBorder.Geometry = PolylineGeometry.Billboard;
				rightBorder.Joins = PolylineJoins.Round;
				rightBorder.BlendMode = ShapesBlendMode.Opaque;
				rightBorder.Closed = false;
				rightBorder.Color = borderColor;
				rightBorder.gameObject.SetActive(value: true);
			}
		}
	}

	private void DestroyBorders()
	{
		if (xAxisBorder != null && xAxisBorder.gameObject != null)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(xAxisBorder.gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(xAxisBorder.gameObject);
			}
			xAxisBorder = null;
		}
		if (yAxisBorder != null && yAxisBorder.gameObject != null)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(yAxisBorder.gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(yAxisBorder.gameObject);
			}
			yAxisBorder = null;
		}
		if (topBorder != null && topBorder.gameObject != null)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(topBorder.gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(topBorder.gameObject);
			}
			topBorder = null;
		}
		if (rightBorder != null && rightBorder.gameObject != null)
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(rightBorder.gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(rightBorder.gameObject);
			}
			rightBorder = null;
		}
	}

	private float CalculateElapsedTimeFromMultiplier(float multiplier)
	{
		if (multiplier <= 1f)
		{
			return 0f;
		}
		return Mathf.Log(multiplier) / raiseSpeed;
	}

	public Crash()
	{
		_Mirror_SyncVarHookDelegate__multiplier = OnMultiplierChanged;
		_Mirror_SyncVarHookDelegate__hasStarted = OnHasStartedChanged;
		_Mirror_SyncVarHookDelegate__hasCrashed = OnHasCrashedChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcCashout__String(string text)
	{
		cashoutText.text = text;
	}

	protected static void InvokeUserCode_RpcCashout__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcCashout called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcCashout__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcShowCountdown()
	{
		if (countdownText != null)
		{
			countdownText.gameObject.SetActive(value: true);
		}
	}

	protected static void InvokeUserCode_RpcShowCountdown(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcShowCountdown called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcShowCountdown();
		}
	}

	protected void UserCode_RpcHideCountdown()
	{
		if (countdownText != null)
		{
			countdownText.gameObject.SetActive(value: false);
		}
	}

	protected static void InvokeUserCode_RpcHideCountdown(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcHideCountdown called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcHideCountdown();
		}
	}

	protected void UserCode_RpcUpdateCountdownText__String(string value)
	{
		if (countdownText != null)
		{
			countdownText.text = value;
		}
	}

	protected static void InvokeUserCode_RpcUpdateCountdownText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateCountdownText called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcUpdateCountdownText__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcPlayMysteriousTickingNoise()
	{
		SFXManager.SFXOneShot(sfxTick, base.transform.position);
	}

	protected static void InvokeUserCode_RpcPlayMysteriousTickingNoise(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayMysteriousTickingNoise called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcPlayMysteriousTickingNoise();
		}
	}

	protected void UserCode_RpcPlayCrashOverSFX()
	{
		SFXManager.SFXOneShot(sfxCrashOver, base.transform.position);
	}

	protected static void InvokeUserCode_RpcPlayCrashOverSFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayCrashOverSFX called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcPlayCrashOverSFX();
		}
	}

	protected void UserCode_RpcSetCrashColors()
	{
		if (multiplierText != null)
		{
			multiplierText.color = Color.red;
		}
		if (multiplierLine != null)
		{
			multiplierLine.Color = Color.red;
		}
	}

	protected static void InvokeUserCode_RpcSetCrashColors(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCrashColors called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcSetCrashColors();
		}
	}

	protected void UserCode_RpcStartRiseLoop()
	{
		InitializeLine();
		if (multiplierText != null)
		{
			multiplierText.color = Color.white;
		}
		if (multiplierLine != null)
		{
			multiplierLine.Color = lineColor;
		}
		sfxLoop.LoopSFX(play: true);
		if (showBorders)
		{
			UpdateBorders();
		}
	}

	protected static void InvokeUserCode_RpcStartRiseLoop(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartRiseLoop called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcStartRiseLoop();
		}
	}

	protected void UserCode_RpcStopRiseLoop()
	{
		sfxLoop.LoopSFX(play: false);
		if (curveUpdateCoroutine != null)
		{
			StopCoroutine(curveUpdateCoroutine);
			curveUpdateCoroutine = null;
		}
	}

	protected static void InvokeUserCode_RpcStopRiseLoop(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStopRiseLoop called on server.");
		}
		else
		{
			((Crash)obj).UserCode_RpcStopRiseLoop();
		}
	}

	static Crash()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcCashout(System.String)", InvokeUserCode_RpcCashout__String);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcShowCountdown()", InvokeUserCode_RpcShowCountdown);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcHideCountdown()", InvokeUserCode_RpcHideCountdown);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcUpdateCountdownText(System.String)", InvokeUserCode_RpcUpdateCountdownText__String);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcPlayMysteriousTickingNoise()", InvokeUserCode_RpcPlayMysteriousTickingNoise);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcPlayCrashOverSFX()", InvokeUserCode_RpcPlayCrashOverSFX);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcSetCrashColors()", InvokeUserCode_RpcSetCrashColors);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcStartRiseLoop()", InvokeUserCode_RpcStartRiseLoop);
		RemoteProcedureCalls.RegisterRpc(typeof(Crash), "System.Void Crash::RpcStopRiseLoop()", InvokeUserCode_RpcStopRiseLoop);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(_multiplier);
			writer.WriteBool(_hasStarted);
			writer.WriteBool(_hasCrashed);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteFloat(_multiplier);
		}
		if ((syncVarDirtyBits & 0x10L) != 0L)
		{
			writer.WriteBool(_hasStarted);
		}
		if ((syncVarDirtyBits & 0x20L) != 0L)
		{
			writer.WriteBool(_hasCrashed);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _multiplier, _Mirror_SyncVarHookDelegate__multiplier, reader.ReadFloat());
			GeneratedSyncVarDeserialize(ref _hasStarted, _Mirror_SyncVarHookDelegate__hasStarted, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref _hasCrashed, _Mirror_SyncVarHookDelegate__hasCrashed, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _multiplier, _Mirror_SyncVarHookDelegate__multiplier, reader.ReadFloat());
		}
		if ((num & 0x10L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _hasStarted, _Mirror_SyncVarHookDelegate__hasStarted, reader.ReadBool());
		}
		if ((num & 0x20L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _hasCrashed, _Mirror_SyncVarHookDelegate__hasCrashed, reader.ReadBool());
		}
	}
}

}

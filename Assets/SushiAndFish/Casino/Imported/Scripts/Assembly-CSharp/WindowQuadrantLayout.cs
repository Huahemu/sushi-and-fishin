namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Diagnostics;
using global::System.Runtime.InteropServices;
using global::System.Text;
using global::UnityEngine;

public class WindowQuadrantLayout : MonoBehaviour
{
	internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const int SW_SHOWNORMAL = 1;

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern int GetWindowTextLength(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	private void Start()
	{
		int argInt = GetArgInt("--quadIndex", -1);
		if (argInt >= 0 && argInt <= 3)
		{
			Screen.fullScreenMode = FullScreenMode.Windowed;
			StartCoroutine(PositionRoutine(argInt));
		}
	}

	private IEnumerator PositionRoutine(int quadIndex)
	{
		IntPtr hwnd = IntPtr.Zero;
		for (int i = 0; i < 40; i++)
		{
			if (!(hwnd == IntPtr.Zero))
			{
				break;
			}
			hwnd = FindOwnTopLevelWindow();
			if (hwnd == IntPtr.Zero)
			{
				yield return new WaitForSeconds(0.05f);
			}
		}
		if (hwnd == IntPtr.Zero)
		{
			yield break;
		}
		int systemWidth = Display.main.systemWidth;
		int systemHeight = Display.main.systemHeight;
		int halfW = Mathf.Max(200, systemWidth / 2);
		int halfH = Mathf.Max(200, systemHeight / 2);
		int num = quadIndex % 2;
		int num2 = quadIndex / 2;
		int x = num * halfW;
		int y = num2 * halfH;
		Screen.SetResolution(halfW, halfH, FullScreenMode.Windowed);
		ShowWindow(hwnd, 1);
		SetForegroundWindow(hwnd);
		for (int i = 0; i < 12; i++)
		{
			MoveWindow(hwnd, x, y, halfW, halfH, bRepaint: true);
			yield return new WaitForSeconds(0.1f);
			if (GetWindowRect(hwnd, out var lpRect))
			{
				int num3 = Math.Abs(lpRect.Left - x);
				int num4 = Math.Abs(lpRect.Top - y);
				if (num3 <= 8 && num4 <= 8)
				{
					break;
				}
			}
		}
	}

	private IntPtr FindOwnTopLevelWindow()
	{
		uint myPid = (uint)Process.GetCurrentProcess().Id;
		IntPtr found = IntPtr.Zero;
		EnumWindows((IntPtr hWnd, IntPtr lParam) =>
		{
			if (!IsWindowVisible(hWnd))
			{
				return true;
			}
			GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
			if (lpdwProcessId != myPid)
			{
				return true;
			}
			int windowTextLength = GetWindowTextLength(hWnd);
			if (windowTextLength <= 0)
			{
				return true;
			}
			StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
			GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
			found = hWnd;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	private int GetArgInt(string key, int fallback)
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i].StartsWith(key))
			{
				string[] array = commandLineArgs[i].Split('=');
				if (array.Length == 2 && int.TryParse(array[1], out var result))
				{
					return result;
				}
			}
		}
		return fallback;
	}
}

}

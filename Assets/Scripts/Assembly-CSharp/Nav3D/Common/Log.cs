using System;
using System.Globalization;
using System.Text;

namespace Nav3D.Common
{
	public class Log
	{
		private string m_LoggerName;

		private LimitedSizeQueue<string> m_Records;

		public Log(string _LoggerName, int? _RecordsLimit)
		{
			m_LoggerName = _LoggerName;
			m_Records = new LimitedSizeQueue<string>(_RecordsLimit ?? int.MaxValue);
		}

		public void Write(string _Record)
		{
			m_Records.Enqueue("[" + DateTime.Now.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture) + "]: " + _Record);
		}

		public void WriteFormat(string _Record, params object[] _Args)
		{
			m_Records.Enqueue("[" + DateTime.Now.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture) + "]: " + string.Format(_Record, _Args));
		}

		public string GetText(out int _LinesCount)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Logger name: " + m_LoggerName);
			foreach (string record in m_Records)
			{
				stringBuilder.AppendLine(record);
			}
			_LinesCount = m_Records.Count;
			return stringBuilder.ToString();
		}

		public void Clear()
		{
			m_Records = new LimitedSizeQueue<string>(m_Records.Limit);
		}
	}
}

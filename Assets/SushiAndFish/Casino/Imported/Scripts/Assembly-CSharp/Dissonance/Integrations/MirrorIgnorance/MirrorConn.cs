using SushiAndFish.Casino.Original;
using global::System;
using global::Mirror;

namespace Dissonance.Integrations.MirrorIgnorance
{
	public readonly struct MirrorConn : IEquatable<MirrorConn>
	{
		public readonly NetworkConnection Connection;

		public MirrorConn(NetworkConnection connection)
		{
			this = default;
			Connection = connection;
		}

		public override int GetHashCode()
		{
			return Connection.GetHashCode();
		}

		public override string ToString()
		{
			return Connection.ToString();
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			if (obj is MirrorConn)
			{
				return Equals((MirrorConn)obj);
			}
			return false;
		}

		public bool Equals(MirrorConn other)
		{
			if (Connection == null)
			{
				if (other.Connection == null)
				{
					return true;
				}
				return false;
			}
			return Connection.Equals(other.Connection);
		}
	}
}

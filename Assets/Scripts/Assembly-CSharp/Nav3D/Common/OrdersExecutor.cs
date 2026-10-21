using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nav3D.Common
{
	public class OrdersExecutor<O> where O : IExecutable
	{
		private const string LOG_ORDER_ENQUEUED = "{0} instance ({1}) has enqueued";

		private readonly int m_MaxConcurrencyLevel;

		private volatile int m_CurrentAliveOrders;

		private readonly ConcurrentHashSet<O> m_OrdersRunning = new ConcurrentHashSet<O>();

		private readonly TaskFactory m_TaskFactory;

		private readonly TaskScheduler m_TaskScheduler;

		public int MaxConcurrencyLevel => m_MaxConcurrencyLevel;

		public int CurrentAliveOrders => m_CurrentAliveOrders;

		public OrdersExecutor(int _MaxConcurrencyLevel)
		{
			m_MaxConcurrencyLevel = _MaxConcurrencyLevel;
			m_TaskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, _MaxConcurrencyLevel).ConcurrentScheduler;
			m_TaskFactory = new TaskFactory(m_TaskScheduler);
		}

		public void EnqueueOrder(O _Order, Log _Log = null)
		{
			_Log?.WriteFormat("{0} instance ({1}) has enqueued", _Order.GetType().Name, _Order.GetHashCode());
			UpdateQueue(_Order);
		}

		public string GetRunningOrdersInfo()
		{
			return string.Join("\n", m_OrdersRunning.Select((O _Order) => _Order.GetExecutingStatus()));
		}

		private void UpdateQueue(O _Order)
		{
			m_TaskFactory.StartNew(() =>
			{
				ref O reference = ref _Order;
				Action onStart = MarkOrderAsRunning;
				Action onResolve = UnmarkOrderAsRunning;
				reference.Execute(onStart, onResolve);
			}, _Order.CancellationToken, TaskCreationOptions.LongRunning, m_TaskScheduler);
			void MarkOrderAsRunning()
			{
				Interlocked.Increment(ref m_CurrentAliveOrders);
				m_OrdersRunning.TryAdd(_Order);
			}
			void UnmarkOrderAsRunning()
			{
				Interlocked.Decrement(ref m_CurrentAliveOrders);
				m_OrdersRunning.TryRemove(_Order);
			}
		}
	}
}

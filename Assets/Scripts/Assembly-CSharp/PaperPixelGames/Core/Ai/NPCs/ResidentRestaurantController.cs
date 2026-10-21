using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentRestaurantController
	{
		private const float EatingAnimationStaggerSeconds = 0.5f;

		private const int MaxEatingAnimationStaggerIndex = 3;

		private readonly ResidentController _owner;

		private readonly ResidentRuntimeState _runtime;

		public ResidentRestaurantController(ResidentController owner, ResidentRuntimeState runtime)
		{
			_owner = owner;
			_runtime = runtime;
		}

		public bool BeginAssignment(RestaurantAssignmentRequest request)
		{
			if (_owner != null && _owner.Brain != null)
			{
				return _owner.Brain.TryAcceptRestaurantAssignment(request);
			}
			return false;
		}

		public void BeginLeave()
		{
			if (_runtime != null && _runtime.HasRestaurantAssignment)
			{
				_owner.Brain.CompleteRestaurantAssignment();
			}
		}

		public void BeginConveyorPaymentRequest()
		{
			if (_runtime != null && _runtime.HasRestaurantAssignment)
			{
				SetService(RestaurantActivityType.RequestPayment, ResidentRestaurantSubState.RequestingPayment);
			}
		}

		public void OnDiningTableStateChanged(RestaurantTableState state)
		{
			switch (state)
			{
			case RestaurantTableState.TakingOrder:
				SetService(RestaurantActivityType.Order, ResidentRestaurantSubState.Ordering);
				break;
			case RestaurantTableState.DeliveringOrder:
				SetService(RestaurantActivityType.Idle, ResidentRestaurantSubState.WaitingOrder);
				break;
			case RestaurantTableState.Eating:
				SetService(RestaurantActivityType.Eat, ResidentRestaurantSubState.Eating);
				BeginEatingAnimationDelay();
				break;
			case RestaurantTableState.AwaitingPayment:
				SetService(RestaurantActivityType.RequestPayment, ResidentRestaurantSubState.RequestingPayment);
				break;
			case RestaurantTableState.Empty:
			case RestaurantTableState.Cleaning:
				_owner.Leave();
				break;
			}
		}

		public void OnConveyorTableStateChanged(ConveyorBeltTableState state)
		{
			switch (state)
			{
			case ConveyorBeltTableState.TakingOrder:
				SetService(RestaurantActivityType.Idle, ResidentRestaurantSubState.WaitingOrder);
				break;
			case ConveyorBeltTableState.Eating:
				SetService(RestaurantActivityType.Eat, ResidentRestaurantSubState.Eating);
				BeginEatingAnimationDelay();
				break;
			case ConveyorBeltTableState.AwaitingPayment:
				BeginConveyorPaymentRequest();
				break;
			case ConveyorBeltTableState.Empty:
				_owner.Leave();
				break;
			}
		}

		private void SetService(RestaurantActivityType serviceState, ResidentRestaurantSubState subState)
		{
			if (serviceState != RestaurantActivityType.Eat)
			{
				_owner?.ClearRestaurantEatingAnimationDelay();
			}
			_runtime.RestaurantServiceState = serviceState;
			if (_runtime.HasArrived)
			{
				_runtime.RestaurantSubState = subState;
				_runtime.IsInRestaurant = true;
			}
		}

		private void BeginEatingAnimationDelay()
		{
			if (!(_owner == null) && _runtime != null)
			{
				int num = Mathf.Clamp(_runtime.RestaurantSeatIndex, 0, 3);
				_owner.BeginRestaurantEatingAnimationDelay((float)num * 0.5f);
			}
		}
	}
}

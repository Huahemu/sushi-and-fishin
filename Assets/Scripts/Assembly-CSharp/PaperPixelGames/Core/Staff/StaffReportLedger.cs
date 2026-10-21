using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffReportLedger
	{
		private readonly Dictionary<int, StaffReportEntry> entries = new Dictionary<int, StaffReportEntry>();

		public int TotalWages { get; private set; }

		public int TotalPurchases { get; private set; }

		public int TotalUnpaidWages { get; private set; }

		private StaffReportEntry Get(StaffEmployeeEntry employee)
		{
			if (!entries.TryGetValue(employee.EmployeeId, out var value))
			{
				Dictionary<int, StaffReportEntry> dictionary = entries;
				int employeeId = employee.EmployeeId;
				StaffReportEntry obj = new StaffReportEntry
				{
					EmployeeId = employee.EmployeeId,
					CustomizationId = employee.CustomizationId,
					Name = employee.Name.ToString(),
					Gender = employee.Gender,
					HasGender = true,
					Role = employee.Role
				};
				value = obj;
				dictionary.Add(employeeId, obj);
			}
			return value;
		}

		public void Tick(StaffEmployeeEntry employee, double seconds)
		{
			Get(employee).WorkSeconds += seconds;
		}

		public void Register(StaffEmployeeEntry employee)
		{
			Get(employee).Departure = employee.Departure;
		}

		public void Task(StaffEmployeeEntry employee)
		{
			Get(employee).Tasks++;
		}

		public void Production(StaffEmployeeEntry employee, int amount, double xp)
		{
			StaffReportEntry staffReportEntry = Get(employee);
			staffReportEntry.Tasks++;
			if (employee.Role == StaffRole.SushiChef)
			{
				staffReportEntry.SushiProduced += amount;
			}
			else
			{
				staffReportEntry.IngredientsProduced += amount;
			}
			staffReportEntry.ProgressionXp += xp;
		}

		public void Service(StaffEmployeeEntry employee, int served, int payments, int cleaned, double xp)
		{
			StaffReportEntry staffReportEntry = Get(employee);
			staffReportEntry.Tasks++;
			staffReportEntry.CustomersServed += served;
			staffReportEntry.PaymentsCollected += payments;
			staffReportEntry.TraysCleaned += cleaned;
			staffReportEntry.ProgressionXp += xp;
		}

		public void Wage(StaffEmployeeEntry employee, int stored)
		{
			Get(employee).WageStored += stored;
			TotalWages += stored;
		}

		public void UnpaidWage(StaffEmployeeEntry employee, int stored)
		{
			stored = Math.Max(0, stored);
			Get(employee).UnpaidWageStored += stored;
			TotalUnpaidWages += stored;
		}

		public void Purchase(StaffEmployeeEntry employee, int stored, double xp)
		{
			StaffReportEntry staffReportEntry = Get(employee);
			staffReportEntry.Packages++;
			staffReportEntry.SpentStored += stored;
			staffReportEntry.ProgressionXp += xp;
			TotalPurchases += stored;
		}

		public void Depart(StaffEmployeeEntry employee, StaffDeparture reason)
		{
			Get(employee).Departure = reason;
		}

		public void CompleteDeparture(StaffEmployeeEntry employee)
		{
			StaffReportEntry staffReportEntry = Get(employee);
			staffReportEntry.Departure = employee.Departure;
			staffReportEntry.HasLeft = true;
		}

		public List<StaffReportEntry> Snapshot()
		{
			List<StaffReportEntry> list = new List<StaffReportEntry>(entries.Count);
			foreach (StaffReportEntry value in entries.Values)
			{
				list.Add(value.Copy());
			}
			list.Sort((StaffReportEntry a, StaffReportEntry b) => a.EmployeeId.CompareTo(b.EmployeeId));
			return list;
		}

		public void Reset()
		{
			entries.Clear();
			TotalWages = 0;
			TotalPurchases = 0;
			TotalUnpaidWages = 0;
		}
	}
}

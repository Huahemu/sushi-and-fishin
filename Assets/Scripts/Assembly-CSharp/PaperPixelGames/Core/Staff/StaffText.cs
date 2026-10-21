using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Tablet;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffText
	{
		public static string Get(string key)
		{
			return TabletLocalization.Get("tablet.staff.v1." + key, Fallback(key));
		}

		public static string Status(StaffEmployeeEntry employee)
		{
			return Status(employee.Status, employee.StatusItemId.ToString());
		}

		public static string Status(StaffStatus status, string itemId = null)
		{
			if (status == StaffStatus.WaitingForIngredients && !string.IsNullOrEmpty(itemId))
			{
				ItemSO itemSO = StaffWorkplaceRegistry.Instance?.Database?.FindById(itemId);
				if (itemSO != null)
				{
					return string.Format(Get("WaitingForIngredient"), itemSO.LocalizedName);
				}
			}
			return Get(status.ToString());
		}

		public static string Turkish(string key)
		{
			switch (key)
			{
			case "StorageFull":
				return "Depolama Alanında Boş Yer Yok";
			case "stack_contents":
				return "{0} kap/kutu · {1} ürün · {2} boş";
			case "stack_items":
				return "{0} adet";
			case "stack_take":
				return "Sol tık: Ürün al";
			case "stack_take_empty":
				return "Sağ tık: Boş kutu al";
			case "stack_take_one":
				return "Sağ tık: Kutudan 1 ürün al";
			case "stack_take_empty_modified":
				return "Shift + sağ tık: Boş kutu al";
			case "stack_return":
				return "Sol tık: Eldeki uygun ürünü/kutuyu ekle";
			case "stack_next_type":
				return "E: Sonraki ürün";
			case "protect_kitchen_shelf":
				return "Mutfak rafındaki ürünleri koru";
			case "protect_kitchen_shelf_description":
				return "Stok görevlileri bu raftaki ürünleri depoya taşımaz.";
			case "collect_station_storage":
				return "Tezgâh altındaki ürünleri topla";
			case "collect_station_storage_description":
				return "Stok görevlileri ChefDesk ve Prep Station alt raflarındaki ürünleri depoya taşır.";
			case "all_stockers":
				return "Tüm stok görevlileri";
			case "collect_fillets":
				return "Filetoları Topla";
			case "candidate_unavailable":
				return "Bu aday artık mevcut değil.";
			case "role_limit_full":
				return "{0} çalışan sınırı dolu.";
			case "role_unavailable":
				return "{0} rolü şu anda kullanılamıyor.";
			case "appearance_unavailable":
				return "Bu rol için kullanılmayan karakter görünümü kalmadı.";
			case "employee_unavailable":
				return "Bu çalışan artık ekipte değil.";
			case "skill_level":
				return "Yetenek {0}/5";
			case "no_team":
				return "Henüz çalışan yok";
			case "no_candidate":
				return "Uygun aday yok";
			case "work_settings":
				return "Çalışma ayarları";
			case "daily_title":
				return "ÇALIŞAN RAPORU · GÜN {0}";
			case "daily_okay":
				return "Tamam";
			case "daily_summary":
				return "Ekipte kalan: {0}     Ödenen maaş: ${1:0.00}     Ödenemeyen maaş: ${2:0.00}\nAyrılan: {3}     Ayrılmayı bekleyen: {4}     Maaş nedeniyle: {5}     İşten çıkarılan: {6}\nPersonel alışverişi: ${7:0.00}";
			case "daily_empty":
				return "Bugün çalışan kaydı yok.";
			case "daily_reason_unpaid":
				return "Maaşı ödenemedi";
			case "daily_reason_fired":
				return "İşten çıkarıldı";
			case "daily_left":
				return "Ayrıldı";
			case "daily_unpaid_amount":
				return "Ödenemeyen maaş: ${0:0.00} (borç silindi)";
			case "TakingOrder":
				return "Sipariş Alıyor";
			case "CollectingDishes":
				return "Yemekleri Topluyor";
			case "DeliveringOrders":
				return "Siparişleri Servis Ediyor";
			case "CollectingPayment":
				return "Masa Ödemesini Alıyor";
			case "ScanningPayment":
				return "Conveyor Ödemesini Tarıyor";
			case "CollectingDirtyTrays":
				return "Kirli Tepsileri Topluyor";
			case "CleaningTrays":
				return "Tepsileri Temizliyor";
			case "ReturningTrays":
				return "Tepsileri Yerleştiriyor";
			case "NoTray":
				return "Tepsi Bekliyor";
			case "WaitingForDishes":
				return "Siparişin Tamamlanmasını Bekliyor";
			case "ReconfiguringTray":
				return "Tepsiyi Düzenliyor";
			case "take_orders":
				return "Sipariş Al";
			case "serve_orders":
				return "Servis Yap";
			case "waitress_report_card":
				return "{0} · {1}\nGörev: {2} · Çalışma: {3:0} dk · Maaş: ${4:0.00}\nServis: {5} · Ödeme: {9} · Temizlenen Tepsi: {10} · XP: {7:0}\n{8}";
			case "PreparingOrder":
				return "Sipariş Hazırlıyor";
			case "StockingConveyor":
				return "Conveyor İçin Hazırlıyor";
			case "SwappingGastroPan":
				return "Gastropan Değiştiriyor";
			case "WaitingForConveyorSlot":
				return "Boş Conveyor Yeri Bekliyor";
			case "ConveyorDisabled":
				return "Conveyor Kapalı";
			case "prepare_orders":
				return "Siparişleri Hazırla";
			case "stock_conveyor":
				return "Conveyor'ı Doldur";
			case "chef_report_card":
				return "{0} · {1}\nGörev: {2} · Çalışma: {3:0} dk · Maaş: ${4:0.00}\nÜretilen Sushi: {5} · XP: {7:0}\n{8}";
			case "CookingRice":
				return "Pirinç Pişiriyor";
			case "PreparingCucumber":
				return "Salatalık Hazırlıyor";
			case "CuttingLongStrip":
				return "Uzun Şerit Kesiyor";
			case "UsingFilletMachine":
				return "Fileto Makinesini Kullanıyor";
			case "WaitingForIngredients":
				return "Malzeme Bekliyor";
			case "WaitingForIngredient":
				return "Malzeme Bekliyor: {0}";
			case "ReadyStockFull":
				return "Hazır Stok Yeterli";
			case "WaitingForMachine":
				return "Boş Makine Bekliyor";
			case "SendingGastroPan":
				return "Gastropan Gönderiyor";
			case "rice_cooker":
				return "Pirinç Pişirici";
			case "fillet_machine":
				return "Fileto Makinesi";
			case "stop_work":
				return "Çalışmayı Durdur";
			case "resume_work":
				return "Çalışmaya Devam Et";
			case "ingredients_produced":
				return "Üretilen Malzeme";
			case "refresh":
				return "Tümünü Yenile ($1)";
			case "insufficient_money":
			case "InsufficientMoney":
				return "Restoranın parası yetersiz";
			case "missing_workplace":
			case "MissingWorkplace":
				return "Personelin çalışma alanı hazır değil";
			case "Unavailable":
				return "Bu rol daha sonra açılacak · Maaş alınmaz";
			case "CollectingItems":
				return "Malzemeleri Topluyor";
			case "StockingItems":
				return "Malzemeleri Yerleştiriyor";
			case "DiscardingEmptyBoxes":
				return "Boş Kutuları Çöpe Atıyor";
			case "GoingToGreenMarket":
				return "Yeşil Markete Gidiyor";
			case "GoingToBlueMarket":
				return "Mavi Markete Gidiyor";
			case "BuyingSupplies":
				return "Malzeme Satın Alıyor";
			case "ReturningToRestaurant":
				return "Restorana Dönüyor";
			case "ProductsLocked":
				return "Gerekli Ürünlerin Kilidi Açılmamış";
			case "MarketClosed":
				return "Market Kapalı";
			case "ProductUnavailable":
				return "Ürün Markette Mevcut Değil";
			case "PurchaseFailed":
				return "Ürün Teslim Alınamadı · Tekrar Denenecek";
			case "Leaving":
				return "Mevcut Görevden Sonra Ayrılacak";
			case "item_capacity":
				return "Malzeme Türü Kapasitesi";
			case "tray_capacity":
				return "Tepsi Kapasitesi";
			case "work_speed":
				return "Çalışma Hızı";
			case "fire_confirmation":
				return "{0} işten çıkarılsın mı? Ödenmemiş günlük maaşı alınır. Görevini güvenle bitirince ayrılır.";
			case "report":
				return "PERSONEL";
			case "prep_report_card":
				return "{0} · {1}\nGörev: {2} · Çalışma: {3:0} dk · Maaş: ${4:0.00}\nÜretilen Malzeme: {5} · XP: {7:0}\n{8}";
			case "report_card":
				return "{0} · {1}\nGörev: {2} · Çalışma: {3:0} dk · Maaş: ${4:0.00}\nPaket: {5} · Alışveriş: ${6:0.00} · XP: {7:0}\n{8}";
			case "Fired":
				return "İşten Çıkarıldı";
			case "Unpaid":
				return "Ayrıldı: Maaşı Ödenmedi";
			case "Waiting":
				return "Bekliyor";
			case "Stopped":
				return "Durduruldu";
			default:
				return Fallback(key);
			}
		}

		public static string Fallback(string key)
		{
			switch (key)
			{
			case "StorageFull":
				return "No Free Storage Slot";
			case "stack_contents":
				return "{0} containers · {1} contents · {2} empty";
			case "stack_items":
				return "{0} items";
			case "stack_take":
				return "Left click: Take product";
			case "stack_take_empty":
				return "Right click: Take empty container";
			case "stack_take_one":
				return "Right click: Take 1 item from box";
			case "stack_take_empty_modified":
				return "Shift + right click: Take empty container";
			case "stack_return":
				return "Left click: Add matching held item/box";
			case "stack_next_type":
				return "E: Next product";
			case "protect_kitchen_shelf":
				return "Protect items on the kitchen shelf";
			case "protect_kitchen_shelf_description":
				return "Stockers will not move items from this shelf to storage.";
			case "collect_station_storage":
				return "Collect items under workstations";
			case "collect_station_storage_description":
				return "Stockers move items from the lower shelves of the ChefDesk and Prep Station to storage.";
			case "all_stockers":
				return "All stockers";
			case "collect_fillets":
				return "Collect Fillets";
			case "candidate_unavailable":
				return "This candidate is no longer available.";
			case "role_limit_full":
				return "The {0} staff limit is full.";
			case "role_unavailable":
				return "The {0} role is not available.";
			case "appearance_unavailable":
				return "No unused appearance is available for this staff role.";
			case "employee_unavailable":
				return "This employee is no longer on the team.";
			case "skill_level":
				return "Skill {0}/5";
			case "no_team":
				return "No staff hired";
			case "no_candidate":
				return "No candidate available";
			case "work_settings":
				return "Work settings";
			case "daily_title":
				return "WORKERS REPORT · DAY {0}";
			case "daily_okay":
				return "Okay";
			case "daily_summary":
				return "Remaining team: {0}     Wages paid: ${1:0.00}     Wages unpaid: ${2:0.00}\nLeft: {3}     Awaiting departure: {4}     Unpaid departures: {5}     Fired: {6}\nStaff purchases: ${7:0.00}";
			case "daily_empty":
				return "No employee activity today.";
			case "daily_reason_unpaid":
				return "Wage unpaid";
			case "daily_reason_fired":
				return "Fired";
			case "daily_left":
				return "Left";
			case "daily_unpaid_amount":
				return "Unpaid wage: ${0:0.00} (waived)";
			case "TakingOrder":
				return "Taking Order";
			case "CollectingDishes":
				return "Collecting Dishes";
			case "DeliveringOrders":
				return "Delivering Orders";
			case "CollectingPayment":
				return "Collecting Table Payment";
			case "ScanningPayment":
				return "Scanning Conveyor Payment";
			case "CollectingDirtyTrays":
				return "Collecting Dirty Trays";
			case "CleaningTrays":
				return "Cleaning Trays";
			case "ReturningTrays":
				return "Returning Trays";
			case "NoTray":
				return "Blocked: No Tray";
			case "WaitingForDishes":
				return "Waiting for Complete Orders";
			case "ReconfiguringTray":
				return "Reconfiguring Tray";
			case "take_orders":
				return "Take Orders";
			case "serve_orders":
				return "Serve Orders";
			case "waitress_report_card":
				return "{0} · {1}\nTasks: {2} · Work: {3:0} min · Wage: ${4:0.00}\nServed: {5} · Payments: {9} · Cleaned Trays: {10} · XP: {7:0}\n{8}";
			case "PreparingOrder":
				return "Preparing Order";
			case "StockingConveyor":
				return "Stocking Conveyor";
			case "SwappingGastroPan":
				return "Swapping GastroPan";
			case "WaitingForConveyorSlot":
				return "Waiting for Conveyor Slot";
			case "ConveyorDisabled":
				return "Conveyor Disabled";
			case "prepare_orders":
				return "Prepare Orders";
			case "stock_conveyor":
				return "Stock Conveyor";
			case "chef_report_card":
				return "{0} · {1}\nTasks: {2} · Work: {3:0} min · Wage: ${4:0.00}\nSushi Produced: {5} · XP: {7:0}\n{8}";
			case "CookingRice":
				return "Cooking Rice";
			case "PreparingCucumber":
				return "Preparing Cucumber";
			case "CuttingLongStrip":
				return "Cutting Long Strip";
			case "UsingFilletMachine":
				return "Using Fillet Machine";
			case "WaitingForIngredients":
				return "Waiting for Ingredients";
			case "WaitingForIngredient":
				return "Waiting for Ingredients: {0}";
			case "ReadyStockFull":
				return "Ready Stock Sufficient";
			case "WaitingForMachine":
				return "Waiting for a Machine";
			case "SendingGastroPan":
				return "Sending GastroPan";
			case "rice_cooker":
				return "Rice Cooker";
			case "fillet_machine":
				return "Fillet Machine";
			case "stop_work":
				return "Stop Working";
			case "resume_work":
				return "Resume Working";
			case "ingredients_produced":
				return "Ingredients Produced";
			case "refresh":
				return "Refresh All ($1)";
			case "insufficient_money":
			case "InsufficientMoney":
				return "Not enough restaurant money";
			case "missing_workplace":
			case "MissingWorkplace":
				return "Staff workplace is not ready";
			case "Unavailable":
				return "Role coming later — no wages";
			case "CollectingItems":
				return "Collecting Items";
			case "StockingItems":
				return "Stocking Items";
			case "DiscardingEmptyBoxes":
				return "Discarding Empty Boxes";
			case "GoingToGreenMarket":
				return "Going to Green Market";
			case "GoingToBlueMarket":
				return "Going to Blue Market";
			case "BuyingSupplies":
				return "Buying Supplies";
			case "ReturningToRestaurant":
				return "Returning to Restaurant";
			case "ProductsLocked":
				return "Required Products Not Unlocked";
			case "MarketClosed":
				return "Market Closed";
			case "ProductUnavailable":
				return "Product Unavailable at Market";
			case "PurchaseFailed":
				return "Could Not Receive Product · Will Retry";
			case "Leaving":
				return "Leaving After Current Task";
			case "item_capacity":
				return "Item Type Capacity";
			case "tray_capacity":
				return "Tray Capacity";
			case "work_speed":
				return "Work Speed";
			case "fire_confirmation":
				return "Fire {0}? Any unpaid daily wage will be charged. They will leave after safely finishing their task.";
			case "report":
				return "STAFF";
			case "prep_report_card":
				return "{0} · {1}\nTasks: {2} · Work: {3:0} min · Wage: ${4:0.00}\nIngredients Produced: {5} · XP: {7:0}\n{8}";
			case "report_card":
				return "{0} · {1}\nTasks: {2} · Work: {3:0} min · Wage: ${4:0.00}\nPackages: {5} · Purchases: ${6:0.00} · XP: {7:0}\n{8}";
			case "Fired":
				return "Fired";
			case "Unpaid":
				return "Left: Unpaid Wage";
			default:
				return key;
			}
		}

		public static string Feedback(StaffFeedback feedback, StaffRole role)
		{
			string text;
			switch (feedback)
			{
			case StaffFeedback.RoleLevelLocked:
				return UnlockLevel((StaffManager.Instance != null) ? StaffManager.Instance.GetRoleDefinition(role).UnlockLevel : StaffRules.DefaultUnlockLevel(role));
			case StaffFeedback.InsufficientMoney:
				text = "insufficient_money";
				break;
			case StaffFeedback.CandidateUnavailable:
				text = "candidate_unavailable";
				break;
			case StaffFeedback.RoleUnavailable:
				text = "role_unavailable";
				break;
			case StaffFeedback.RoleLimitFull:
				text = "role_limit_full";
				break;
			case StaffFeedback.AppearanceUnavailable:
				text = "appearance_unavailable";
				break;
			case StaffFeedback.EmployeeUnavailable:
				text = "employee_unavailable";
				break;
			default:
				text = "missing_workplace";
				break;
			}
			string key = text;
			if (feedback != StaffFeedback.RoleUnavailable && feedback != StaffFeedback.RoleLimitFull)
			{
				return Get(key);
			}
			string staffRoleName = TabletLocalization.GetStaffRoleName(role, StaffCatalogSO.CreateFallbackDefinition(role).DisplayName);
			return string.Format(Get(key), staffRoleName);
		}

		public static string UnlockLevel(int level)
		{
			return TabletLocalization.Format("ui.market.unlocks_at_level", "Unlocks at Level {0}", level);
		}

		public static string SkillLabel(StaffRole role)
		{
			return Get(role switch
			{
				StaffRole.Waitress => "tray_capacity", 
				StaffRole.Stocker => "item_capacity", 
				_ => "work_speed", 
			});
		}

		public static string SkillValue(StaffEmployeeEntry entry)
		{
			if (entry.Role != StaffRole.Stocker && entry.Role != StaffRole.Waitress)
			{
				return $"{entry.WorkSpeed:0.00}x";
			}
			return entry.SkillLevel.ToString();
		}
	}
}

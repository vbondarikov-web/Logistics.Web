using System.Collections.ObjectModel;

namespace Logistics.Web.Models
{
    public class CalcRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Now;
        public string Loading { get; set; } = "";
        public string Unloading { get; set; } = "";
        public string Mode { get; set; } = "Руб/Км";
        public double Km { get; set; }
        public double RoundKm { get; set; }
        public double CalcOn { get; set; }
        public double Cost { get; set; }
        public double ZeroKm { get; set; }
        public double ZeroTon { get; set; }
        public double Offer { get; set; }
        public double Revenue { get; set; }
        public double Profit { get; set; }
        public double ProfitPct { get; set; }
        public string Detail { get; set; } = "";
        public string DateStr => Date.ToString("dd.MM.yyyy HH:mm");
        public string Route => Loading + " - " + Unloading;
    }

    public static class CalcHistoryStore
    {
        public static ObservableCollection<CalcRecord> Records { get; } = new();
    }

    public static class CalcSettings
    {
        public static double Fuel { get; set; } = 70;
        public static double Repair { get; set; } = 30000;
        public static double Platon { get; set; } = 3.5;
        public static double Admin { get; set; } = 5000;
        public static double Osago { get; set; } = 40000;
        public static double KaskoTrailer { get; set; } = 50000;
        public static double KaskoTruck { get; set; } = 150000;
        public static double LeasingTrailer { get; set; } = 80000;
        public static double LeasingTruck { get; set; } = 150000;
    }
}
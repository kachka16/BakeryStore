using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.Windows;

namespace StoreBakery
{
    public class HistoryOrderModel
    {
        public string OrderNumber { get; set; }
        public string TimeText { get; set; }
        public string DateText { get; set; }
        public string ProductsText { get; set; }
        public string PaymentType { get; set; }
        public string PaymentBackground { get; set; }
        public string PaymentForeground { get; set; }
        public string TotalDisplay { get; set; }
    }

    public partial class KasaHistoryWindows : Window
    {
        public ObservableCollection<HistoryOrderModel> HistoryList { get; } = new ObservableCollection<HistoryOrderModel>();

        public KasaHistoryWindows()
        {
            InitializeComponent();

            HistoryListView.ItemsSource = HistoryList;

            this.Activated += (s, e) => LoadCompletedOrders();

            LoadCompletedOrders();
        }

        private static string GetConnectionString()
        {
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bakery_db.db");
            return $"Data Source={dbPath};Version=3;";
        }

        private void LoadCompletedOrders()
        {
            HistoryList.Clear();

            try
            {
                using (var connection = new SQLiteConnection(GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT o.Id, o.OrderNumber, o.OrderTime, o.PaymentType, o.TotalAmount,
                               GROUP_CONCAT(i.ProductName, ', ') AS ProductsSummary
                        FROM Orders o
                        LEFT JOIN OrderItems i ON o.Id = i.OrderId
                        WHERE o.Status = 'Завершено'
                        GROUP BY o.Id
                        ORDER BY o.Id DESC;";

                    using (var cmd = new SQLiteCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string rawTime = reader["OrderTime"].ToString();
                            string timeText = rawTime;
                            string dateText = "";

                            if (DateTime.TryParseExact(rawTime, "yyyy-MM-dd HH:mm:ss",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            {
                                timeText = dt.ToString("HH:mm");
                                dateText = dt.ToString("dd.MM.yyyy");
                            }

                            string products = reader["ProductsSummary"] != DBNull.Value ? reader["ProductsSummary"].ToString() : "";
                            if (products.Length > 60)
                            {
                                products = products.Substring(0, 60) + "...";
                            }

                            string paymentType = reader["PaymentType"].ToString();
                            bool isCash = paymentType == "Готівка";

                            decimal total = Convert.ToDecimal(reader["TotalAmount"]);

                            HistoryList.Add(new HistoryOrderModel
                            {
                                OrderNumber = reader["OrderNumber"].ToString(),
                                TimeText = timeText,
                                DateText = dateText,
                                ProductsText = products,
                                PaymentType = paymentType,
                                PaymentBackground = isCash ? "#FFA5D6A7" : "#FFB7C2E0",
                                PaymentForeground = isCash ? "#FF1B5E20" : "#FF1A4A75",
                                TotalDisplay = $"{total:0.00} грн"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження історії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void exitKasa(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }

        private void SaleButton(object sender, RoutedEventArgs e)
        {
            KasaSaleWindow saleWind = new KasaSaleWindow();
            saleWind.Show();
            this.Hide();
        }

        private void StorageButton(object sender, RoutedEventArgs e)
        {
            KasaStorageWindow storWind = new KasaStorageWindow();
            storWind.Show();
            this.Hide();
        }

        private void OrderButton(object sender, RoutedEventArgs e)
        {
            KasaOrderWindow orderWind = new KasaOrderWindow();
            orderWind.Show();
            this.Hide();
        }
    }
}
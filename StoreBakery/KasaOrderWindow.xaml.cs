using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace StoreBakery
{
    public class OrderItemView
    {
        public string Name { get; set; }
        public string Details { get; set; }
        public string Sum { get; set; }
    }

    public partial class KasaOrderWindow : Window
    {
        public ObservableCollection<OrderModel> OrdersList { get; set; } = new ObservableCollection<OrderModel>();

        private int selectedOrderId = -1;

        public KasaOrderWindow()
        {
            InitializeComponent();

            this.DataContext = this;

            OrdersListView.ItemsSource = OrdersList;

            LoadOrdersFromDatabase();
        }

        private static string GetConnectionString()
        {
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bakery_db.db");
            return $"Data Source={dbPath};Version=3;";
        }

        private void LoadOrdersFromDatabase()
        {
            OrdersList.Clear();

            try
            {
                using (var connection = new SQLiteConnection(GetConnectionString()))
                {
                    connection.Open();

                    string query = @"
                        SELECT o.Id, o.OrderNumber, o.OrderTime, o.Status, o.TotalAmount,
                               GROUP_CONCAT(i.ProductName, ', ') AS ProductsSummary
                        FROM Orders o
                        LEFT JOIN OrderItems i ON o.Id = i.OrderId
                        WHERE o.Status <> 'Завершено'
                        GROUP BY o.Id
                        ORDER BY o.Id DESC;";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string rawProducts = reader["ProductsSummary"]?.ToString() ?? "";

                                string formattedProducts = rawProducts;
                                if (rawProducts.Length > 28)
                                {
                                    formattedProducts = rawProducts.Substring(0, 28) + "...";
                                }

                                OrdersList.Add(new OrderModel
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    OrderNumber = reader["OrderNumber"].ToString(),
                                    OrderTime = reader["OrderTime"].ToString(),
                                    Status = reader["Status"].ToString(),
                                    TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                                    ProductsText = formattedProducts
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження замовлень з БД: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is OrderModel order)
            {
                selectedOrderId = order.Id;

                if (LoadOrderDetails(order.Id))
                {
                    StatusPopup.Visibility = Visibility.Visible;
                }
            }
        }

        private bool LoadOrderDetails(int orderId)
        {
            try
            {
                using (var connection = new SQLiteConnection(GetConnectionString()))
                {
                    connection.Open();

                    string orderQuery = "SELECT OrderNumber, OrderTime, Status, PaymentType, TotalAmount FROM Orders WHERE Id = @id";
                    using (var cmd = new SQLiteCommand(orderQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", orderId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                MessageBox.Show("Замовлення не знайдено в базі даних.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return false;
                            }

                            string status = reader["Status"].ToString();

                            PopupOrderNumberText.Text = "Чек № " + reader["OrderNumber"].ToString();
                            PopupOrderTimeText.Text = reader["OrderTime"].ToString();
                            PopupPaymentText.Text = reader["PaymentType"].ToString();
                            PopupStatusText.Text = status;
                            PopupTotalText.Text = Convert.ToDecimal(reader["TotalAmount"]).ToString("0.00") + " грн";

                            CompleteOrderButton.IsEnabled = status != "Завершено";
                        }
                    }

                    var items = new List<OrderItemView>();
                    string itemsQuery = "SELECT ProductName, Quantity, Price FROM OrderItems WHERE OrderId = @id";
                    using (var cmd = new SQLiteCommand(itemsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", orderId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string name = reader["ProductName"].ToString();
                                decimal qty = Convert.ToDecimal(reader["Quantity"]);
                                decimal price = Convert.ToDecimal(reader["Price"]);

                                items.Add(new OrderItemView
                                {
                                    Name = name,
                                    Details = $"{qty} шт x {price:0.00} грн",
                                    Sum = $"{qty * price:0.00} грн"
                                });
                            }
                        }
                    }

                    PopupItemsControl.ItemsSource = items;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження деталей замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void CompleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrderId < 0) return;

            try
            {
                using (var connection = new SQLiteConnection(GetConnectionString()))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Orders SET Status = @status WHERE Id = @id";
                    using (var cmd = new SQLiteCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@status", "Завершено");
                        cmd.Parameters.AddWithValue("@id", selectedOrderId);
                        cmd.ExecuteNonQuery();
                    }
                }

                selectedOrderId = -1;
                StatusPopup.Visibility = Visibility.Collapsed;

                LoadOrdersFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка оновлення статусу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            selectedOrderId = -1;
            StatusPopup.Visibility = Visibility.Collapsed;
        }

        private void StorageButton(object sender, RoutedEventArgs e)
        {
            KasaStorageWindow storWind = new KasaStorageWindow();
            storWind.Show();
            this.Hide();
        }

        private void HistoryButton(object sender, RoutedEventArgs e)
        {
            KasaHistoryWindows historyWind = new KasaHistoryWindows();
            historyWind.Show();
            this.Hide();
        }

        private void SaleButton(object sender, RoutedEventArgs e)
        {
            KasaSaleWindow saleWind = new KasaSaleWindow();
            saleWind.Show();
            this.Hide();
        }

        private void exitKasa(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }
    }
}
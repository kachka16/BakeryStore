using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StoreBakery
{
    public class StorageProduct
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }

        public string QuantityDisplay => $"{Quantity} шт";
        public string PriceDisplay => $"{Price:0.00} грн";
    }

    public partial class KasaStorageWindow : Window
    {
        public ObservableCollection<StorageProduct> AllProducts { get; set; } = new ObservableCollection<StorageProduct>();

        private string lastSearch = "";

        public KasaStorageWindow()
        {
            InitializeComponent();

            StorageListView.ItemsSource = AllProducts;

            this.Activated += (s, e) => LoadAllProductsFromDatabase(lastSearch);

            LoadAllProductsFromDatabase();
        }

        private void LoadAllProductsFromDatabase(string searchQuery = "")
        {
            string connectionString = "Data Source=bakery_db.db;Version=3;";
            lastSearch = searchQuery ?? "";
            AllProducts.Clear();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = "SELECT Code, Name, NumbersStorage AS Quantity, Category, Price FROM Products";

                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        query += " WHERE Code LIKE @search OR Name LIKE @searchName OR Category LIKE @searchCategory";
                    }

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(searchQuery))
                        {
                            cmd.Parameters.AddWithValue("@search", "%" + searchQuery + "%");
                            cmd.Parameters.AddWithValue("@searchName", "%" + searchQuery + "%");
                            cmd.Parameters.AddWithValue("@searchCategory", "%" + searchQuery + "%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AllProducts.Add(new StorageProduct
                                {
                                    Code = reader["Code"] != DBNull.Value ? reader["Code"].ToString() : "",
                                    Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : "",
                                    Quantity = reader["Quantity"] != DBNull.Value ? Convert.ToDecimal(reader["Quantity"]) : 0,
                                    Category = reader["Category"] != DBNull.Value ? reader["Category"].ToString() : "",
                                    Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження складу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StorageProduct product)
            {
                string connectionString = "Data Source=bakery_db.db;Version=3;";

                try
                {
                    using (var connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();

                        string updateQuery = "UPDATE Products SET NumbersStorage = NumbersStorage + 1 WHERE Code = @code";
                        using (var cmd = new SQLiteCommand(updateQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", product.Code);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadAllProductsFromDatabase(lastSearch);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка оновлення кількості: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void exitKasa(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string searchText = textBox.Text.Trim();

            if (searchText == "Пошук товару" || string.IsNullOrEmpty(searchText))
            {
                LoadAllProductsFromDatabase();
                return;
            }

            LoadAllProductsFromDatabase(searchText);
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

        private void OrderButton(object sender, RoutedEventArgs e)
        {
            KasaOrderWindow orderWind = new KasaOrderWindow();
            orderWind.Show();
            this.Hide();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox textBox = sender as TextBox;
                string searchText = textBox?.Text.Trim();

                if (string.IsNullOrEmpty(searchText) || searchText == "Пошук товару")
                {
                    LoadAllProductsFromDatabase();
                    return;
                }

                LoadAllProductsFromDatabase(searchText);
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null && tb.Text == "Пошук товару")
            {
                tb.Text = "";
                tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            }
        }
    }
}
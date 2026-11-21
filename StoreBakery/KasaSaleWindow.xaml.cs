using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StoreBakery
{
    public partial class KasaSaleWindow : Window
    {
        private const string ConnStr = "Data Source=bakery_db.db;Version=3;";

        public static ObservableCollection<CartItem> CartItems { get; } = new ObservableCollection<CartItem>();

        private decimal totalSum = 0;
        private static bool isOpen = false;
        private static decimal totalSuma = 0;
        private bool isCashPayment = true;

        private static int shiftOrdersCount = 0;
        private static decimal totalVnesennya = 0.00m;
        private static decimal openingCash = 0.00m;
        private static decimal totalVidacha = 0.00m;
        private static decimal cashSalesSum = 0.00m;
        private static decimal cardSalesSum = 0.00m;

        public KasaSaleWindow()
        {
            InitializeComponent();
            CartListView.ItemsSource = CartItems;

            EnsureShiftTables();
            RefreshShiftTotals();

            UpdateText();
            RecalculateTotal();
        }


        private static void EnsureShiftTables()
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();

                string createCashMoves = @"
                    CREATE TABLE IF NOT EXISTS CashMoves (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Type TEXT NOT NULL,
                        Amount REAL NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );";

                string createZReports = @"
                    CREATE TABLE IF NOT EXISTS ZReports (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ClosedAt TEXT NOT NULL,
                        OrdersCount INTEGER NOT NULL,
                        CashSales REAL NOT NULL,
                        CardSales REAL NOT NULL,
                        Vnesennya REAL NOT NULL,
                        Vidacha REAL NOT NULL,
                        CashInBox REAL NOT NULL
                    );";

                using (var cmd = new SQLiteCommand(createCashMoves, connection))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(createZReports, connection))
                    cmd.ExecuteNonQuery();
            }
        }


        private void RefreshShiftTotals()
        {
            try
            {
                using (var connection = new SQLiteConnection(ConnStr))
                {
                    connection.Open();

                    string since;
                    using (var cmd = new SQLiteCommand("SELECT COALESCE(MAX(ClosedAt), '') FROM ZReports;", connection))
                    {
                        since = cmd.ExecuteScalar()?.ToString() ?? "";
                    }

                    string salesQuery = @"
                        SELECT COUNT(*),
                               COALESCE(SUM(CASE WHEN PaymentType = 'Готівка' THEN TotalAmount ELSE 0 END), 0),
                               COALESCE(SUM(CASE WHEN PaymentType = 'Картка' THEN TotalAmount ELSE 0 END), 0)
                        FROM Orders
                        WHERE OrderTime > @since;";

                    using (var cmd = new SQLiteCommand(salesQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@since", since);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                shiftOrdersCount = Convert.ToInt32(reader.GetValue(0));
                                cashSalesSum = Convert.ToDecimal(reader.GetValue(1));
                                cardSalesSum = Convert.ToDecimal(reader.GetValue(2));
                            }
                        }
                    }

                    string movesQuery = @"
                        SELECT COALESCE(SUM(CASE WHEN Type = 'Внесення' THEN Amount ELSE 0 END), 0),
                               COALESCE(SUM(CASE WHEN Type = 'Видача' THEN Amount ELSE 0 END), 0)
                        FROM CashMoves
                        WHERE CreatedAt > @since;";

                    using (var cmd = new SQLiteCommand(movesQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@since", since);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                totalVnesennya = Convert.ToDecimal(reader.GetValue(0));
                                totalVidacha = Convert.ToDecimal(reader.GetValue(1));
                            }
                        }
                    }

                    using (var cmd = new SQLiteCommand("SELECT sumaKoshti FROM koshti ORDER BY rowid DESC LIMIT 1;", connection))
                    {
                        object result = cmd.ExecuteScalar();
                        openingCash = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних зміни: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            UpdateKasaKoshtiUI();
        }

        private static void SaveCashMove(string type, decimal amount)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string sql = "INSERT INTO CashMoves (Type, Amount, CreatedAt) VALUES (@type, @amount, @created);";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@type", type);
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void SaveZReport(decimal cashInBox)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO ZReports (ClosedAt, OrdersCount, CashSales, CardSales, Vnesennya, Vidacha, CashInBox)
                    VALUES (@closedAt, @count, @cash, @card, @vnes, @vid, @box);";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@closedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@count", shiftOrdersCount);
                    cmd.Parameters.AddWithValue("@cash", cashSalesSum);
                    cmd.Parameters.AddWithValue("@card", cardSalesSum);
                    cmd.Parameters.AddWithValue("@vnes", totalVnesennya);
                    cmd.Parameters.AddWithValue("@vid", totalVidacha);
                    cmd.Parameters.AddWithValue("@box", cashInBox);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateText()
        {
            if (isOpen)
            {
                statusButton.Text = "Зміна: Відкрита";
            }
            else
            {
                statusButton.Text = "Зміна: Закрита";
            }
        }


        public bool AddProductToCart(string name, string code, decimal price, decimal quantity = 1)
        {
            var existingItem = CartItems.FirstOrDefault(i => i.Code == code);
            decimal inCart = existingItem != null ? existingItem.Quantity : 0;
            decimal stock = GetStock(code);

            if (inCart + quantity > stock)
            {
                MessageBox.Show($"На складі недостатньо товару \"{name}\". Доступно: {stock} шт, вже в чеку: {inCart} шт.",
                                "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                CartItems.Add(new CartItem { Name = name, Code = code, Price = price, Quantity = quantity });
            }

            RecalculateTotal();
            return true;
        }

        private static decimal GetStock(string code)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                using (var cmd = new SQLiteCommand("SELECT NumbersStorage FROM Products WHERE Code = @code", connection))
                {
                    cmd.Parameters.AddWithValue("@code", code);
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
            }
        }

        private void RecalculateTotal()
        {
            totalSum = CartItems.Sum(i => i.TotalAmount);

            if (TotalSumTextBlock != null)
            {
                TotalSumTextBlock.Text = totalSum.ToString("0.00") + " грн";
            }
        }

        private void DeleteCartItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CartItem item)
            {
                CartItems.Remove(item);
                RecalculateTotal();
            }
        }


        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchPopup == null) return;

            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string searchText = textBox.Text.Trim();

            if (string.IsNullOrEmpty(searchText) || searchText == "Пошук товару" || searchText.Length < 1)
            {
                SearchPopup.IsOpen = false;
                return;
            }

            var suggestions = new List<ProductSuggestion>();

            try
            {
                using (var connection = new SQLiteConnection(ConnStr))
                {
                    connection.Open();
                    string query = "SELECT Code, Name, Price FROM Products WHERE Code LIKE @search OR Name LIKE @searchName LIMIT 10";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@search", "%" + searchText + "%");
                        cmd.Parameters.AddWithValue("@searchName", "%" + searchText + "%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                suggestions.Add(new ProductSuggestion
                                {
                                    Code = reader["Code"].ToString(),
                                    Name = reader["Name"].ToString(),
                                    Price = Convert.ToDecimal(reader["Price"])
                                });
                            }
                        }
                    }
                }

                if (suggestions.Count > 0)
                {
                    SuggestionsListBox.ItemsSource = suggestions;
                    SearchPopup.IsOpen = true;
                }
                else
                {
                    SearchPopup.IsOpen = false;
                }
            }
            catch (Exception)
            {
                SearchPopup.IsOpen = false;
            }
        }

        private void SuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsListBox.SelectedItem is ProductSuggestion selectedProduct)
            {
                AddProductToCart(selectedProduct.Name, selectedProduct.Code, selectedProduct.Price, 1);

                SearchPopup.IsOpen = false;
                SearchTextBox.Text = "";
                SearchTextBox.Focus();
                SuggestionsListBox.SelectedItem = null;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox textBox = sender as TextBox;
                string searchText = textBox?.Text.Trim();

                if (string.IsNullOrEmpty(searchText) || searchText == "Пошук товару")
                    return;

                try
                {
                    using (var connection = new SQLiteConnection(ConnStr))
                    {
                        connection.Open();
                        string query = "SELECT Code, Name, Price FROM Products WHERE Code = @search OR Name = @searchName";

                        using (var cmd = new SQLiteCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@search", searchText);
                            cmd.Parameters.AddWithValue("@searchName", searchText);

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string code = reader["Code"].ToString();
                                    string name = reader["Name"].ToString();
                                    decimal price = Convert.ToDecimal(reader["Price"]);

                                    AddProductToCart(name, code, price, 1);
                                    SearchPopup.IsOpen = false;
                                    textBox.Text = "";
                                    textBox.Focus();
                                }
                                else
                                {
                                    if (SuggestionsListBox.Items.Count > 0 && SuggestionsListBox.Items[0] is ProductSuggestion firstSugg)
                                    {
                                        AddProductToCart(firstSugg.Name, firstSugg.Code, firstSugg.Price, 1);
                                        SearchPopup.IsOpen = false;
                                        textBox.Text = "";
                                        textBox.Focus();
                                    }
                                    else
                                    {
                                        MessageBox.Show("Товар не знайдено в базі даних!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка пошуку товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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


        private void PaymentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isOpen)
            {
                MessageBox.Show("Зміна закрита!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CartItems.Count == 0)
            {
                MessageBox.Show("Додайте товари.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TotalOrderTextBlock.Text = totalSum.ToString("0.00");
            isCashPayment = true;
            UpdatePaymentUI();
            PayConfirm.Visibility = Visibility.Visible;
        }

        private void CashButton_Click(object sender, RoutedEventArgs e)
        {
            isCashPayment = true;
            UpdatePaymentUI();
        }

        private void CardButton_Click(object sender, RoutedEventArgs e)
        {
            isCashPayment = false;
            UpdatePaymentUI();
        }

        private void UpdatePaymentUI()
        {
            if (isCashPayment)
            {
                CashButton.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF7CAC48");
                CashButton.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF2B5004");

                CardButton.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFE4E4F0");
                CardButton.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");

                ClientAmountTextBox.IsReadOnly = false;
                ClientAmountTextBox.Text = TotalOrderTextBlock.Text;
                ClientAmountTextBox.Focus();
                ClientAmountTextBox.SelectAll();
            }
            else
            {
                CardButton.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF7CAC48");
                CardButton.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF2B5004");

                CashButton.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFE4E4F0");
                CashButton.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF666666");

                ClientAmountTextBox.IsReadOnly = true;
                ClientAmountTextBox.Text = TotalOrderTextBlock.Text;
                ChangeTextBlock.Text = "0.00";
            }
        }

        private void ClientAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isCashPayment || ChangeTextBlock == null || TotalOrderTextBlock == null) return;

            if (decimal.TryParse(TotalOrderTextBlock.Text, out decimal orderTotal) &&
                decimal.TryParse(ClientAmountTextBox.Text, out decimal clientAmount))
            {
                decimal change = clientAmount - orderTotal;
                if (change >= 0)
                {
                    ChangeTextBlock.Text = change.ToString("0.00");
                    ChangeTextBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF584A75");
                }
            }
            else
            {
                ChangeTextBlock.Text = "0.00";
            }
        }

        private void CancelPayment_Click(object sender, RoutedEventArgs e)
        {
            PayConfirm.Visibility = Visibility.Collapsed;
        }

        private void ConfirmPayment_Click(object sender, RoutedEventArgs e)
        {
            if (!isOpen)
            {
                MessageBox.Show("Помилка. Зміна закрита!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CartItems.Count == 0)
            {
                MessageBox.Show("Додайте товари.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isCashPayment)
            {
                if (decimal.TryParse(TotalOrderTextBlock.Text.Replace('.', ','), out decimal orderTotal) &&
                    decimal.TryParse(ClientAmountTextBox.Text.Replace('.', ','), out decimal clientAmount))
                {
                    if (clientAmount < orderTotal)
                    {
                        MessageBox.Show("Сума коштів від клієнта менша за суму чеку!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Введено некоректну суму!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                using (var connection = new SQLiteConnection(ConnStr))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var item in CartItems)
                        {
                            decimal stock;
                            using (var stockCmd = new SQLiteCommand("SELECT NumbersStorage FROM Products WHERE Code = @code", connection, transaction))
                            {
                                stockCmd.Parameters.AddWithValue("@code", item.Code);
                                object res = stockCmd.ExecuteScalar();
                                stock = res == null || res == DBNull.Value ? 0 : Convert.ToDecimal(res);
                            }

                            if (stock < item.Quantity)
                            {
                                MessageBox.Show($"Недостатньо товару \"{item.Name}\" на складі. Доступно: {stock} шт, у чеку: {item.Quantity} шт.",
                                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return; 
                            }
                        }


                        string orderNumber = "-" + new Random().Next(10000000, 99999999).ToString();
                        string orderTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string paymentTypeStr = isCashPayment ? "Готівка" : "Картка";
                        string insertOrderQuery = "INSERT INTO Orders (OrderNumber, OrderTime, Status, Category, PaymentType, TotalAmount) VALUES (@num, @time, @status, @cat, @pay, @total); SELECT last_insert_rowid();";

                        long orderId;
                        using (var cmd = new SQLiteCommand(insertOrderQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@num", orderNumber);
                            cmd.Parameters.AddWithValue("@time", orderTime);
                            cmd.Parameters.AddWithValue("@status", "Готується");
                            cmd.Parameters.AddWithValue("@cat", "Випічка");
                            cmd.Parameters.AddWithValue("@pay", paymentTypeStr);
                            cmd.Parameters.AddWithValue("@total", totalSum);

                            orderId = (long)cmd.ExecuteScalar();
                        }

                        foreach (var item in CartItems)
                        {
                            string insertItemQuery = "INSERT INTO OrderItems (OrderId, ProductName, Quantity, Price) VALUES (@orderId, @name, @qty, @price)";
                            using (var itemCmd = new SQLiteCommand(insertItemQuery, connection, transaction))
                            {
                                itemCmd.Parameters.AddWithValue("@orderId", orderId);
                                itemCmd.Parameters.AddWithValue("@name", item.Name);
                                itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                                itemCmd.Parameters.AddWithValue("@price", item.Price);
                                itemCmd.ExecuteNonQuery();
                            }

                            string updateStockQuery = "UPDATE Products SET NumbersStorage = NumbersStorage - @qty WHERE Code = @code";
                            using (var stockCmd = new SQLiteCommand(updateStockQuery, connection, transaction))
                            {
                                stockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                                stockCmd.Parameters.AddWithValue("@code", item.Code);
                                stockCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }

                RefreshShiftTotals();

                totalSuma += totalSum;
                CartItems.Clear();
                RecalculateTotal();
                PayConfirm.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження в базу даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void OpenShift_Click(object sender, RoutedEventArgs e)
        {
            if (isOpen)
            {
                MessageBox.Show("Зміна вже відкрита!", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            isOpen = true;
            totalSuma = 0;
            UpdateText();
        }

        private void OpenVnesennya_Click(object sender, RoutedEventArgs e)
        {
            if (!isOpen) return;

            VnesennyaAmountTextBox.Text = "";
            VnesennyaPanel.Visibility = Visibility.Visible;
            VnesennyaAmountTextBox.Focus();
        }

        private void OpenVidacha_Click(object sender, RoutedEventArgs e)
        {
            if (!isOpen) return;

            VidachaAmountTextBox.Text = "";
            VidachaPanel.Visibility = Visibility.Visible;
            VidachaAmountTextBox.Focus();
        }

        private void ConfirmVidacha_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(VidachaAmountTextBox.Text.Replace('.', ','), out decimal sum) && sum > 0)
            {
                decimal currentCashInBox = openingCash + totalVnesennya + cashSalesSum - totalVidacha;

                if (sum > currentCashInBox)
                {
                    MessageBox.Show($"Неможливо виконати видачу! Сума видачі ({sum:0.00} грн) перевищує наявну готівку в касі ({currentCashInBox:0.00} грн).",
                                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    SaveCashMove("Видача", sum);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка збереження видачі: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                RefreshShiftTotals();
                VidachaPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Введено некоректну суму!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ConfirmVnesennya_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(VnesennyaAmountTextBox.Text.Replace('.', ','), out decimal sum) && sum > 0)
            {
                try
                {
                    SaveCashMove("Внесення", sum);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка збереження внесення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                RefreshShiftTotals();
                VnesennyaPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelVidacha_Click(object sender, RoutedEventArgs e)
        {
            VidachaPanel.Visibility = Visibility.Collapsed;
        }

        private void CancelVnesennya_Click(object sender, RoutedEventArgs e)
        {
            VnesennyaPanel.Visibility = Visibility.Collapsed;
        }


        private void UpdateKasaKoshtiUI()
        {
            if (VnesennyaSumText == null) return;

            VnesennyaSumText.Text = totalVnesennya.ToString("0.00");
            VidachaSumText.Text = totalVidacha.ToString("0.00");
            CashSalesSumText.Text = cashSalesSum.ToString("0.00");
            CardSalesSumText.Text = cardSalesSum.ToString("0.00");

            decimal totalCashInBox = openingCash + totalVnesennya + cashSalesSum - totalVidacha;
            TotalCashInBoxText.Text = totalCashInBox.ToString("0.00");
        }

        private void SaveKoshtiToDatabase(decimal totalCashInBox)
        {
            try
            {
                using (var connection = new SQLiteConnection(ConnStr))
                {
                    connection.Open();

                    string insertQuery = @"
                        INSERT INTO koshti (sumaVnesennya, sumaVinesenya, sumaOplatGotivka, sumaOplatKarta, sumaKoshti)
                        VALUES (@vnesennya, @vidacha, @gotivka, @karta, @sumaKoshti);";

                    using (var cmd = new SQLiteCommand(insertQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@vnesennya", totalVnesennya);
                        cmd.Parameters.AddWithValue("@vidacha", totalVidacha);
                        cmd.Parameters.AddWithValue("@gotivka", cashSalesSum);
                        cmd.Parameters.AddWithValue("@karta", cardSalesSum);
                        cmd.Parameters.AddWithValue("@sumaKoshti", totalCashInBox);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження коштів у БД: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void koshtiOpen(object sender, RoutedEventArgs e)
        {
            kasaKoshti.Visibility = Visibility.Visible;
        }

        private void CloseKasaKoshti_Click(object sender, RoutedEventArgs e)
        {
            kasaKoshti.Visibility = Visibility.Collapsed;
        }


        private void CloseShift_Click(object sender, RoutedEventArgs e)
        {
            if (!isOpen)
            {
                MessageBox.Show("Зміна ще не відкрита!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PopupZConfirm.Visibility = Visibility.Visible;
        }

        private void ConfirmCloseShift_Click(object sender, RoutedEventArgs e)
        {
            PopupZConfirm.Visibility = Visibility.Collapsed;

            RefreshShiftTotals();

            ZChecksText.Text = shiftOrdersCount.ToString();
            ZCashText.Text = cashSalesSum.ToString("0.00");
            ZCardText.Text = cardSalesSum.ToString("0.00");
            ZVnesennyaText.Text = totalVnesennya.ToString("0.00");
            ZVidachaText.Text = totalVidacha.ToString("0.00");

            ZPaperSuma.Visibility = Visibility.Visible;
        }

        private void CancelCloseShift_Click(object sender, RoutedEventArgs e)
        {
            PopupZConfirm.Visibility = Visibility.Collapsed;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RefreshShiftTotals();

            decimal endingCash = openingCash + totalVnesennya + cashSalesSum - totalVidacha;

            SaveKoshtiToDatabase(endingCash);

            try
            {
                SaveZReport(endingCash);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження Z-звіту: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshShiftTotals();

            ZPaperSuma.Visibility = Visibility.Collapsed;
            isOpen = false;
            UpdateText();
        }


        private void KasaButton_Click(object sender, RoutedEventArgs e)
        {
            DefaultMenuPanel.Visibility = Visibility.Collapsed;
            BottomKasaPanel.Visibility = Visibility.Collapsed;
            KasaSubMenuPanel.Visibility = Visibility.Visible;
            UpdateText();
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            DefaultMenuPanel.Visibility = Visibility.Visible;
            BottomKasaPanel.Visibility = Visibility.Visible;
            KasaSubMenuPanel.Visibility = Visibility.Collapsed;
        }

        private void exitKasa(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }

        private void HistoryButton(object sender, RoutedEventArgs e)
        {
            KasaHistoryWindows historyWind = new KasaHistoryWindows();
            historyWind.Show();
            this.Hide();
        }

        private void OrderButton(object sender, RoutedEventArgs e)
        {
            KasaOrderWindow orderWind = new KasaOrderWindow();
            orderWind.Show();
            this.Hide();
        }

        private void StorageButton(object sender, RoutedEventArgs e)
        {
            KasaStorageWindow storWind = new KasaStorageWindow();
            storWind.Show();
            this.Hide();
        }

        private void Button_Click(object sender, RoutedEventArgs e) { }
        private void statusButton_Click(object sender, RoutedEventArgs e) { }
    }
}
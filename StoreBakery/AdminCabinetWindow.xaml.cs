using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoreBakery
{
    public class KasaAccount
    {
        public int Id { get; set; }
        public string Login { get; set; }
    }

    public class AdminProduct
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }

    public class ZReportRow
    {
        public string ClosedAtText { get; set; }
        public int Checks { get; set; }
        public decimal CashSales { get; set; }
        public decimal CardSales { get; set; }
        public decimal Vnesennya { get; set; }
        public decimal Vidacha { get; set; }
        public decimal CashInBox { get; set; }
    }

    public class ProductColumns
    {
        public string Code, Name, Category, Price, Quantity;
        public string Id;
    }

    public partial class AdminCabinetWindow : Window
    {
        private readonly ObservableCollection<ZReportRow> zReportRows = new ObservableCollection<ZReportRow>();
        private readonly ObservableCollection<AdminProduct> products = new ObservableCollection<AdminProduct>();
        private readonly ObservableCollection<KasaAccount> cashiers = new ObservableCollection<KasaAccount>();
        private readonly ObservableCollection<string> categories = new ObservableCollection<string>();

        private bool isEditMode;
        private static ProductColumns productColumns;

        public AdminCabinetWindow()
        {
            InitializeComponent();

            ZReportsGrid.ItemsSource = zReportRows;
            ProductsList.ItemsSource = products;
            CashiersList.ItemsSource = cashiers;
            CategoryBox.ItemsSource = categories;

            Date1Picker.SelectedDate = DateTime.Today.AddDays(-6);
            Date2Picker.SelectedDate = DateTime.Today;

            SetActiveMenu(ReportsMenuBtn);

            bool schemaOk = true;
            Safe(() =>
            {
                EnsureCategoriesTable();
            }, "підготовка БД", () => schemaOk = false);

            if (schemaOk)
            {
                LoadCashiers();
                LoadProducts(string.Empty);
                LoadCategories();
                LoadReports();
            }
        }

        private void Safe(Action action, string where = "", Action onError = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                onError?.Invoke();
                MessageBox.Show(
                    $"Помилка ({where}):\n{ex.Message}",
                    "Помилка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void Info(string text)
        {
            MessageBox.Show(text, "Адмін панель", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string Stamp(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static bool TryParseMoney(string text, out decimal value)
        {
            string normalized = (text ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static decimal ReadDecimal(SQLiteDataReader reader, string column)
        {
            object value = reader[column];
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static string ParseClosedAt(string raw)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return dt.ToString("dd.MM.yyyy HH:mm");
            }
            return raw;
        }

        private void EnsureCategoriesTable()
        {
            using (var connection = AppDb.Open())
            using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Categories (Name TEXT PRIMARY KEY)", connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static ProductColumns GetProductColumns(SQLiteConnection connection)
        {
            if (productColumns != null) return productColumns;

            var cols = new List<Tuple<string, string>>();
            using (var cmd = new SQLiteCommand("PRAGMA table_info(Products)", connection))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    cols.Add(Tuple.Create(
                        r["name"].ToString(),
                        (r["type"]?.ToString() ?? "").ToUpperInvariant()));
                }
            }

            Func<string[], string> find = candidates =>
            {
                foreach (string c in candidates)
                {
                    var hit = cols.FirstOrDefault(x => string.Equals(x.Item1, c, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit.Item1;
                }
                return null;
            };

            var result = new ProductColumns
            {
                Id = find(new[] { "Id" }),
                Code = find(new[] { "Code", "Kod", "Barcode", "ProductCode" }),
                Name = find(new[] { "Name", "Title", "ProductName" }),
                Category = find(new[] { "Category", "Categoria", "Group" }),
                Price = find(new[] { "Price", "Cost", "Cina" }),
                Quantity = find(new[] { "Quantity", "Qty", "Stock", "Amount", "Count", "Balance", "Remainder", "Remains", "Kilkist" })
            };

            if (result.Quantity == null)
            {
                var numeric = cols.FirstOrDefault(c =>
                    !string.Equals(c.Item1, result.Id, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Item1, result.Price, StringComparison.OrdinalIgnoreCase) &&
                    (c.Item2.Contains("REAL") || c.Item2.Contains("INT") || c.Item2.Contains("NUM") || c.Item2.Contains("DEC")));
                if (numeric != null) result.Quantity = numeric.Item1;
            }

            string all = string.Join(", ", cols.Select(c => c.Item1));
            if (result.Code == null) throw new Exception("Не знайдено колонку коду в Products. Колонки: " + all);
            if (result.Name == null) throw new Exception("Не знайдено колонку назви в Products. Колонки: " + all);
            if (result.Category == null) throw new Exception("Не знайдено колонку категорії в Products. Колонки: " + all);
            if (result.Price == null) throw new Exception("Не знайдено колонку ціни в Products. Колонки: " + all);
            if (result.Quantity == null) throw new Exception("Не знайдено колонку кількості в Products. Колонки: " + all);

            productColumns = result;
            return result;
        }

        private void ShowReports_Click(object sender, RoutedEventArgs e)
        {
            LoadReports();
        }

        private void LoadReports()
        {
            Safe(() =>
            {
                zReportRows.Clear();

                if (Date1Picker.SelectedDate == null || Date2Picker.SelectedDate == null)
                {
                    return;
                }

                DateTime d1 = Date1Picker.SelectedDate.Value.Date;
                DateTime d2 = Date2Picker.SelectedDate.Value.Date;

                DateTime rangeStart = d1 < d2 ? d1 : d2;
                DateTime rangeEnd = d1 < d2 ? d2 : d1;

                string from = Stamp(rangeStart);
                string to = Stamp(rangeEnd.AddDays(1));

                using (var connection = AppDb.Open())
                using (var cmd = new SQLiteCommand(
                    @"SELECT ClosedAt, OrdersCount, CashSales, CardSales, Vnesennya, Vidacha, CashInBox
                      FROM ZReports
                      WHERE ClosedAt >= @from AND ClosedAt < @to
                      ORDER BY ClosedAt DESC", connection))
                {
                    cmd.Parameters.AddWithValue("@from", from);
                    cmd.Parameters.AddWithValue("@to", to);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            zReportRows.Add(new ZReportRow
                            {
                                ClosedAtText = ParseClosedAt(reader["ClosedAt"]?.ToString() ?? ""),
                                Checks = (int)ReadDecimal(reader, "OrdersCount"),
                                CashSales = ReadDecimal(reader, "CashSales"),
                                CardSales = ReadDecimal(reader, "CardSales"),
                                Vnesennya = ReadDecimal(reader, "Vnesennya"),
                                Vidacha = ReadDecimal(reader, "Vidacha"),
                                CashInBox = ReadDecimal(reader, "CashInBox")
                            });
                        }
                    }
                }
            }, "завантаження Z-звітів");
        }

        private void LoadCashiers()
        {
            Safe(() =>
            {
                cashiers.Clear();
                using (var connection = AppDb.Open())
                using (var cmd = new SQLiteCommand("SELECT id, login FROM kasa ORDER BY login", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cashiers.Add(new KasaAccount
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Login = reader["login"]?.ToString() ?? ""
                        });
                    }
                }
            }, "завантаження касирів");
        }

        private void CashiersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CashiersList.SelectedItem is KasaAccount account)
            {
                CashierInfoText.Text = $"Обрано касира: {account.Login}";
                NewLoginBox.Text = account.Login;
                NewPassBox.Password = "";
            }
        }

        private void SaveCashier_Click(object sender, RoutedEventArgs e)
        {
            Safe(() =>
            {
                if (!(CashiersList.SelectedItem is KasaAccount account))
                {
                    Info("Оберіть касира зі списку!");
                    return;
                }

                string newLogin = NewLoginBox.Text.Trim();
                string newPass = NewPassBox.Password.Trim();

                if (string.IsNullOrEmpty(newLogin))
                {
                    Info("Логін не може бути порожнім!");
                    return;
                }

                using (var connection = AppDb.Open())
                {
                    using (var check = new SQLiteCommand("SELECT COUNT(*) FROM kasa WHERE login = @login AND id != @id", connection))
                    {
                        check.Parameters.AddWithValue("@login", newLogin);
                        check.Parameters.AddWithValue("@id", account.Id);
                        if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                        {
                            Info("Такий логін вже існує!");
                            return;
                        }
                    }

                    string query = "UPDATE kasa SET login = @login"
                                   + (!string.IsNullOrEmpty(newPass) ? ", \"pass\" = @pass" : "")
                                   + " WHERE id = @id";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@login", newLogin);
                        cmd.Parameters.AddWithValue("@id", account.Id);
                        if (!string.IsNullOrEmpty(newPass))
                        {
                            cmd.Parameters.AddWithValue("@pass", newPass);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }

                Info("Дані касира оновлено!");
                LoadCashiers();
            }, "збереження касира");
        }

        private void DeleteCashier_Click(object sender, RoutedEventArgs e)
        {
            Safe(() =>
            {
                if (!(CashiersList.SelectedItem is KasaAccount account))
                {
                    Info("Оберіть касира зі списку!");
                    return;
                }

                if (MessageBox.Show($"Ви дійсно хочете видалити касира {account.Login}?", "Підтвердження",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    using (var connection = AppDb.Open())
                    using (var cmd = new SQLiteCommand("DELETE FROM kasa WHERE id = @id", connection))
                    {
                        cmd.Parameters.AddWithValue("@id", account.Id);
                        cmd.ExecuteNonQuery();
                    }

                    Info("Касира видалено!");
                    LoadCashiers();
                    NewLoginBox.Clear();
                    NewPassBox.Clear();
                    CashierInfoText.Text = "Оберіть логін касира";
                }
            }, "видалення касира");
        }

        private void LoadProducts(string filter)
        {
            Safe(() =>
            {
                products.Clear();
                using (var connection = AppDb.Open())
                {
                    var c = GetProductColumns(connection);

                    string query =
                        $"SELECT \"{c.Code}\" AS Code, \"{c.Name}\" AS Name, \"{c.Category}\" AS Category, " +
                        $"\"{c.Price}\" AS Price, \"{c.Quantity}\" AS Quantity FROM Products";

                    if (!string.IsNullOrEmpty(filter))
                    {
                        query += $" WHERE \"{c.Name}\" LIKE @f OR \"{c.Code}\" LIKE @f OR \"{c.Category}\" LIKE @f";
                    }
                    query += $" ORDER BY \"{c.Name}\"";

                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(filter))
                        {
                            cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                products.Add(new AdminProduct
                                {
                                    Code = reader["Code"]?.ToString() ?? "",
                                    Name = reader["Name"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    Price = ReadDecimal(reader, "Price"),
                                    Quantity = ReadDecimal(reader, "Quantity")
                                });
                            }
                        }
                    }
                }
            }, "завантаження товарів");
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            isEditMode = false;
            FormTitleText.Text = "Новий товар";
            CodeBox.IsEnabled = true;
            CodeBox.Clear();
            NameBox.Clear();
            CategoryBox.Text = "";
            PriceBox.Clear();
            QuantityBox.Clear();
            ProductFormPopup.Visibility = Visibility.Visible;
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AdminProduct p)
            {
                isEditMode = true;
                FormTitleText.Text = "Редагування товару";
                CodeBox.Text = p.Code;
                CodeBox.IsEnabled = false;
                NameBox.Text = p.Name;
                CategoryBox.Text = p.Category;
                PriceBox.Text = p.Price.ToString("0.00", CultureInfo.InvariantCulture);
                QuantityBox.Text = p.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                ProductFormPopup.Visibility = Visibility.Visible;
            }
        }

        private void SaveProduct_Click(object sender, RoutedEventArgs e)
        {
            Safe(() =>
            {
                string code = CodeBox.Text.Trim();
                string name = NameBox.Text.Trim();
                string category = CategoryBox.Text.Trim();

                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                {
                    Info("Код та назва товару обов'язкові!");
                    return;
                }

                if (!TryParseMoney(PriceBox.Text, out decimal price) || price < 0)
                {
                    Info("Введіть коректну ціну!");
                    return;
                }

                if (!TryParseMoney(QuantityBox.Text, out decimal qty) || qty < 0)
                {
                    Info("Введіть коректну кількість!");
                    return;
                }

                using (var connection = AppDb.Open())
                {
                    var c = GetProductColumns(connection);

                    if (isEditMode)
                    {
                        string sql = $"UPDATE Products SET \"{c.Name}\"=@name, \"{c.Category}\"=@category, " +
                                     $"\"{c.Price}\"=@price, \"{c.Quantity}\"=@qty WHERE \"{c.Code}\"=@code";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@name", name);
                            cmd.Parameters.AddWithValue("@category", category);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@qty", qty);
                            cmd.Parameters.AddWithValue("@code", code);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var check = new SQLiteCommand($"SELECT COUNT(*) FROM Products WHERE \"{c.Code}\" = @code", connection))
                        {
                            check.Parameters.AddWithValue("@code", code);
                            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                            {
                                Info("Товар з таким кодом вже існує!");
                                return;
                            }
                        }

                        string sql;
                        if (c.Id != null)
                        {
                            sql = $"INSERT INTO Products (\"{c.Id}\", \"{c.Code}\", \"{c.Name}\", \"{c.Category}\", \"{c.Price}\", \"{c.Quantity}\") " +
                                  $"VALUES ((SELECT COALESCE(MAX(\"{c.Id}\"), 0) + 1 FROM Products), @code, @name, @category, @price, @qty)";
                        }
                        else
                        {
                            sql = $"INSERT INTO Products (\"{c.Code}\", \"{c.Name}\", \"{c.Category}\", \"{c.Price}\", \"{c.Quantity}\") " +
                                  $"VALUES (@code, @name, @category, @price, @qty)";
                        }

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", code);
                            cmd.Parameters.AddWithValue("@name", name);
                            cmd.Parameters.AddWithValue("@category", category);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@qty", qty);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrEmpty(category))
                    {
                        using (var cmd = new SQLiteCommand("INSERT OR IGNORE INTO Categories (Name) VALUES (@name)", connection))
                        {
                            cmd.Parameters.AddWithValue("@name", category);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                ProductFormPopup.Visibility = Visibility.Collapsed;
                LoadProducts(string.Empty);
                LoadCategories();
                Info("Збережено успішно!");
            }, "збереження товару");
        }

        private void CancelProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductFormPopup.Visibility = Visibility.Collapsed;
        }

        private void LoadCategories()
        {
            Safe(() =>
            {
                categories.Clear();
                using (var connection = AppDb.Open())
                {
                    var c = GetProductColumns(connection);

                    string sql = $@"SELECT Name FROM Categories WHERE Name IS NOT NULL AND Name != ''
                                    UNION
                                    SELECT ""{c.Category}"" FROM Products WHERE ""{c.Category}"" IS NOT NULL AND ""{c.Category}"" != ''
                                    ORDER BY 1";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string cat = reader[0]?.ToString();
                            if (!string.IsNullOrEmpty(cat) && !categories.Contains(cat))
                            {
                                categories.Add(cat);
                            }
                        }
                    }
                }
            }, "завантаження категорій");
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            NewCategoryInputBox.Clear();
            CategoryFormPopup.Visibility = Visibility.Visible;
        }

        private void CancelNewCategory_Click(object sender, RoutedEventArgs e)
        {
            CategoryFormPopup.Visibility = Visibility.Collapsed;
        }

        private void SaveNewCategory_Click(object sender, RoutedEventArgs e)
        {
            Safe(() =>
            {
                string catName = NewCategoryInputBox.Text.Trim();
                if (string.IsNullOrEmpty(catName))
                {
                    Info("Введіть назву категорії!");
                    return;
                }

                if (categories.Contains(catName))
                {
                    Info("Така категорія вже існує!");
                    return;
                }

                using (var connection = AppDb.Open())
                using (var cmd = new SQLiteCommand("INSERT OR IGNORE INTO Categories (Name) VALUES (@name)", connection))
                {
                    cmd.Parameters.AddWithValue("@name", catName);
                    cmd.ExecuteNonQuery();
                }

                CategoryFormPopup.Visibility = Visibility.Collapsed;
                LoadCategories();
                Info("Категорію успішно створено!");
            }, "створення категорії");
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                SetActiveMenu(btn);
                ReportsPanel.Visibility = tag == "Reports" ? Visibility.Visible : Visibility.Collapsed;
                CashiersPanel.Visibility = tag == "Cashiers" ? Visibility.Visible : Visibility.Collapsed;
                ProductsPanel.Visibility = tag == "Products" ? Visibility.Visible : Visibility.Collapsed;

                if (tag == "Reports") LoadReports();
                else if (tag == "Cashiers") LoadCashiers();
                else if (tag == "Products")
                {
                    LoadProducts(string.Empty);
                    LoadCategories();
                }
            }
        }

        private void SetActiveMenu(Button activeBtn)
        {
            ReportsMenuBtn.ParentBorderBackground("#FFA880C6");
            CashiersMenuBtn.ParentBorderBackground("#FFA880C6");
            ProductsMenuBtn.ParentBorderBackground("#FFA880C6");

            if (activeBtn.Parent is Border border)
            {
                border.Background = new BrushConverter().ConvertFrom("#FF7848AC") as SolidColorBrush;
            }
        }

        private void exitAdmin(object sender, RoutedEventArgs e)
        {
            AutoWindows autoWind = new AutoWindows();
            autoWind.Show();
            this.Hide();
        }
    }

    public static class ButtonExtensions
    {
        public static void ParentBorderBackground(this Button btn, string hex)
        {
            if (btn.Parent is Border border)
            {
                border.Background = new BrushConverter().ConvertFrom(hex) as SolidColorBrush;
            }
        }
    }
}
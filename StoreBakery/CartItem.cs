using System.ComponentModel;

public class CartItem : INotifyPropertyChanged
{
    private string _name;
    private string _code;
    private decimal _quantity;
    private decimal _price;



    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string Code
    {
        get => _code;
        set { _code = value; OnPropertyChanged(nameof(Code)); }
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            _quantity = value;
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(TotalAmount));
        }
    }

    public decimal Price
    {
        get => _price;
        set
        {
            _price = value;
            OnPropertyChanged(nameof(Price));
            OnPropertyChanged(nameof(TotalAmount));
        }
    }

    public decimal TotalAmount => Quantity * Price;

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ProductSuggestion
{
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string DisplayText => $"{Name} (Код: {Code}) {Price:F2} грн";
}
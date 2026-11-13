using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreBakery
{
    public class OrderModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public string ProductsText { get; set; }
        public string OrderTime { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

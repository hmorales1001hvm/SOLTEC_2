using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Negocio.Comun
{
    public class ApiResponse
    {
        public ApiResponse() => Success = true;

        public bool Success { get; set; } = true;

        public string Message { get; set; }

        public string Url { get; set; }

        public int? Count { get; set; }

        public ApiResponse(string itemString) : this() => ItemString = itemString;

        public ApiResponse(int itemInt) : this() => ItemId = itemInt;

        public string ItemString { get; set; }

        public int? ItemId { get; set; }

        public string DateRequest { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public ApiResponse(Exception ex)
        {
            Success = false;
            Message = ex.Message;
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        public ApiResponse() { }

        public ApiResponse(T result) => Result = result;

        public ApiResponse(List<T> result)
        {
            List = result;
            Count = List.Count;
        }

        public List<T> List { get; set; }

        public T Result { get; set; }

    }
}

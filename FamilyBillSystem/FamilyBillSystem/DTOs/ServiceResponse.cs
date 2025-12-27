namespace FamilyBillSystem.DTOs
{
    public class ServiceResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ServiceResponse<T> CreateSuccess(T data, string message = "")
        {
            return new ServiceResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ServiceResponse<T> Error(string message)
        {
            return new ServiceResponse<T>
            {
                Success = false,
                Message = message,
                Data = default
            };
        }
    }

    public class ServiceResponse : ServiceResponse<object>
    {
        public static new ServiceResponse CreateSuccess(string message = "")
        {
            return new ServiceResponse
            {
                Success = true,
                Message = message
            };
        }

        public static new ServiceResponse Error(string message)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = message
            };
        }

        public static ServiceResponse Error(string message, object data)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = message,
                Data = data
            };
        }
    }
}

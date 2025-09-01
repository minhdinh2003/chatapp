// Common/Response.cs
namespace API.Common
{
    public class Response<T>
    {
        public bool isSuccess { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public object Error { get; set; }

        public static Response<T> Success(T data, string message)
        {
            return new Response<T>
            {
                isSuccess = true,
                Data = data,
                Message = message,
                Error = null
            };
        }

        public static Response<T> Failure(string message)
        {
            return new Response<T>
            {
                isSuccess = false,
                Message = message,
                Error = null
            };
        }
    }
}
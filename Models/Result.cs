namespace LeccionesAprendidas.Models
{
    public class Result
    {
        public bool Succeeded { get; init; }
        public bool IsError => !Succeeded;
        public string? Error { get; init; }

        public static Result Success() => new() { Succeeded = true };

        public static Result Failure(string error) =>
            new() { Succeeded = false, Error = error };
    }

    public class Result<T>
    {
        private readonly T? _value;

        public bool Succeeded { get; }
        public bool IsError => !Succeeded;
        public string? Error { get; }

        public T? Value => _value;

        private Result(T value)
        {
            _value = value;
            Succeeded = true;
        }

        private Result(string error)
        {
            Error = error;
            Succeeded = false;
        }

        public static Result<T> Success(T value) => new(value);
        public static Result<T> Failure(string error) => new(error);
    }

}

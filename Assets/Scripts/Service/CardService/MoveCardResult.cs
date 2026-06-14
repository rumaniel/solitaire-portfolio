namespace Service.CardService
{
    public readonly struct MoveCardResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }

        public MoveCardResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static MoveCardResult Success(string message = "") => new MoveCardResult(true, message);

        public static MoveCardResult Fail(string message) => new MoveCardResult(false, message);
    }
}

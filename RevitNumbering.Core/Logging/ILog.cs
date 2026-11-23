namespace RevitNumbering.Core.Logging
{
    public interface ILog
    {
        void Info(string message);

        void Error(string message, Exception exception);
    }
}

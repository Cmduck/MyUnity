namespace KissFramework
{
    public class Singleton<T> where T : class, new()
    {
        public static T Instance { get; private set; } = new T();

        public static void SetInstance(T value)
        {
            Instance = value;
        }
    }
}
namespace DarkGreyRPG.Studio.ViewModels;

internal static class EditHistoryClock
{
    private static long _sequence;
    public static long Current => System.Threading.Interlocked.Read(ref _sequence);
    public static long Next() => System.Threading.Interlocked.Increment(ref _sequence);
}

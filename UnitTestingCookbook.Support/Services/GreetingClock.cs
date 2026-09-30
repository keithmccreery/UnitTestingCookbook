namespace UnitTestingCookbook.Support.Services;

public class GreetingClock
{
    private readonly TimeProvider timeProvider;

    public GreetingClock(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public string GetGreeting()
    {
        int hour = timeProvider.GetLocalNow().Hour;

        return hour switch
        {
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };
    }
}

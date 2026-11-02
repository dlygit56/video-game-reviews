using System;

namespace GameReviews.Data;

public static class Session
{
    private static AppUser? _current;

    public static AppUser? Current
    {
        get => _current;
        set
        {
            _current = value;
            Changed?.Invoke();
        }
    }

    public static event Action? Changed;

    public static long UserId => _current?.Id ?? 0;
}

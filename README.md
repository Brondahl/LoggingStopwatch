# LoggingStopwatch
Combining the concepts of the C# Stopwatch with logging functionality to time log the results of the timing


# Rights / License

Created as an OpenSource offering (MIT License), but written on general Company time whilst employed at Softwire. All rights are retained by Softwire.
Specifically, written on time allocated to the TechOfficer budget, and thus not on any individual Softwire project.

Written with the benefit of experience gained from previous projects, but not by lifting any individual code from those projects.

Usage looks like this:

``` csharp
using(var stopwatch = new LongOperationLoggingStopwatch("SomeMethod", logger, settings))
{
    foreach(var thing in myThings)
    {
        BoringSetup(thing);
        using(stopwatch.TimeInnerOperation())
        {
            InterestingMethod(thing);
        } //Progress Log may be written here depending on configuration.
        MoreBoringWork()
    }
}//Summary Log will be written here.
```

(There’s also a simpler LoggingStopwatch if you just want a single block of code timed):

``` csharp
using(var stopwatch = new LoggingStopwatch("SomeMethod", logger))
{
    InterestingMethod(thing);
}//Timing record Log will be written here.
```

You might want to add an extension method to your project's Logging Type of choice, for greater convenience.

Various options can be passed in for what gets logged, including providing an expected target for how many inner operations will be completed and then logging Completion Percentage and projected Completion Time.

It’s also fully thread-safe and compatible with being called across multiple threads (and then (if wanted) reporting on how many distinct threads were used and how time was split across them)

Note that because it’s using a `using` block, you don’t have an extra layer of lambdas floating around, nor distinct methods for Action<T> and Func<T> (Or Task and Task<T>), and you can define the overal scope in one method and then pass the timer down through other methods into the guts of the thing you want to know the total time for within the loop.

The code defines it’s own logging interface which you can implement, or you can just pass in a trivial Action<string>, or if you're already using the `Microsoft.Extensions.Logging.ILogger` interface, you can pass that in directly. (Records are logged as `Information`)

It’s also fast! If you set reportingPeriod to be suitably rare, then the per-loop overhead is equivalent to calling DateTime.UtcNow twice. (~0.15 microseconds).
On executions that trigger progress logging then the overhead to build the progress update, but not log it (i.e. if you provided a `NullLogger`) is around 2 microseconds.

Unfortunately I ran out of time to actually set this up on nuget as a package you can depend on, so for the moment you’ll just need to grab the src files and paste them into your project. (although that does mean you could integrate your project’s logging framework directly!).

Feel free to nugetify if for us if you want (or I might do so in the future. :man_shrugging:)

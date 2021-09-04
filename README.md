# ByondSharp

ByondSharp is a C# library providing easy interop with BYOND (DreamMaker).

It abstracts away the complexity of handling FFI with BYOND, and makes it easy for developers with any level of experience to create meaningful interop with BYOND from C#.

### Use

ByondSharp works by allowing a developer to write 'normal' C# code, without references to pointers or much consideration for the FFI aspect of their code, and tag their methods they need to export to BYOND with an attribute, ``ByondFFI``. Once tagged the method will be wrapped using source generation and exposed to CDecl calls, making it easily called from BYOND.

Important things you will need to run ByondSharp in BYOND:
- [.NET 5.0 or greater runtimes](https://dotnet.microsoft.com/download/dotnet/5.0)

That's it. Really.

Once you have .NET 5.0 or greater runtimes, writing the code is pretty straight forward. I would recommend looking at the [samples](https://github.com/bobbahbrown/ByondSharp/blob/master/ByondSharp.Samples), especially the [timer sample](https://github.com/bobbahbrown/ByondSharp/blob/master/ByondSharp.Samples/Deferred/Timers.cs), for an essential introduction to the format of these functions.

At a bare minimum, exported functions must:
- Have the ``ByondFFI`` attribute
- Be ``public`` and ``static`` methods
- Return ``void``, ``string``, or when async return ``Task`` or ``Task<string>``
- Have zero arguments, or have one argument which is a ``List<string>``

Aside from that you're free to do as you please.

I would highly recommend using the content of the ByondSharp project as the basis for your own project. By not doing this you may miss out on the functionality of the ``TaskManager``. The code included in the sample project (``ByondSharp.Samples``), are just samples and can be ignored.

See below for the timer sample as a point of reference.

```csharp
namespace ByondSharp
{
    /// <summary>
    /// Stateful example of operations possible with ByondSharp. With these two calls, one can maintain 
    /// a stopwatch and keep track of the passing of time.
    /// </summary>
    public class TimerSample
    {
        private static Stopwatch _sw;

        [ByondFFI]
        public static void StartStopwatch()
        {
            if (_sw is not null)
            {
                return;
            }

            _sw = new Stopwatch();
            _sw.Start();
        }

        [ByondFFI]
        public static string GetStopwatchStatus()
        {
            return _sw.Elapsed.ToString();
        }
    }
}
```

### Deferrable Behaviour

Using the ByondSharp solution, it is possible to add the ``Deferrable`` boolean named argument to any ``ByondFFIAttribute`` on an async method. This will generate an additional version of the method's export, with ``Deferred`` added as a suffix to the method's name. If the method has a return value, the deferred method will return a ``ulong`` id, which is an internal scheduled id for this job. You can then poll the ``TaskManager`` using the ``PollJobs`` exported function to get a semicolon separated list of completed jobs. Once the job appears in this list, you can retrieve the result and remove it from the task manager by calling the ``GetResult`` exported function, with the job id as the only argument. By doing this you accomplish two things: you can run tasks on multiple threads, as is the default behaviour of async tasks, and as well as this you can avoid blocking BYOND's thread waiting for a result from this external call.

### How to build

Knowing how to run it and the brief rules for writing code is not very useful without being able to build the library, so how is it done?

Quite simple: in Visual Studio, you will build the ByondSharp project in debug or release configuration **for x86 CPUs** (BYOND is a 32-bit application, a 64-bit compiled DLL will not do much!)

Once this is done, you will generate several files within your ``ByondSharp\bin\x86\[Debug/Release]\net5.0\win-x86\copy_to_byond`` directory. Copy these to the location in which they will be referenced from BYOND. So long as no unanticipated files are generated during compilation (namely non-DLLs) then these should be the only files you require.

All other files generated outside this folder can be discarded, but the aforementioned files __must__ be present in a directory that BYOND can access.

### Using in BYOND

To use external DLLs in BYOND, simply use the ``call()()`` proc. For example:

```dm
#define byondsharp_repeatme(options)			call("byondsharpNE", "RepeatMe")(options)
#define byondsharp_startstopwatch(options)		call("byondsharpNE", "StartStopwatch")(options)
#define byondsharp_getstopwatchstatus(options)	call("byondsharpNE", "GetStopwatchStatus")(options)

/world/New()
	byondsharp_startstopwatch(null)
	while (1)
		var/timelog = byondsharp_getstopwatchstatus(null)
		world.log << timelog
		world.log << byondsharp_repeatme(timelog)
		sleep(1)
```

### What about Linux?

Unfortunately .NET currently does not target x86 linux ([see this issue for progress on that](https://github.com/dotnet/runtime/issues/7335)), so this package can't be used for linux deployments until a time that it does, or that Lummox releases a 64-bit BYOND. Thanks to Nopm for helping me figure that out.

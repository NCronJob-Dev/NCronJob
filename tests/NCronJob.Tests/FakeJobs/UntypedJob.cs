using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCronJob.Tests;

public class UntypedJob
{
    public static Delegate Dummy => Dummy_Impl;

    public static Delegate NoOp = () => { };

    private static void Dummy_Impl(
        IJobExecutionContext context,
        Storage storage,
        CancellationToken token)
    { 
        DummyJob.Implementation(
            $"{nameof(DummyJob)} (untyped)",
            storage,
            context,
            token);
    }
}

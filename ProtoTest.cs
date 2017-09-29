// this is a testbench for protocol stack.
// it creates 2 ProtoStacks, both containing a ping client and
// ping server and connects them (cross) in the while loop so that messages
// got from one ProtoStack are passed to the other.
// testbench flow is controlled by 2 time parameters:
// 1. loopPeriod - emulates antenna failure:
//          messages are transmitted once in loopPeriod
// 2. testBudget - a watchdog period restricting test time

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

public class MainClass {
    public static void Main (string[] argument) {

        // ensure exception messages are written in common language :)
        Thread.CurrentThread.CurrentUICulture
            = new System.Globalization.CultureInfo("en-US");
        ProtoTest t = new ProtoTest ();
        t.run();
        Console.WriteLine ("all tests done");
    }
}

/*!
 *  \author  4o
 *  \brief a testbench for a proto stack (for Space Engineers' PB)
 *  \details this is a testbench for protocol stack.
 *           it creates 2 ProtoStacks, both containing a ping client and
 *           ping server and connects them (cross) in the while loop so that messages
 *           got from one ProtoStack are passed to the other.
 *           testbench flow is controlled by 2 time parameters:
 *           1. loopPeriod - emulates antenna failure:
 *                    messages are transmitted once in loopPeriod
 *           2. testBudget - a watchdog period restricting test time
 *  \coryright BSD 3-clause
 */
public class ProtoTest {
    // test flow parameters:
    // emulates antenna failure. set to lesser values to speed up the test
    readonly TimeSpan loopPeriod = new TimeSpan (0,0,0,0,100);
    // constraints test run time
    readonly TimeSpan testBudget = new TimeSpan (0,0,10);

    Stopwatch sw = new Stopwatch ();

    /*!
     *  \brief runs all tests
     */
    public void run () {
        Console.WriteLine (
            "test may take some time. don't worry, there is a watchdog "
            + "(on the downside, it's syncronous)");
        test1(false);
        test2(false);
    }
    /*!
     *  \brief functional test: pipes
     *  \param verbose print transmitted messages
     */
    public void test1 (bool verbose) {

        ProtoStack proto1;
        ProtoStack proto2;
        Func <bool> fn;

        // creates 2 ProtoStacks with "crossbar" ping protos. uses "pipe"-type
        // protos for ping servers
        createTestEnv (
            false,
            out proto1,
            out proto2,
            out fn
        );

        Console.WriteLine ("running test1 loop:");
        // common loop for 2 tests
        protoTestLoop (
            proto1,
            proto2,
            fn
        );
        // cleanup: manual removal of all protos
        bool killProtoStack;

        proto1.UnregisterProto ("proto2", "pingCh1", out killProtoStack);
        if (killProtoStack) {
            throw new Exception ("clean: proto1 failed");
        }
        proto1.UnregisterProto ("proto2", "pingCh2", out killProtoStack);
        if (!killProtoStack) {
            throw new Exception ("clean: proto1 failed");
        }
        if (!proto1.IsEmpty) {
            throw new Exception ("clean check: proto1 failed");
        }

        proto2.UnregisterProto ("proto1", "pingCh1", out killProtoStack);
        if (killProtoStack) {
            throw new Exception ("clean: proto2 failed");
        }
        proto2.UnregisterProto ("proto1", "pingCh2", out killProtoStack);
        if (!killProtoStack) {
            throw new Exception ("clean: proto2 failed");
        }
        if (!proto2.IsEmpty) {
            throw new Exception ("clean check: proto2 failed");
        }

    }

    /*!
     *  \brief functional test: listeners
     *  \param verbose print transmitted messages
     */
    public void test2 (bool verbose) {

        ProtoStack proto1;
        ProtoStack proto2;
        Func <bool> fn;

        // creates 2 ProtoStacks with "crossbar" ping protos. uses
        // "service"-type protos for ping servers
        createTestEnv (
            true,
            out proto1,
            out proto2,
            out fn
        );

        Console.WriteLine ("running test2 loop:");
        // common loop for 2 tests
        protoTestLoop (
            proto1,
            proto2,
            fn
        );

        // cleanup: manual removal of PingClient protos and automatic removal
        // of "service" PingServers
        bool killProtoStack;
        proto1.UnregisterProto ("proto2", "pingCh1", out killProtoStack);
        if (killProtoStack) {
            throw new Exception ("clean proto1: this should never happen");
        }
        proto2.UnregisterProto ("proto1", "pingCh2", out killProtoStack);
        if (killProtoStack) {
            throw new Exception ("clean proto1: this should never happen");
        }

        if (proto1.IsEmpty) {
            throw new Exception ("clean check: proto1 failed (1)");
        }

        if (proto2.IsEmpty) {
            throw new Exception ("clean check: proto2 failed (1)");
        }

        // waiting for 900 ms (assuming that spoilage time is 450ms) to spoil protos
        Thread.Sleep (900);
        // a step should trigger removal of all spoiled PingServers
        proto1.Step (false);
        proto2.Step (false);

        if (!proto1.IsEmpty) {
            throw new Exception ("clean check: proto1 failed");
        }

        if (!proto2.IsEmpty) {
            throw new Exception ("clean check: proto2 failed");
        }
    }

    /*!
     *  \brief creates 2 ProtoStacks with "crossbar"-connected
     *         PingClients and PingServers
     *  \param useListeners creates servers as "services" if true
     *  \param proto1 created ProtoStack intsnce 0
     *  \param proto2 created ProtoStack intsnce 1
     *  \param fn a Func representing the stopping criterion for test loops
     */
    void createTestEnv (
        bool useListeners,
        out ProtoStack proto1,
        out ProtoStack proto2,
        out Func <bool> fn
    ) {
        // create ping and pong ProtoStack
        proto1 = new ProtoStack ("proto1", new TimeSpan (0,0,0,0,450));
        proto2 = new ProtoStack ("proto2", new TimeSpan (0,0,0,0,450));

        // create, remember reference for PingClient and
        // associate it with one ProtoStack (last arg in RegisterProto forces
        // transport proto to handle emulated antenna failure)
        PingClient pingClient1 = new PingClient (10);
        proto1.RegisterProto ("proto2", "pingCh1", pingClient1, true);
        PingClient pingClient2 = new PingClient (10);
        proto2.RegisterProto ("proto1", "pingCh2", pingClient2, true);

        if (useListeners) {
            // create "service"-type PingServers
            PingServerFactory pingServerFactory = new PingServerFactory (true);
            proto2.AddListener ("pingCh1", pingServerFactory);
            proto1.AddListener ("pingCh2", pingServerFactory);
        } else {
            // create "pipe"-type PingServers
            proto1.RegisterProto (
                "proto2", "pingCh2", new PingServer (), true);
            proto2.RegisterProto (
                "proto1", "pingCh1", new PingServer (), true);
        }

        // run loop check function to see if PingClients are satisfied
        fn = ( () => pingClient1.Done == false && pingClient2.Done == false );
    }

    /*!
     *  \brief steps a ProtoStack and performs time measurement
     *  \param proto an instance of ProtoStack to step
     *  \param canSend flag alowing a ProtoStack to send messages
     *  \param msg a message for transmission generated by the ProtoStack
     */
    double protoStep (ProtoStack proto, bool canSend, out string msg) {
        sw.Reset();
        GC.Collect();
        sw.Start();
        msg = proto.Step (canSend);
        sw.Stop();
        return sw.ElapsedTicks;
    }

    /*!
     *  \brief steps 2 ProtoStacks and performs time measurement
     *  \param proto1 one instance of ProtoStack to step
     *  \param proto2 the other instance of ProtoStack to step
     *  \param canSend flag alowing a ProtoStack to send messages
     *  \param msg1 a message for transmission generated by the proto1
     *  \param msg2 a message for transmission generated by the proto2
     *  \param l a list to store runtime durations
     */
    void protoStepAll (
        ProtoStack proto1,
        ProtoStack proto2,
        bool canSend,
        out string msg1,
        out string msg2,
        List<double> l
    ) {
        l.Add (protoStep (proto1, canSend, out msg1));
        l.Add (protoStep (proto2, canSend, out msg2));
    }

    /*!
     *  \brief feeds a message to ProtoStack with time measurement
     *  \param proto an instance of ProtoStack to handle the msg
     *  \param msg a message for transmission generated by another ProtoStack
     */
    double protoHandleMsg (ProtoStack proto, string msg) {
        sw.Reset();
        GC.Collect();
        sw.Start();
        proto.HandleMsg (msg);
        sw.Stop();
        return sw.ElapsedTicks;
    }

    /*!
     *  \brief fires `HandleMsg()` on 2 instances of ProtoStack with messages
     *         generated by these ProtoStacks
     *  \param proto1 one instance of ProtoStack
     *  \param proto2 the other instance of ProtoStack
     *  \param msg1 a message for transmission generated by the proto1
     *  \param msg2 a message for transmission generated by the proto2
     *  \param l a list to store runtime durations
     */
    void protoHandleMsgAll (
        ProtoStack proto1,
        ProtoStack proto2,
        string msg1,
        string msg2,
        List<double> l
    ) {
        if (msg2 != null) {
            l.Add (protoHandleMsg (proto1, msg2));
        }
        if (msg1 != null) {
            l.Add (protoHandleMsg (proto2, msg1));
        }
    }

    /*!
     *  \brief a loop step: get all messages and feed them to other ProtoStack
     *  \param proto1 one instance of ProtoStack
     *  \param proto2 the other instance of ProtoStack
     *  \param canSend flag alowing a ProtoStack to send messages
     *  \param lstep a list to store runtime durations for `step()` methods
     *  \param lparser a list to store runtime durations for `HandleMsg()` methods
     */
    void stepAll (
        ProtoStack proto1,
        ProtoStack proto2,
        bool canSend,
        List<double> lstep,
        List<double> lparser
    ) {
        string msg1;
        string msg2;
        protoStepAll (
            proto1,
            proto2,
            canSend,
            out msg1,
            out msg2,
            lstep
        );
        protoHandleMsgAll (
            proto1,
            proto2,
            msg1,
            msg2,
            lparser
        );
    }

    /*!
     *  \brief the test loop. runs the loop function until either check function `f`
     *         returns true, or time budget exceeded.
     *         reports timings when the loop is done
     *  \param proto1 one instance of ProtoStack
     *  \param proto2 the other instance of ProtoStack
     *  \param f a Func representing the stopping criterion for test loops
     */
    void protoTestLoop (
        ProtoStack proto1,
        ProtoStack proto2,
        Func <bool> f
    ) {

        // initialize timestamps, that are used to control test flow
        DateTime loopTime = DateTime.Now;
        DateTime startTime = DateTime.Now;

        // forbids transmission when false
        bool canSend = false;

        // a list of run durations for `step()` methods
        List<double> stepDureations = new List<double> ();
        // a list of run durations for `HandleMsg()` methods
        List<double> parsersDureations = new List<double> ();

        // loop iterations counter
        int cntSteps = 0;
        // counts the number of iterations involving msg transmission
        int cntMsgs = 0;

        bool timeBudgetExceeded = false;

        while (f ()) {
            canSend = (DateTime.Now - loopTime) > loopPeriod;

            stepAll (proto1, proto2, canSend, stepDureations, parsersDureations);

            if (canSend) {
                cntMsgs ++;
                loopTime = DateTime.Now;
            }
            cntSteps ++;

            // watchdog
            if ((DateTime.Now - startTime) > testBudget) {
                timeBudgetExceeded = true;
                break;
            }
        }

        if (timeBudgetExceeded){ // watchdog check
            throw new Exception ("time budget exceded");
        }

        // seflcheck: numer of runs against number of measurements
        if (stepDureations.Count != cntSteps*2) {
            throw new Exception ("test selfcheck failed: stepDureations.Count");
        }

        // seflcheck: numer of runs with canSend==true against number of measurements
        if (parsersDureations.Count != cntMsgs*2) {
            throw new Exception ("test selfcheck failed: parsersDureations.Count");
        }

        Console.WriteLine ("________________________________________"
            + "________________________________________\n"
            + "done in "
            + (DateTime.Now - startTime).TotalSeconds + " sec\n"
            + "avarage step duration: " + (stepDureations.Average() / Stopwatch.Frequency)
            + "\nmax step duration: " + (stepDureations.Max() / Stopwatch.Frequency)
            + "\ntotal steps: " + stepDureations.Count
            + "\ntotal steps > 1 ms: " + stepDureations.Count (x => x > Stopwatch.Frequency/1000.0)
            + "\navarage parser duration: " + (parsersDureations.Average() / Stopwatch.Frequency)
            + "\nmax parser duration: " + (parsersDureations.Max() / Stopwatch.Frequency)
            + "\ntotal parser calls: " + parsersDureations.Count
            + "\ntotal parser calls > 1 ms: " + parsersDureations.Count (x => x > Stopwatch.Frequency/1000.0)
            + "\n________________________________________"
            + "________________________________________\n"
        );

    }
}

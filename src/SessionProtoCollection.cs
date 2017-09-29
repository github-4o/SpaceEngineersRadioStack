using System;
using System.Text;
using System.Collections.Generic;

/*!
 *  \author  4o
 *  \brief manages a set of IProto for a given "receiverID"
 *  \details this class manages a collection of IProtos associated with a
 *           particular "senderID".
 *           reminder: proto structure
 *           ProtoStack - filteres messages by sender and receiver ID
 *           SessionProtoCollection - filteres messages by channel ID (aka port)
 *           IProto - filteres messages by proto ID
 *           reminder: message structure
 *           [receiverID] [senderId] [channelID] [protoID] [message]
 *                                        ^
 *                                  you are here
 *           this class handles a collection of Protos associated with channel ID strings.
 *           it handles channelID parsing to enable truly abtract IProto operation.
 *           discards message if:
 *           1. `msg` has no delimeter ofter offset (no channelID field)
 *           2. no IProto is registered for the channelID
 *           possible ways to improve this class:
 *           1. call `step()` on a single proto in a tick (possibly with `yield return`)
 *              to spread a ProtoStack load across multiple ticks
 *           2. replace Dictionary with a hash-based something to enable native duplicate
 *              channelID detection
 *           3. add a method to remove IProto by reference to enable `IProto.Disable()`
 *              method for convenience purposes
 *  \coryright BSD 3-clause
 */
public class SessionProtoCollection {

    /*!
     *  \brief a list of IProtos associated with "channelIDs"
     */
    Dictionary <string, IProto> protos = new Dictionary <string, IProto> ();
    /*!
     *  \brief a list of spoilable IProtos associated with "channelIDs"
     */
    Dictionary <string, IProto> managedProtos = new Dictionary <string, IProto> ();

    /*!
     *  \brief checks if there are any IProtos registered
     */
    public bool ReadyToDie {get {
        return (protos.Count + managedProtos.Count) == 0;
    }}

    /*!
     *  \brief service variable. although it's used only in `internalStep()`, it was
     *         placed here to avoid excessive allocations
     */
    List<string> protoNamesReadyToDie = new List<string> ();

    /*!
     *  \brief registers channelID<->IProto pair
     *  \param channel channelID to associate the proto to
     *  \param proto IProto to register
     *  \param useTransportProto wrap the proto in a TransportProto instance
     *         to unsure msg delivery
     */
    public bool RegisterProto (
        string channel,
        IProto proto,
        bool useTransportProto
    ) {
        if (canAdd (channel, proto)) {
            if (useTransportProto) {
                protos.Add (channel, new TransportProto (proto));
            } else {
                protos.Add (channel, proto);
            }
            return true;
        }
        return false;
    }

    /*!
     *  \brief registers channelID<->IProto pair
     *  \param channel channelID to associate the proto to
     *  \param proto IProto to register
     *  \param useTransportProto wrap the proto in a TransportProto instance
     *         to unsure msg delivery
     *  \param watchdogPeriod if proto is idle for this duration, it will be
     *         automatically deleted
     */
    public bool RegisterProto (
        string channel,
        IProto proto,
        bool useTransportProto,
        TimeSpan watchdogPeriod
    ) {
        IProto p = new ManagedProto (proto, watchdogPeriod);
        if (canAdd (channel, proto)) {
            if (useTransportProto) {
                managedProtos.Add (channel, new TransportProto (p));
            } else {
                managedProtos.Add (channel, p);
            }
            return true;
        }
        return false;
    }

    /*!
     *  \brief removes a proto by channel name
     *  \param channel channelID to clear of a IProto
     */
    public bool UnregisterProto (string channel) {
        if (!protos.ContainsKey (channel)) {
            return false;
        }
        protos.Remove (channel);
        return true;
    }

    /*!
     *  \brief a step function that allows message transmission
     *  \details a step function that allows message transmission
     *           loops protos with `StepCanSend()` until someone returns
     *           non-null StringBuilder
     *           then loops remaining protos with `StepCantSend()`
     *           this is done to ensure only one message is transmitted in a run
     *           and more importantly a IProto *in `protos` list* gets exact time of
     *           message transmission
     *           if a transport wrapper is used then the wrapped proto may not
     *           get the time of it's message transmission
     */
    public StringBuilder StepCanSend () {
        internalStep ();
        StringBuilder ret = null;
        foreach (var p in protos) {
            if (ret == null) {
                ret = p.Value.StepCanSend ();
                if (ret != null) {
                    ret.Insert (0, p.Key + "%");
                }
            } else {
                p.Value.StepCantSend ();
            }
        }
        foreach (var p in managedProtos) {
            if (ret == null) {
                ret = p.Value.StepCanSend ();
                if (ret != null) {
                    ret.Insert (0, p.Key + "%");
                }
            } else {
                p.Value.StepCantSend ();
            }
        }
        return ret;
    }

    /*!
     *  \brief a step function that forbids message transmission
     */
    public void StepCantSend () {
        internalStep ();
        foreach (var p in protos) {
            p.Value.StepCantSend ();
        }
        foreach (var p in managedProtos) {
            p.Value.StepCantSend ();
        }
    }

    /*!
     *  \brief parses `msg` to get channelID, then passes the message forther with valid offset
     *  \param msg a message to parse
     *  \param offset indicates the start of message related to this class parser
     *         (after "receiverID" and "senderID" fields)
     *  \details discards message if:
     *           1. `msg` has no delimeter ofter offset (no channelID field)
     *           2. no IProto is registered for the channelID
     *           get the time of it's message transmission
     */
    public bool HandleMsg (string msg, int offset) {
        int endIndex = msg.IndexOf ('%', offset);
        if (endIndex > 0) {
            string chName = msg.Substring (offset, endIndex-offset);
            if (protos.ContainsKey (chName)) {
                protos [chName].HandleMsg (msg, endIndex+1);
                return true;
            }
            if (managedProtos.ContainsKey (chName)) {
                managedProtos [chName].HandleMsg (msg, endIndex+1);
                return true;
            }
        }
        return false;
    }

    /*!
     *  \brief checks the spoilable IProto list and removes spoiled items
     */
    void internalStep () {
        protoNamesReadyToDie.Clear();
        foreach (var p in managedProtos) {
            if (p.Value is IManagedProto) {
                if (((IManagedProto)p.Value).ReadyToDie) {
                    protoNamesReadyToDie.Add (p.Key);
                }
            } else if (p.Value is TransportProto) {
                if (((TransportProto)p.Value).ReadyToDie) {
                    protoNamesReadyToDie.Add (p.Key);
                }
            }
        }
        foreach (string n in protoNamesReadyToDie) {
            managedProtos.Remove (n);
        }
    }

    /*!
     *  \brief checks if an IProto could be associated with a "channelID"
     *  \param channel "channelID"
     *  \param proto an instance of IProto
     *  \details fails if:
     *           1. there is a proto already associated with channel
     *           2. the IProto instance is already associated with a channel
     *           also check if provided IProto is in fact IProto (ManagedProto or
     *           TransportProto). if that is the case, then someone got a reference
     *           to an IProto created inside the stack, which should never happen
     */
    bool canAdd (string channel, IProto proto) {
        if (protos.ContainsKey(channel)) {
            return false;
        }
        foreach (var p in protos) {
            if (p.Value == proto) {
                return false;
            }
        }
        foreach (var p in managedProtos) {
            if (p.Value is IServiceProto) {
                if (((IServiceProto)p.Value).Holds(proto)) {
                    return false;
                }
            }
            if (p.Value == proto) {
                throw new Exception (
                    "someone got a reference to internal proto");
            }
        }
        foreach (var p in protos) {
            if (p.Value == proto) {
                return false;
            }
        }
        return true;
    }
}

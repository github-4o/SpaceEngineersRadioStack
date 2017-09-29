using System;
using System.Text;
using System.Collections.Generic;

/*!
 *  \author  4o
 *  \brief manages a set of IProto for a given "receiverID"
 *  \details provides a ways to connect 2 IProtos on 2 ProtoStacks. there are 2
 *           modes of operation:
 *           1. "pipe" - associate IProto with static set of "senderID" and "channelID"
 *                  implemented in RegisterProto
 *           2. "service" - create an instance of IProto for "senderID" while handling an
 *                  input message on a given "channelID". implemented in AddListener
 *           `RegisterProto` with a watchdogPeriod arg is used to create a disposable
 *           proto, that will be deleted after defined idle period. this is the only way
 *           "service" IProtos are registered
 *           reminder: proto structure
 *           ProtoStack - filteres messages by sender and receiver ID
 *           SessionProtoCollection - filteres messages by channel ID (aka port)
 *           IProto - filteres messages by proto ID
 *           reminder: message structure
 *           [receiverID] [senderId] [channelID] [protoID] [message]
 *                ^            ^
 *                 you are here
 *           this class manages multiple SessionProtoCollection for a single receiverID.
 *           `myIdentity` associates an instance of this class with a
 *           [receiverID] msg field
 *           `myIdentity` is set upon creation and not a subject to change in runtime as
 *           this *might* break all registered IProtos
 *           discards message if:
 *           1. [receiverID] does not match `myIdentity`
 *           2. no proto is registered for [senderID]
 *           3. message format failure
 *           possible ways to improve this class:
 *           1. add a descriptor-like iface associating particular
 *               ([receiverID] [senderId]) pair with an int and use this int to register
 *               IProto
 *           2. add support for IProto factory effectively removing `useTransportProto` flag
 *               from `RegisterProto()` method args
 *  \coryright BSD 3-clause
 */
public class ProtoStack {
    /*!
     *  \brief watchdogPeriod for disposable IProtos. set via constructor
     */
    TimeSpan defaultTimeSpan = new TimeSpan (0,10,0);

    /*!
     *  \brief a collection of SessionProtoCollection associated with [senderID]
     */
    Dictionary<string, SessionProtoCollection> sessionProtosCollections
        = new Dictionary <string, SessionProtoCollection> ();

    /*!
     *  \brief defines "receiverID" for message parser
     */
    string myIdentity;

    /*!
     *  \brief holds a set of "service" IProto factories associated with a channelIDs
     */
    Dictionary<string, IProtoFactory> listeners =
        new Dictionary<string, IProtoFactory> ();

    /*!
     *  \brief service variable. although it's used only in `Step()`, it was
     *         placed here to avoid excessive allocations
     */
    List <string> collectionsToKill = new List<string> ();

    /*!
     *  \brief checks if there are any IProtos registered
     */
    public bool IsEmpty {get {return sessionProtosCollections.Count == 0;}}

    /*!
     *  \brief constructor
     *  \param id "receiverID"
     *  \param listenersWatchdog override defaultTimeSpan for IProtos registered as
     *         temporary
     */
    public ProtoStack (string id, TimeSpan listenersWatchdog) {
        defaultTimeSpan = listenersWatchdog;
        myIdentity = id;
    }

    /*!
     *  \brief constructor, that does not override defaultTimeSpan
     *  \param id "receiverID"
     */
    public ProtoStack (string id) {
        myIdentity = id;
    }

    /*!
     *  \brief tries to register a listener (aka "service")
     *  \param channel channelID to associate the factory to
     *  \param factory an instance of class able to spawn new IProto instances
     *         when requested by the ProtoStack
     */
    public bool AddListener (string channel, IProtoFactory factory) {
        if (!listeners.ContainsKey (channel)) {
            listeners.Add (channel, factory);
            return true;
        }
        return false;
    }


    /*!
     *  \brief tries to associate the proto with the "pipe" defined by channel
     *  \param otherSide the mask for "senderID" msg field
     *  \param channel channelID to associate the proto to
     *  \param proto IProto to register
     *  \param useTransportProto wrap the proto in a TransportProto instance
     *         to unsure msg delivery
     */
    public bool RegisterProto (
        string otherSide,
        string channel,
        IProto proto,
        bool useTransportProto
    ) {
        if (!sessionProtosCollections.ContainsKey (otherSide)) {
            sessionProtosCollections.Add (
                otherSide,
                new SessionProtoCollection ()
            );
        }
        return sessionProtosCollections[otherSide].RegisterProto (
            channel, proto, useTransportProto);
    }

    /*!
     *  \brief tries to associate the proto with the "pipe" defined by channel
     *         the proto is treated as "temporary" and "disposable" and
     *         will be automatically deleted if idle for watchdogPeriod
     *  \param otherSide the mask for "senderID" msg field
     *  \param channel channelID to associate the proto to
     *  \param proto IProto to register
     *  \param useTransportProto wrap the proto in a TransportProto instance
     *         to unsure msg delivery
     *  \param watchdogPeriod if proto is idle for this duration, it will be
     *         automatically deleted
     */
    public bool RegisterProto (
        string otherSide,
        string channel,
        IProto proto,
        bool useTransportProto,
        TimeSpan watchdogPeriod
    ) {
        if (!sessionProtosCollections.ContainsKey (otherSide)) {
            sessionProtosCollections.Add (
                otherSide,
                new SessionProtoCollection ()
            );
        }
        return sessionProtosCollections[otherSide].RegisterProto (
            channel, proto, useTransportProto, watchdogPeriod);
    }

    /*!
     *  \brief tries to remove a proto defined by "senderID" and "channelID"
     *  \param otherSide "senderID"
     *  \param channel "channelID"
     *  \param pleaseKillMe true if there are no protos left after this operation
     *         indicates that ProtoStack could be safely deleted if required
     */
    public bool UnregisterProto (
        string otherSide,
        string channel,
        out bool pleaseKillMe
    ) {
        pleaseKillMe = false;
        bool ret = false;
        if (sessionProtosCollections.ContainsKey (otherSide)) {
            ret = sessionProtosCollections[otherSide].UnregisterProto (channel);

            if (sessionProtosCollections[otherSide].ReadyToDie) {
                sessionProtosCollections.Remove (otherSide);
            }
            pleaseKillMe = sessionProtosCollections.Count == 0;
        } else {
            throw new Exception ("this is a warning :)");
        }
        return ret;
    }

    /*!
     *  \brief calls a step on all registered IProtos potentially returning
     *         one message for transmission
     *  \param canSend true if one message could be pulled from a collection of IProtos
     */
    public string Step (bool canSend) {
        StringBuilder msg = null;
        foreach (var kvp in sessionProtosCollections) {
            if (canSend) {
                if (msg == null) {
                    msg = kvp.Value.StepCanSend ();

                    if (msg != null) {
                        msg.Insert (0, kvp.Key + "%" + myIdentity + "%");
                        canSend = false;
                    }
                } else {
                    throw new Exception (
                        "this should never happen. my init is to blame");
                }
            } else {
                kvp.Value.StepCantSend ();
            }
            if (kvp.Value.ReadyToDie) {
                collectionsToKill.Add (kvp.Key);
            }
        }
        foreach (string nameToKill in collectionsToKill) {
            sessionProtosCollections.Remove (nameToKill);
        }
        collectionsToKill.Clear();
        if (msg != null) {
            return msg.ToString();
        }
        return null;
    }

    /*!
     *  \brief parses the message and feeds the data section to a IProto if the message
     *         is valid and there is a IProto registered for senderID-channelID mask
     *         it also spawns IProtos for registered IProtoFactorys
     *  \param msg radio message to handle
     */
    public void HandleMsg (string msg) {
        if (msg == null) {
            throw new Exception ("null msg");
        }
        int offset = -1;
        if (msg.StartsWith (myIdentity)) {
            if (msg[myIdentity.Length] == '%') {
                offset = myIdentity.Length+1;
            }
        }
        if (offset > 0) {
            int endIndex = msg.IndexOf ('%', offset);
            if (endIndex > 0) {
                string otherSide = msg.Substring (offset, endIndex-offset);
                bool protoFound = false;
                if (sessionProtosCollections.ContainsKey (otherSide)) {
                    protoFound |= sessionProtosCollections[otherSide].HandleMsg(
                        msg, endIndex+1);
                }
                if (!protoFound) {
                    // check if someone listens for a channel
                    int chOffset = endIndex+1;
                    int chEndIndex = msg.IndexOf ('%', chOffset);
                    if (chEndIndex > 0) {
                        string chName = msg.Substring (
                            chOffset, chEndIndex-chOffset);
                        if (listeners.ContainsKey (chName)) {
                            if (!RegisterProto (
                                otherSide,
                                chName,
                                listeners[chName].SpawnProto(),
                                listeners[chName].RequireTransport,
                                defaultTimeSpan
                            )) {
                                throw new Exception (
                                    "failed to fire a listener " +chName);
                            }
                            sessionProtosCollections[otherSide].HandleMsg (
                                msg, endIndex+1);
                            return;
                        }
                    }
                } else {
                    return;
                }
            }
        }
        Console.WriteLine ("msg discarded");
    }
}

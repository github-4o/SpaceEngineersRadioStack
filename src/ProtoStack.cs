using System;
using System.Text;
using System.Collections.Generic;

/*!
 *  \author  4o
 *  \brief "top" lvl for all `IProto`s associated with network node
 *  \details this is the "top" lvl for a proto stack. it is the first to parse
 *           incoming message.
 *           <BR>message structure:
 *           <BR>[receiverID] [senderId] [channelID] [protoID] [message]
 *           <BR>..........^..........^
 *           <BR>......you are here
 *           <BR>this class implements network lvl. upon message parsing the
 *           first thing to check if receiverID matches `myIdentity` (which is
 *           defined upon instance creation)
 *           <BR>if receiverID check fails, message is discarded as addressed to
 *           someone else.
 *           <BR>else this class checks if there is an protos registered for
 *           senderID field of incoming message. `sessionProtosCollections`
 *           represents a collection of `IProto`s associated with a
 *           senderID. if there is a valid SessionProtoCollection<->senderID
 *           pair, then incoming message is passed to the matching
 *           SessionProtoCollection. message is passed "as is", but a
 *           valid offset is provided to instruct SessionProtoCollection about
 *           the beginning of possible channelID field.
 *           <BR>
 *           <BR>the reverse path (msg sending) is implemented through `Step`
 *           method. it is suggested that `Step` function is called once in a
 *           while to allow all involved classes to work their dark magic.
 *           when `Step(true)` is called, this class checks if there is an
 *           IProto willing to send a message. only one proto per `Step(true)`
 *           run can send the message. this message is prefixed with
 *           receiverID which is pulled from SessionProtoCollection, which wants
 *           to send the message and senderID, which is pulled from myIdentity.
 *           <BR>there are 2 distinct interface for this class:
 *           1. "System" interface, which should be connected to "runtime" code
 *           handling regular tasks. this interface consists of 2 methods:
 *           - string Step(bool)
 *           <BR> this method is designed to be called every time the PB is
 *           triggered. in a nut shell this method calls Step() on all
 *           registered `IProto`s thus allowing work scheduling inside the
 *           `IProto`s. external runtime code instructs ProtoStack about the
 *           possibility to send one message with Step(true) call.
 *           - void HandleMsg(string msg);
 *           <BR>if external runtime code receives a radio message, then it
 *           should call HandleMsg(string msg) on ProtoStack to make it parse
 *           that message.
 *           2. "User" iface:
 *           - register an IProto via AddListener() and RegisterProto()
 *           functions
 *           - UnregisterProto()
 *           <BR><BR>in general there are 2 model of operation for `IProto`s:
 *           1. "Pipe" - persistent association of an IProto with
 *           [senderID, channelID] mask. if there are 2 `IProto`s on 2 grids
 *           created this way, then they form a permanent pipe.
 *           2. "Service" - a server-client mode, where a client is created as
 *           an IProto, and a separate server IProto instance created for each
 *           client. this is done with the help of IProtoFactory, which is
 *           responsible for spawning new IProto instances every time ProtoStack
 *           receives an appropriate message and no valid `IProto`s can
 *           handle this message, but there is a valid IProtoFactory associated
 *           with channelID from the message. note that IProtoFactory is
 *           associated with channelID alone. after a new "server" IProto is
 *           spawned this way, it is internally added as a regular IProto, for
 *           a senderID from the incoming message. this effectively creates an
 *           instance of "server" IProto for all demanding "client" IProtos.
 *           to avoid DDOS-like behavior, "server" instances are created as
 *           "spoilable". if they remain idle for some time (10 min by default)
 *           then they are automatically deleted. idle is defined as: "no
 *           messages were handled by a spoilable IProto in a given time."
 *           <BR><BR>there are 3 flavors of handling IProto. they are transparent
 *           to IProto and defined by the choice of register function.
 *           - RegisterProto(string, string, IProto, bool) registers a permanent
 *           IProto, which could be removed only by calling UnregisterProto().
 *           - RegisterProto(string, string, IProto, bool, TimeSpan) registers
 *           "spoilable" IProto, which will be deleted when idle for TimeSpan
 *           period.
 *           - AddListener(string, IProtoFactory) registers an IProto spawner.
 *           all IProtos spawned by IProtoFactory are internally registered as
 *           spoilable.
 *           <BR>NOTE1: i had long conversations with myself about the receiverID
 *           field. what should be an ID for a particular network node? the very
 *           first answer is something unique (MAC). this prevents 2 network
 *           nodes from answering same message. but on the other hand, this
 *           forces a user to hardcore all network names, which is not very good
 *           . the question remains open, but my current answer is:
 *           - there are "unique" indispensable persistent network nodes, where
 *           I (as user) is responsible that there are no nodes with matching
 *           IDs. in general there will be a handful of this type of nodes. a
 *           base is a good example.
 *           - disposable non-persistent nodes like miners, which are identified
 *           by PB.EntityId.
 *           <BR>what this approach lacks right now is "enter network" procedure
 *           for "unique" nodes. so a check is performed if there is another
 *           similarly named node. but this creates many more problems...
 *           <BR>NOTE2: patches are most welcome. if you have a use case that
 *           can't be handled by this ProtoStack, then contact me in Space
 *           Engineers Discord (@4o#0098)
 */
public class ProtoStack {
    /*!
     *  \author  4o
     *  \brief watchdogPeriod for disposable IProtos. set via constructor
     */
    TimeSpan defaultTimeSpan = new TimeSpan (0,10,0);

    /*!
     *  \author  4o
     *  \brief a collection of SessionProtoCollection associated with [senderID]
     */
    Dictionary<string, SessionProtoCollection> sessionProtosCollections
        = new Dictionary <string, SessionProtoCollection> ();

    /*!
     *  \author  4o
     *  \brief defines "receiverID" for message parser
     */
    string myIdentity;

    /*!
     *  \author  4o
     *  \brief holds a set of "service" IProto factories associated with a channelIDs
     */
    Dictionary<string, IProtoFactory> listeners =
        new Dictionary<string, IProtoFactory> ();

    /*!
     *  \author  4o
     *  \brief service variable. although it's used only in `Step()`, it was
     *         placed here to avoid excessive allocations
     */
    List <string> collectionsToKill = new List<string> ();

    /*!
     *  \author  4o
     *  \brief checks if there are any IProtos registered
     */
    public bool IsEmpty {get {return sessionProtosCollections.Count == 0;}}

    /*!
     *  \author  4o
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
     *  \author  4o
     *  \brief constructor, that does not override defaultTimeSpan
     *  \param id "receiverID"
     */
    public ProtoStack (string id) {
        myIdentity = id;
    }

    /*!
     *  \author  4o
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
     *  \author  4o
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
     *  \author  4o
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
     *  \author  4o
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
     *  \author  4o
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
     *  \author  4o
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
